// Role: Exercises real Identity cookies and mutable database grants using an isolated in-memory store.
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
using NbJewel.Api.Jewelry;

namespace NbJewel.Api.Tests;

public sealed class IdentitySessionTests
{
    private static readonly string[] AllReads = ["items.read", "prices.read", "stock.read", "status.read", "reference.read", "chat.read"];

    [Fact]
    public async Task RegistrationIgnoresForgedPrivilegeFieldsAndLogoutClearsSession()
    {
        await using var factory = new IdentityTestFactory();
        using var client = factory.CreateClient();
        await Token(client);
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "new@example.test", password = "PasswordExample123!", accountType = "erp", allStores = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<AccountAccess>("/api/auth/me");
        Assert.Equal("customer", me!.AccountType);
        Assert.Empty(me.Permissions);
        Assert.False(me.AllStores);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/items")).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("customer", true)]
    [InlineData("erp", false)]
    public async Task CustomerIsDeniedAndNarrowScopeReceivesOnlyAuthorizedStores(string accountType, bool allStores)
    {
        await using var factory = new IdentityTestFactory();
        await factory.Seed(accountType, allStores, AllReads);
        using var client = factory.CreateClient();
        await Login(client);
        foreach (var path in new[] { "/api/items", "/api/items/1/stock", "/api/stock/low", "/api/stores" })
            Assert.Equal(accountType == "customer" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        if (accountType == "erp")
        {
            var stock = await client.GetFromJsonAsync<List<NbJewel.Api.Models.StockRecord>>("/api/items/1/stock");
            Assert.All(stock!, row => Assert.Equal(1, row.StoreId));
            var stores = await client.GetFromJsonAsync<List<NbJewel.Api.Models.Store>>("/api/stores");
            Assert.Equal(1, Assert.Single(stores!).Id);
        }
        else Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/chat", new { question = "show stock" })).StatusCode);
    }

    [Fact]
    public async Task PermissionsAreCheckedPerActionAndRevocationAppliesToExistingCookie()
    {
        await using var factory = new IdentityTestFactory();
        var id = await factory.Seed("erp", true, ["items.read", "prices.read", "status.read", "reference.read"]);
        using var client = factory.CreateClient();
        await Login(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/items")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/items/1/stock")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/items/1/stock", new { quantity = 1, storeId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync("/api/items/1/status", new { status = "Active" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/chat", new { question = "show stock" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        db.PermissionGrants.RemoveRange(db.PermissionGrants.Where(x => x.UserId == id && x.Permission == "prices.read"));
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/items")).StatusCode);
    }

    [Fact]
    public async Task CookieWritesRequireCurrentValidCsrfToken()
    {
        await using var factory = new IdentityTestFactory();
        await factory.Seed("erp", true, AllReads.Concat(["items.write", "stock.write", "status.write"]).ToArray());
        using var client = factory.CreateClient();
        await Login(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/items/1/status", new { status = "Active" })).StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "wrong");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
    }

    [Fact]
    public async Task InactiveImageIsHiddenEvenWhenFilenameIsKnown()
    {
        await using var factory = new IdentityTestFactory();
        var repo = factory.Services.GetRequiredService<IJewelryRepository>();
        var item = (await repo.ListAsync(true, default))[0];
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/jewelry-images/" + item.File)).StatusCode);
        await repo.SetActiveAsync(item.Id, false, default);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/jewelry-images/" + item.File)).StatusCode);
    }

    private static async Task Login(HttpClient client)
    {
        await Token(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "erp@example.test", password = "PasswordExample123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        await Token(client);
    }

    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
}

public sealed class IdentityTestFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NBJEWEL_IDENTITY_CONNECTION"] = "",
            ["Repository:Provider"] = "Sample",
            ["JEWELRY_STORAGE_PATH"] = Path.Combine(Path.GetTempPath(), "nbjewel-identity-" + databaseName)
        }));
        builder.ConfigureTestServices(services =>
        {
            // Minimal-host setup can capture configuration before test overrides apply.
            // Replace storage itself so publication tests never mutate shared app data.
            services.RemoveAll<JewelryStorageOptions>();
            services.AddSingleton(new JewelryStorageOptions(
                Path.Combine(Path.GetTempPath(), "nbjewel-isolated-images-" + databaseName),
                Path.Combine(SampleRepositoryFactory.FindRoot(), "sample-data", "jewelry")));
            services.RemoveAll<DbContextOptions<IdentityDatabase>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IdentityDatabase>>();
            services.AddDbContext<IdentityDatabase>(options => options.UseInMemoryDatabase(databaseName));
            services.RemoveAll<IdentityAvailability>();
            services.AddSingleton(new IdentityAvailability(true));
        });
    }

    public async Task<string> Seed(string accountType, bool allStores, string[] permissions)
    {
        using var scope = Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Email = "erp@example.test", UserName = "erp@example.test", AccountType = accountType, AllStores = allStores };
        var result = await manager.CreateAsync(user, "PasswordExample123!");
        Assert.True(result.Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        db.PermissionGrants.AddRange(permissions.Select(p => new PermissionGrant { UserId = user.Id, Permission = p }));
        db.StoreGrants.Add(new StoreGrant { UserId = user.Id, StoreId = 1 });
        await db.SaveChangesAsync();
        return user.Id;
    }
}
