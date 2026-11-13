// Role: Proves anonymous abuse controls key on the real client address (trusted proxies only), not on a droppable cookie, and that cleanup bounds stored state.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
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
using NbJewel.Api.Identity;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Platform;
using Xunit;

namespace NbJewel.Api.Tests;

public class AnonymousAbuseControlTests
{
    private sealed class UnusedModel : ILlmClient
    {
        public Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken ct) =>
            throw new InvalidOperationException("The model must not be called by these tests.");
    }

    // Sets the transport address from a test header before the application pipeline runs, like a real socket would.
    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, step) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Remote", out var value)) context.Connection.RemoteIpAddress = IPAddress.Parse(value.ToString());
                return step();
            });
            next(app);
        };
    }

    private sealed class Factory(Dictionary<string, string?>? extra = null) : WebApplicationFactory<Program>
    {
        private readonly string name = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["NBJEWEL_IDENTITY_CONNECTION"] = "", ["Repository:Provider"] = "Sample", ["GEMINI_MODEL"] = "test",
                    ["JEWELRY_STORAGE_PATH"] = Path.Combine(Path.GetTempPath(), "abuse-" + name)
                };
                foreach (var pair in extra ?? []) values[pair.Key] = pair.Value;
                c.AddInMemoryCollection(values);
            });
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<JewelryStorageOptions>(); s.AddSingleton(new JewelryStorageOptions(Path.Combine(Path.GetTempPath(), "abuse-images-" + name), Path.Combine(SampleRepositoryFactory.FindRoot(), "sample-data", "jewelry")));
                s.RemoveAll<DbContextOptions<IdentityDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<IdentityDatabase>>(); s.AddDbContext<IdentityDatabase>(o => o.UseInMemoryDatabase(name + "identity"));
                s.RemoveAll<DbContextOptions<PlatformDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<PlatformDatabase>>(); s.AddDbContext<PlatformDatabase>(o => o.UseInMemoryDatabase(name + "platform"));
                s.RemoveAll<DbContextOptions<NbJewel.Api.Knowledge.KnowledgeDatabase>>(); s.RemoveAll<IDbContextOptionsConfiguration<NbJewel.Api.Knowledge.KnowledgeDatabase>>(); s.AddDbContext<NbJewel.Api.Knowledge.KnowledgeDatabase>(o => o.UseInMemoryDatabase(name + "knowledge"));
                s.RemoveAll<IdentityAvailability>(); s.AddSingleton(new IdentityAvailability(true));
                s.RemoveAll<ILlmClient>(); s.AddSingleton<ILlmClient>(new UnusedModel());
                s.AddSingleton<IStartupFilter, RemoteAddressFilter>();
            });
        }
        public async Task SeedCustomer()
        {
            using var scope = Services.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.CreateAsync(new ApplicationUser { UserName = "customer@example.test", Email = "customer@example.test", AccountType = "customer" }, "PasswordExample123!")).Succeeded);
        }
    }

    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
    // A fresh client has an empty cookie jar, so every call looks like a brand-new visitor.
    private static async Task<HttpClient> Visitor(Factory factory, string? remote = null, string? forwardedFor = null)
    {
        var client = factory.CreateClient();
        if (remote is not null) client.DefaultRequestHeaders.Add("X-Test-Remote", remote);
        if (forwardedFor is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        await Token(client); return client;
    }
    private static async Task<HttpResponseMessage> Create(HttpClient client) => await client.PostAsJsonAsync("/api/ai/website/conversations", new { });
    private static async Task<string> Created(HttpClient client)
    {
        var response = await Create(client); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, string id) =>
        client.PostAsJsonAsync($"/api/ai/website/conversations/{id}/messages", new { text = "who are you", clientRequestId = Guid.NewGuid().ToString() });

    [Fact]
    public async Task Dropping_the_cookie_does_not_reset_the_conversation_creation_cap()
    {
        await using var factory = new Factory(new() { ["AiPlatform:AnonymousIpConversationsPerDay"] = "3" });
        for (var i = 0; i < 3; i++) { using var client = await Visitor(factory); await Created(client); }
        using var fourth = await Visitor(factory);
        Assert.Equal((HttpStatusCode)429, (await Create(fourth)).StatusCode);
    }

    [Fact]
    public async Task Dropping_the_cookie_does_not_reset_the_message_cap()
    {
        await using var factory = new Factory(new() { ["AiPlatform:AnonymousIpRequestsPerMinute"] = "2" });
        for (var i = 0; i < 2; i++) { using var client = await Visitor(factory); Assert.Equal(HttpStatusCode.OK, (await Send(client, await Created(client))).StatusCode); }
        using var third = await Visitor(factory);
        // The conversation create is also counted per address, so the third visitor is stopped at or before the send.
        var response = await Create(third);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
            response = await Send(third, id);
        }
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task Address_limits_do_not_apply_to_a_signed_in_customer()
    {
        await using var factory = new Factory(new() { ["AiPlatform:AnonymousIpConversationsPerDay"] = "1" });
        await factory.SeedCustomer();
        using (var visitor = await Visitor(factory)) await Created(visitor);
        using var customer = await Visitor(factory);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsJsonAsync("/api/auth/login", new { email = "customer@example.test", password = "PasswordExample123!" })).StatusCode);
        await Token(customer);
        Assert.Equal(HttpStatusCode.Created, (await Create(customer)).StatusCode);
    }

    [Fact]
    public async Task Trusted_proxy_forwarded_address_is_the_rate_limit_subject()
    {
        await using var factory = new Factory(new() { ["TRUSTED_PROXIES"] = "10.0.0.1", ["AiPlatform:AnonymousIpConversationsPerDay"] = "1" });
        using var a1 = await Visitor(factory, "10.0.0.1", "203.0.113.5"); await Created(a1);
        using var b = await Visitor(factory, "10.0.0.1", "203.0.113.9"); await Created(b);
        using var a2 = await Visitor(factory, "10.0.0.1", "203.0.113.5");
        Assert.Equal((HttpStatusCode)429, (await Create(a2)).StatusCode);
    }

    [Fact]
    public async Task Forwarded_address_from_an_untrusted_peer_is_ignored()
    {
        await using var factory = new Factory(new() { ["TRUSTED_PROXIES"] = "10.0.0.1", ["AiPlatform:AnonymousIpConversationsPerDay"] = "1" });
        using var first = await Visitor(factory, "198.51.100.7", "203.0.113.5"); await Created(first);
        using var spoofed = await Visitor(factory, "198.51.100.7", "203.0.113.99");
        Assert.Equal((HttpStatusCode)429, (await Create(spoofed)).StatusCode);
    }

    [Fact]
    public async Task Forwarded_address_is_ignored_when_no_proxy_is_trusted()
    {
        await using var factory = new Factory(new() { ["AiPlatform:AnonymousIpConversationsPerDay"] = "1" });
        using var first = await Visitor(factory, "198.51.100.7", "203.0.113.5"); await Created(first);
        using var spoofed = await Visitor(factory, "198.51.100.7", "203.0.113.99");
        Assert.Equal((HttpStatusCode)429, (await Create(spoofed)).StatusCode);
    }

    [Fact]
    public void Invalid_trusted_proxy_entry_stops_startup()
    {
        using var factory = new Factory(new() { ["TRUSTED_PROXIES"] = "not-an-address" });
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    private static PlatformDatabase NewDatabase() => new(new DbContextOptionsBuilder<PlatformDatabase>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Cleanup_drains_a_backlog_larger_than_one_batch()
    {
        using var db = NewDatabase(); var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 1200; i++) db.Conversations.Add(new PlatformConversation { AssistantId = "website", OwnerHash = "o", CreatedAt = now.AddDays(-9), ExpiresAt = now.AddDays(-2) });
        db.Conversations.Add(new PlatformConversation { AssistantId = "website", OwnerHash = "o", CreatedAt = now, ExpiresAt = now.AddDays(7) });
        await db.SaveChangesAsync();
        var result = await PlatformMaintenance.PurgeAsync(db, 90, now, CancellationToken.None);
        Assert.Equal(1200, result.Conversations);
        Assert.Equal(1, await db.Conversations.CountAsync());
    }

    [Fact]
    public async Task Cleanup_removes_messages_and_turns_of_expired_conversations_only()
    {
        using var db = NewDatabase(); var now = DateTimeOffset.UtcNow;
        var old = new PlatformConversation { AssistantId = "website", OwnerHash = "o", CreatedAt = now.AddDays(-9), ExpiresAt = now.AddDays(-1) };
        var live = new PlatformConversation { AssistantId = "website", OwnerHash = "o", CreatedAt = now, ExpiresAt = now.AddDays(7) };
        db.Conversations.AddRange(old, live);
        db.Messages.AddRange(new PlatformMessage { ConversationId = old.Id, Role = "user", CreatedAt = now }, new PlatformMessage { ConversationId = live.Id, Role = "user", CreatedAt = now });
        db.Turns.AddRange(new PlatformTurn { ConversationId = old.Id, ClientRequestId = "a", CreatedAt = now }, new PlatformTurn { ConversationId = live.Id, ClientRequestId = "b", CreatedAt = now });
        await db.SaveChangesAsync();
        await PlatformMaintenance.PurgeAsync(db, 90, now, CancellationToken.None);
        Assert.Equal(live.Id, (await db.Conversations.SingleAsync()).Id);
        Assert.Equal(live.Id, (await db.Messages.SingleAsync()).ConversationId);
        Assert.Equal(live.Id, (await db.Turns.SingleAsync()).ConversationId);
    }

    [Fact]
    public async Task Cleanup_keeps_a_conversation_whose_lease_is_still_active()
    {
        using var db = NewDatabase(); var now = DateTimeOffset.UtcNow;
        db.Conversations.Add(new PlatformConversation { AssistantId = "website", OwnerHash = "o", CreatedAt = now.AddDays(-9), ExpiresAt = now.AddDays(-1), LeaseOwner = "x", LeaseUntil = now.AddMinutes(1) });
        await db.SaveChangesAsync();
        await PlatformMaintenance.PurgeAsync(db, 90, now, CancellationToken.None);
        Assert.Equal(1, await db.Conversations.CountAsync());
    }

    [Fact]
    public async Task Cleanup_removes_stale_admission_gates_and_keeps_current_ones()
    {
        using var db = NewDatabase(); var now = DateTimeOffset.UtcNow;
        db.Gates.AddRange(
            new PlatformGate { Id = "ip:stale", MinuteStart = now.AddDays(-3), DayStart = now.AddDays(-3) },
            new PlatformGate { Id = "ip:today", MinuteStart = now.AddMinutes(-5), DayStart = now.AddHours(-1) });
        await db.SaveChangesAsync();
        var result = await PlatformMaintenance.PurgeAsync(db, 90, now, CancellationToken.None);
        Assert.Equal(1, result.Gates);
        Assert.Equal("ip:today", (await db.Gates.SingleAsync()).Id);
    }

    [Fact]
    public async Task Cleanup_applies_retention_to_usage_and_audit_rows()
    {
        using var db = NewDatabase(); var now = DateTimeOffset.UtcNow;
        db.Usage.AddRange(new PlatformUsage { AssistantId = "website", CreatedAt = now.AddDays(-100) }, new PlatformUsage { AssistantId = "website", CreatedAt = now });
        db.Audit.AddRange(new PlatformAudit { AssistantId = "website", Operation = "x", CreatedAt = now.AddDays(-100) }, new PlatformAudit { AssistantId = "website", Operation = "y", CreatedAt = now });
        await db.SaveChangesAsync();
        await PlatformMaintenance.PurgeAsync(db, 90, now, CancellationToken.None);
        Assert.Equal(1, await db.Usage.CountAsync());
        Assert.Equal("y", (await db.Audit.SingleAsync()).Operation);
    }
}
