// Role: Exercises conversation ownership, revocation, limits and protected storage through HTTP.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NbJewel.Api.Chat;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Platform;
using Xunit;

namespace NbJewel.Api.Tests;

public partial class PlatformConversationTests
{
    private static readonly string[] Grants = ["chat.read", "items.read", "prices.read", "status.read", "stock.read", "reference.read"];
    private sealed class ScriptedModel : ILlmClient
    {
        private int calls;
        public TaskCompletionSource<bool>? TwoEntered { get; set; }
        public bool EvidenceScript { get; set; }
        public bool AttemptPrivateTool { get; set; }
        public string? AnswerText { get; set; }
        public TaskCompletionSource<bool>? Entered { get; set; }
        public TaskCompletionSource<bool>? Release { get; set; }
        public async Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken ct)
        {
            if (AttemptPrivateTool && (!EvidenceScript || messages.Any(m => m.Role == "tool")))
                return new LlmResponse(null, [new("get_stock_by_store", JsonNode.Parse("{\"itemId\":1}")!.AsObject())]);
            if (EvidenceScript && !messages.Any(m => m.Role == "tool"))
                return new LlmResponse(null, [new("search_knowledge", new JsonObject { ["query"] = "alpha" }), new("search_knowledge", new JsonObject { ["query"] = "beta" })]);
            if (Interlocked.Increment(ref calls) == 2) TwoEntered?.TrySetResult(true);
            Entered?.TrySetResult(true);
            if (Release is not null) await Release.Task.WaitAsync(ct);
            return EvidenceScript
                ? new LlmResponse(JsonSerializer.Serialize(new { answer = AnswerText ?? "The sapphire care guidance applies.", sources = new[] { new { tool = "search_knowledge", recordId = "beta" } } }), [])
                : new LlmResponse("{\"answer\":\"I cannot verify this.\",\"sources\":[]}", []);
        }
    }
    private sealed class MutableKnowledge : IKnowledgeRetriever, IKnowledgeEvidenceValidator
    {
        public string? PoisonText { get; set; }
        public string BetaText { get; set; } = "Other approved advice.";
        public bool AlphaAllowed { get; set; } = true;
        public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(AssistantContext context, string query, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KnowledgeHit>>(query == "alpha"
                ? [new("doc", "version", "alpha", "Care", PoisonText ?? "The sapphire care guidance applies.")]
                : [new("doc", "version", "beta", "Other", BetaText)]);
        public Task<bool> CanUseAsync(AssistantContext context, string chunkId, CancellationToken ct) => Task.FromResult(chunkId == "beta" || (chunkId == "alpha" && AlphaAllowed));
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string name = Guid.NewGuid().ToString();
        public ScriptedModel Model { get; } = new();
        public MutableKnowledge Knowledge { get; } = new();
        public int AssistantLimit { get; set; } = 60;
        public int DailyTokenBudget { get; set; } = 50000000;
        public string? WebsiteInstructions { get; set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["NBJEWEL_IDENTITY_CONNECTION"] = "", ["Repository:Provider"] = "Sample", ["BUSINESS_STORAGE_PATH"] = Path.Combine(Path.GetTempPath(), "nbjewel-policy-" + name), ["GEMINI_MODEL"] = "test", ["AiPlatform:AssistantRequestsPerMinute"] = AssistantLimit.ToString(), ["AiPlatform:DailyReservedTokenBudget"] = DailyTokenBudget.ToString(), ["JEWELRY_STORAGE_PATH"] = Path.Combine(Path.GetTempPath(), "conv-" + name), ["Assistants:website:Instructions"] = WebsiteInstructions
            }));
            builder.ConfigureTestServices(s => {
                s.RemoveAll<JewelryStorageOptions>(); s.AddSingleton(new JewelryStorageOptions(Path.Combine(Path.GetTempPath(), "conv-images-" + name), Path.Combine(SampleRepositoryFactory.FindRoot(), "sample-data", "jewelry")));
                s.RemoveAll<DbContextOptions<IdentityDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<IdentityDatabase>>(); s.AddDbContext<IdentityDatabase>(o => o.UseInMemoryDatabase(name + "identity"));
                s.RemoveAll<DbContextOptions<PlatformDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<PlatformDatabase>>(); s.AddDbContext<PlatformDatabase>(o => o.UseInMemoryDatabase(name + "platform"));
                s.RemoveAll<DbContextOptions<KnowledgeDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<KnowledgeDatabase>>(); s.AddDbContext<KnowledgeDatabase>(o => o.UseInMemoryDatabase(name + "knowledge"));
                s.RemoveAll<IdentityAvailability>(); s.AddSingleton(new IdentityAvailability(true));
                s.RemoveAll<ILlmClient>(); s.AddSingleton<ILlmClient>(Model);
                s.RemoveAll<IKnowledgeRetriever>(); s.AddSingleton<IKnowledgeRetriever>(Knowledge);
                s.RemoveAll<IKnowledgeEvidenceValidator>(); s.AddSingleton<IKnowledgeEvidenceValidator>(Knowledge);
            });
        }
        public async Task<string> Seed(string type)
        {
            using var scope = Services.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "erp@example.test", Email = "erp@example.test", AccountType = type, AllStores = false };
            Assert.True((await manager.CreateAsync(user, "PasswordExample123!")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
            db.PermissionGrants.AddRange(Grants.Select(p => new PermissionGrant { UserId = user.Id, Permission = p }));
            db.StoreGrants.Add(new StoreGrant { UserId = user.Id, StoreId = 1 }); await db.SaveChangesAsync();
            if (user.AccountType == "erp") await scope.ServiceProvider.GetRequiredService<BusinessMemberships>().AssignAsync(user.Id, BusinessRegistry.DemoSlug, "staff", default); return user.Id;
        }
    }
    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
    private static async Task Login(HttpClient client)
    {
        await Token(client); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "erp@example.test", password = "PasswordExample123!" })).StatusCode); await Token(client);
    }
    private static async Task<string> Create(HttpClient client, string assistant)
    {
        var response = await client.PostAsJsonAsync($"/api/ai/{assistant}/conversations", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, string assistant, string id, string text, string? requestId = null) => client.PostAsJsonAsync($"/api/ai/{assistant}/conversations/{id}/messages", new { text, clientRequestId = requestId ?? Guid.NewGuid().ToString() });
    [Fact]
    public async Task Visitor_ownership_and_authenticated_context_are_separate()
    {
        await using var factory = new Factory(); await factory.Seed("erp");
        using var first = factory.CreateClient(); using var second = factory.CreateClient(); await Token(first);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.PostAsJsonAsync("/api/ai/erp/conversations", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await first.PostAsJsonAsync("/api/ai/unknown/conversations", new { })).StatusCode);
        var id = await Create(first, "website");
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/api/ai/website/conversations/{id}/messages")).StatusCode);
        await Login(first);
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync($"/api/ai/website/conversations/{id}/messages")).StatusCode);
        var erp = await Create(first, "erp");
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync($"/api/ai/website/conversations/{erp}/messages")).StatusCode);
    }
    [Fact]
    public async Task Customer_cannot_enter_erp()
    {
        await using var factory = new Factory(); await factory.Seed("customer"); using var client = factory.CreateClient(); await Login(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/ai/erp/conversations", new { })).StatusCode);
    }
    [Fact]
    public async Task Idempotency_protected_storage_expiry_and_deletion()
    {
        await using var factory = new Factory(); using var client = factory.CreateClient(); await Token(client); var id = await Create(client, "website");
        var key = Guid.NewGuid().ToString(); var first = await Send(client, "website", id, "who are you", key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var repeat = await Send(client, "website", id, "who are you", key); Assert.Equal(await first.Content.ReadAsStringAsync(), await repeat.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, "website", id, "different question", key)).StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/ai/website/conversations/{id}/messages"); Assert.Equal(2, history.GetArrayLength());
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<PlatformDatabase>(); var messages = await db.Messages.Where(x => x.ConversationId == id).ToListAsync(); Assert.Equal(2, messages.Count); Assert.All(messages, x => Assert.DoesNotContain("who are you", x.ProtectedText)); }
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/ai/website/conversations/{id}")).StatusCode);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<PlatformDatabase>(); Assert.False(await db.Messages.AnyAsync(x => x.ConversationId == id)); Assert.False(await db.Turns.AnyAsync(x => x.ConversationId == id)); }
        var expired = await Create(client, "website");
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<PlatformDatabase>(); var row = await db.Conversations.FindAsync(expired); row!.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/ai/website/conversations/{expired}/messages")).StatusCode);
    }
    [Fact]
    public async Task Seventh_message_is_throttled()
    {
        await using var factory = new Factory(); using var client = factory.CreateClient(); await Token(client); var id = await Create(client, "website");
        for (var i = 0; i < 6; i++) Assert.Equal(HttpStatusCode.OK, (await Send(client, "website", id, "who are you")).StatusCode);
        Assert.Equal((HttpStatusCode)429, (await Send(client, "website", id, "who are you")).StatusCode);
    }
    [Fact]
    public async Task Token_reservation_is_realistic_so_the_daily_budget_admits_normal_use()
    {
        // 1,000,000 reserved tokens per day is far more than six short messages need. A per-message reservation above that refuses the first one.
        await using var factory = new Factory { DailyTokenBudget = 1000000 }; using var client = factory.CreateClient(); await Token(client); var id = await Create(client, "website");
        for (var i = 0; i < 6; i++) Assert.Equal(HttpStatusCode.OK, (await Send(client, "website", id, "who are you")).StatusCode);
    }
    [Fact]
    public async Task Revoked_grant_blocks_history()
    {
        await using var factory = new Factory(); var userId = await factory.Seed("erp"); using var client = factory.CreateClient(); await Login(client); var id = await Create(client, "erp");
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>(); db.PermissionGrants.Remove(await db.PermissionGrants.SingleAsync(x => x.UserId == userId && x.Permission == "stock.read")); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/ai/erp/conversations/{id}/messages")).StatusCode);
    }
    [Fact]
    public async Task Revoking_store_scope_blocks_history()
    {
        await using var factory = new Factory(); var userId = await factory.Seed("erp"); using var client = factory.CreateClient(); await Login(client); var id = await Create(client, "erp");
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>(); db.StoreGrants.Remove(await db.StoreGrants.SingleAsync(x => x.UserId == userId)); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/ai/erp/conversations/{id}/messages")).StatusCode);
    }
    [Fact]
    public async Task Revocation_while_model_runs_blocks_response()
    {
        await using var factory = new Factory(); var userId = await factory.Seed("erp");
        factory.Model.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); factory.Model.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = factory.CreateClient(); await Login(client); var id = await Create(client, "erp");
        var pending = Send(client, "erp", id, "Find me a gold ring");
        await factory.Model.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>(); db.PermissionGrants.Remove(await db.PermissionGrants.SingleAsync(x => x.UserId == userId && x.Permission == "stock.read")); await db.SaveChangesAsync(); }
        factory.Model.Release.SetResult(true);
        Assert.Equal(HttpStatusCode.Forbidden, (await pending).StatusCode);
    }
    [Fact]
    public async Task Assistant_budget_applies_across_visitors()
    {
        await using var factory = new Factory { AssistantLimit = 1 }; using var first = factory.CreateClient(); using var second = factory.CreateClient();
        await Token(first); await Token(second); var firstId = await Create(first, "website"); var secondId = await Create(second, "website");
        Assert.Equal(HttpStatusCode.OK, (await Send(first, "website", firstId, "who are you")).StatusCode);
        Assert.Equal((HttpStatusCode)429, (await Send(second, "website", secondId, "who are you")).StatusCode);
    }
    [Fact]
    public async Task Revoked_uncited_tool_evidence_is_not_returned_after_generation()
    {
        await using var factory = new Factory(); factory.Model.EvidenceScript = true;
        factory.Model.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); factory.Model.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = factory.CreateClient(); await Token(client); var id = await Create(client, "website");
        var key = Guid.NewGuid().ToString();
        var pending = Send(client, "website", id, "What care guidance applies?", key);
        await factory.Model.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, "website", id, "What care guidance applies?", key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/ai/website/conversations/{id}")).StatusCode);
        factory.Knowledge.AlphaAllowed = false; factory.Model.Release.SetResult(true);
        var response = await pending; Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reply = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("sapphire care", reply, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Revoked_uncited_tool_evidence_is_hidden_from_replay_and_history()
    {
        await using var factory = new Factory(); factory.Model.EvidenceScript = true;
        using var client = factory.CreateClient(); await Token(client); var id = await Create(client, "website"); var key = Guid.NewGuid().ToString();
        var first = await Send(client, "website", id, "What care guidance applies?", key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode); var initial = await first.Content.ReadAsStringAsync();
        Assert.Contains("Other approved advice", initial, StringComparison.OrdinalIgnoreCase); Assert.Contains("beta", initial); Assert.DoesNotContain("alpha", initial);
        Assert.DoesNotContain("sapphire care", initial, StringComparison.OrdinalIgnoreCase);
        factory.Knowledge.AlphaAllowed = false;
        var replay = await Send(client, "website", id, "What care guidance applies?", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.DoesNotContain("Other approved advice", await replay.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var history = await client.GetStringAsync($"/api/ai/website/conversations/{id}/messages"); Assert.DoesNotContain("Other approved advice", history, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Third_concurrent_message_for_same_visitor_is_rejected()
    {
        await using var factory = new Factory();
        factory.Model.TwoEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Model.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = factory.CreateClient(); await Token(client);
        var first = await Create(client, "website"); var second = await Create(client, "website"); var third = await Create(client, "website");
        var one = Send(client, "website", first, "Tell me about rings");
        var two = Send(client, "website", second, "Tell me about necklaces");
        await factory.Model.TwoEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { Assert.Equal((HttpStatusCode)429, (await Send(client, "website", third, "Tell me about bracelets")).StatusCode); }
        finally { factory.Model.Release.TrySetResult(true); }
        Assert.Equal(HttpStatusCode.OK, (await one).StatusCode); Assert.Equal(HttpStatusCode.OK, (await two).StatusCode);
    }
}
