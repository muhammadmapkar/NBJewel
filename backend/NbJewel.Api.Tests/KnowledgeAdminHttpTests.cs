// Role: Tests knowledge administration through real sessions, permissions, and CSRF protection.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NbJewel.Api.Identity;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Platform;
namespace NbJewel.Api.Tests;

public sealed class KnowledgeAdminHttpTests
{
    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
    private static async Task Login(HttpClient client)
    {
        await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "PasswordExample123!" })).StatusCode);
        await Token(client);
    }
    private static object Input(bool confirm = true) => new { title = "Guide", text = "emerald", collection = "public", audience = "public", confirm };
    [Fact]
    public async Task AnonymousAndInsufficientAccessCannotAdminister()
    {
        await using var anonymous = new KnowledgeHttpFactory();
        using var a = anonymous.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/api/ai/knowledge/documents")).StatusCode);
        await Token(a);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.PostAsJsonAsync("/api/ai/knowledge/documents", new { title = "a", text = "x", collection = "public", audience = "public", confirm = true, assistantId = "website" })).StatusCode);
        foreach (var config in new[] { ("customer", true, new[] { "knowledge.manage" }), ("erp", false, new[] { "knowledge.manage" }), ("erp", true, Array.Empty<string>()) })
        {
            await using var factory = new KnowledgeHttpFactory();
            await factory.Seed(config.Item1, config.Item2, config.Item3);
            using var client = factory.CreateClient(); await Login(client);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/ai/knowledge/documents")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/ai/knowledge/products/1/publish", new { confirm = true, assistantId = "website" })).StatusCode);
        }
    }
    [Fact]
    public async Task MissingProviderAndRevokedGrantFailClosed()
    {
        await using var factory = new KnowledgeHttpFactory { DisableKnowledgeProvider = true };
        await factory.Seed("erp", true, ["knowledge.manage"]);
        using var client = factory.CreateClient(); await Login(client);
        var ready = await PreparedStorefrontAsset.Create(factory, 7);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/ai/knowledge/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/ai/knowledge/products/7/publish", new { confirm = true, assetRevisionId = ready.RevisionId })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        db.PermissionGrants.RemoveRange(db.PermissionGrants); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/ai/knowledge/documents")).StatusCode);
    }
    [Fact]
    public async Task AdminMutationsRequireCsrfAndConfirmationAndCanPublishRevoke()
    {
        await using var factory = new KnowledgeHttpFactory(); await factory.Seed("erp", true, ["knowledge.manage"]);
        using var client = factory.CreateClient(); await Login(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai/knowledge/documents", Input())).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai/knowledge/documents", Input(false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai/knowledge/documents", new { title = (string?)null, text = (string?)null, collection = "public", audience = "public", confirm = true })).StatusCode);
        var asset = await PreparedStorefrontAsset.Create(factory, 7);
        var create = await client.PostAsJsonAsync("/api/ai/knowledge/documents", Input()); Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/ai/knowledge/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/ai/knowledge/products/2147483647/publish", new { confirm = true })).StatusCode);
        foreach (var action in new[] { "publish", "revoke" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{id}/{action}", new { confirm = false })).StatusCode);
            string? versionId;
            using (var versionScope = factory.Services.CreateScope()) versionId = (await versionScope.ServiceProvider.GetRequiredService<KnowledgeDatabase>().Documents.AsNoTracking().SingleAsync(x => x.Id == id)).LatestVersionId;
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{id}/{action}", new { confirm = true, versionId })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ai/knowledge/products/7/{action}", new { confirm = true, assetRevisionId = asset.RevisionId })).StatusCode);
        }
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeDatabase>();
        var audits = await database.Audits.ToListAsync();
        Assert.Contains(audits, x => x.Operation == "document.publish" && x.TargetId == id && x.Outcome == "confirmed");
        Assert.Contains(audits, x => x.Operation == "product.publish" && x.TargetId == "7");
        Assert.All(audits, audit => { Assert.Equal(64, audit.ActorHash.Length); Assert.DoesNotContain("admin@example.test", JsonSerializer.Serialize(audit)); Assert.DoesNotContain("emerald", JsonSerializer.Serialize(audit)); });
    }
}
public sealed class KnowledgeHttpFactory : WebApplicationFactory<Program>
{
    private readonly string name = Guid.NewGuid().ToString();
    public bool DisableKnowledgeProvider { get; init; }
    public bool UseFilePublications { get; init; }
    public Dictionary<string, string?> TestConfiguration { get; init; } = [];
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["NBJEWEL_IDENTITY_CONNECTION"] = "", ["AI_PLATFORM_CONNECTION"] = "", ["Repository:Provider"] = "Sample", ["ITEM_ASSET_STORAGE_PATH"] = Path.Combine(Path.GetTempPath(), "nbjewel-http-assets-" + name) }).AddInMemoryCollection(TestConfiguration));
        builder.ConfigureTestServices(services => {
            if (!UseFilePublications) services.RemoveAll<IProductPublicationStore>();
            services.RemoveAll<DbContextOptions<IdentityDatabase>>(); services.RemoveAll<IDbContextOptionsConfiguration<IdentityDatabase>>();
            services.AddDbContext<IdentityDatabase>(o => o.UseInMemoryDatabase(name + "identity"));
            services.RemoveAll<DbContextOptions<KnowledgeDatabase>>(); services.RemoveAll<IDbContextOptionsConfiguration<KnowledgeDatabase>>();
            services.AddDbContext<KnowledgeDatabase>(o => { if (!DisableKnowledgeProvider) o.UseInMemoryDatabase(name + "knowledge"); });
            services.RemoveAll<DbContextOptions<PlatformDatabase>>(); services.RemoveAll<IDbContextOptionsConfiguration<PlatformDatabase>>();
            services.AddDbContext<PlatformDatabase>(o => o.UseInMemoryDatabase(name + "platform"));
            services.RemoveAll<IdentityAvailability>(); services.AddSingleton(new IdentityAvailability(true));
        });
    }
    public async Task Seed(string type, bool allStores, string[] permissions)
    {
        using var scope = Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Email = "admin@example.test", UserName = "admin@example.test", AccountType = type, AllStores = allStores };
        Assert.True((await manager.CreateAsync(user, "PasswordExample123!")).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        db.PermissionGrants.AddRange(permissions.Select(p => new PermissionGrant { UserId = user.Id, Permission = p }));
        db.StoreGrants.Add(new StoreGrant { UserId = user.Id, StoreId = 1 }); await db.SaveChangesAsync();
    }
}
