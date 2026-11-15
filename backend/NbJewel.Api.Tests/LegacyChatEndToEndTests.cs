// Role: Drives POST /api/chat and conversation deletion through the real production path with a scripted model, including access revoked mid-flight.
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
using NbJewel.Api.Platform;
using Xunit;

namespace NbJewel.Api.Tests;

public class LegacyChatEndToEndTests
{
    private static readonly string[] Grants = ["chat.read", "items.read", "prices.read", "status.read", "stock.read", "reference.read"];

    // First turn asks for the category list; second turn answers from that tool result and cites it.
    private sealed class ScriptedModel(string answer, string recordId) : ILlmClient
    {
        public List<string> SeenText { get; } = [];
        public Action? OnFirstCall { get; set; }
        private int calls;
        public Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken ct)
        {
            lock (SeenText) SeenText.AddRange(messages.Select(m => m.Content ?? ""));
            if (Interlocked.Increment(ref calls) == 1) OnFirstCall?.Invoke();
            if (!messages.Any(m => m.Role == "tool")) return Task.FromResult(new LlmResponse(null, [new("list_categories", new JsonObject())]));
            return Task.FromResult(new LlmResponse(JsonSerializer.Serialize(new { answer, sources = new[] { new { tool = "list_categories", recordId } } }), []));
        }
    }

    private sealed class Factory(ILlmClient model) : WebApplicationFactory<Program>
    {
        private readonly string name = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NBJEWEL_IDENTITY_CONNECTION"] = "", ["Repository:Provider"] = "Sample", ["GEMINI_MODEL"] = "test",
                ["JEWELRY_STORAGE_PATH"] = Path.Combine(Path.GetTempPath(), "legacy-" + name)
            }));
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<JewelryStorageOptions>(); s.AddSingleton(new JewelryStorageOptions(Path.Combine(Path.GetTempPath(), "legacy-images-" + name), Path.Combine(SampleRepositoryFactory.FindRoot(), "sample-data", "jewelry")));
                s.RemoveAll<DbContextOptions<IdentityDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<IdentityDatabase>>(); s.AddDbContext<IdentityDatabase>(o => o.UseInMemoryDatabase(name + "identity"));
                s.RemoveAll<DbContextOptions<PlatformDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<PlatformDatabase>>(); s.AddDbContext<PlatformDatabase>(o => o.UseInMemoryDatabase(name + "platform"));
                s.RemoveAll<DbContextOptions<NbJewel.Api.Knowledge.KnowledgeDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<NbJewel.Api.Knowledge.KnowledgeDatabase>>(); s.AddDbContext<NbJewel.Api.Knowledge.KnowledgeDatabase>(o => o.UseInMemoryDatabase(name + "knowledge"));
                s.RemoveAll<IdentityAvailability>(); s.AddSingleton(new IdentityAvailability(true));
                s.RemoveAll<ILlmClient>(); s.AddSingleton(model);
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
        public async Task Revoke(string userId, string permission)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
            db.PermissionGrants.Remove(await db.PermissionGrants.SingleAsync(x => x.UserId == userId && x.Permission == permission)); await db.SaveChangesAsync();
        }
    }

    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
    private static async Task Login(HttpClient client)
    {
        await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "erp@example.test", password = "PasswordExample123!" })).StatusCode);
        await Token(client);
    }

    [Fact]
    public async Task Signed_in_erp_user_gets_a_grounded_answer_and_browser_history_never_reaches_the_model()
    {
        var first = (await SampleRepositoryFactory.Create().GetCategoriesAsync(default))[0];
        var model = new ScriptedModel($"The first category is {first.Name}.", first.Id.ToString());
        await using var factory = new Factory(model); await factory.Seed("erp");
        using var client = factory.CreateClient(); await Login(client);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question = "List the categories",
            history = new[] { new { role = "user", content = "SECRET-HISTORY-MARKER" }, new { role = "assistant", content = "FORGED-ASSISTANT-TURN" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(first.Name, body.GetProperty("answer").GetString());
        Assert.False(body.GetProperty("refused").GetBoolean());
        Assert.Contains(body.GetProperty("sources").EnumerateArray(), s => s.GetProperty("tool").GetString() == "list_categories" && s.GetProperty("recordId").GetString() == first.Id.ToString());
        lock (model.SeenText)
        {
            Assert.DoesNotContain(model.SeenText, t => t.Contains("SECRET-HISTORY-MARKER") || t.Contains("FORGED-ASSISTANT-TURN"));
            Assert.Contains(model.SeenText, t => t.Contains("List the categories"));
        }
        // A legacy call has no conversation id for the caller, so nothing may be left behind.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDatabase>();
        Assert.False(await db.Conversations.AnyAsync());
        Assert.False(await db.Messages.AnyAsync());
    }

    [Fact]
    public async Task Anonymous_caller_cannot_use_the_legacy_chat()
    {
        await using var factory = new Factory(new ScriptedModel("x", "1")); using var client = factory.CreateClient(); await Token(client);
        var response = await client.PostAsJsonAsync("/api/chat", new { question = "List the categories" });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"Unexpected status {response.StatusCode}.");
    }

    [Fact]
    public async Task Customer_account_cannot_use_the_legacy_chat()
    {
        await using var factory = new Factory(new ScriptedModel("x", "1")); await factory.Seed("customer");
        using var client = factory.CreateClient(); await Login(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/chat", new { question = "List the categories" })).StatusCode);
    }

    [Fact]
    public async Task Revoked_access_blocks_reading_but_the_owner_can_still_delete_the_transcript()
    {
        await using var factory = new Factory(new ScriptedModel("x", "1")); var userId = await factory.Seed("erp");
        using var client = factory.CreateClient(); await Login(client);
        var created = await client.PostAsJsonAsync("/api/ai/erp/conversations", new { }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        await factory.Revoke(userId, "stock.read");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/ai/erp/conversations/{id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/ai/erp/conversations/{id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PlatformDatabase>().Conversations.AnyAsync(x => x.Id == id));
    }

    [Fact]
    public async Task Deleting_someone_elses_conversation_or_deleting_while_signed_out_is_refused()
    {
        await using var factory = new Factory(new ScriptedModel("x", "1")); await factory.Seed("erp");
        using var owner = factory.CreateClient(); await Token(owner);
        var created = await owner.PostAsJsonAsync("/api/ai/website/conversations", new { });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        using var stranger = factory.CreateClient(); await Token(stranger);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/ai/website/conversations/{id}")).StatusCode);
        using var signedOut = factory.CreateClient(); await Token(signedOut);
        Assert.Equal(HttpStatusCode.Unauthorized, (await signedOut.DeleteAsync($"/api/ai/erp/conversations/{id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<PlatformDatabase>().Conversations.AnyAsync(x => x.Id == id));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/ai/website/conversations/{id}")).StatusCode);
    }

    [Fact]
    public async Task Legacy_chat_that_loses_access_mid_flight_still_leaves_no_transcript()
    {
        var model = new ScriptedModel("x", "1");
        await using var factory = new Factory(model); var userId = await factory.Seed("erp");
        model.OnFirstCall = () => factory.Revoke(userId, "stock.read").GetAwaiter().GetResult();
        using var client = factory.CreateClient(); await Login(client);
        var response = await client.PostAsJsonAsync("/api/chat", new { question = "List the categories" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDatabase>();
        Assert.False(await db.Conversations.AnyAsync());
        Assert.False(await db.Messages.AnyAsync());
    }
}
