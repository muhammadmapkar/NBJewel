// Role: Proves ERP access needs a business membership and customer identity cannot acquire one.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Identity;

namespace NbJewel.Api.Tests;

public sealed class BusinessMembershipHttpTests
{
    [Theory]
    [InlineData("/api/items")]
    [InlineData("/api/ai/knowledge/products/7")]
    public async Task UnassignedStaffCannotUseGlobalGrants(string path)
    {
        await using var factory = new IdentityTestFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Email = "erp@example.test", UserName = "erp@example.test", AccountType = "erp", AllStores = true };
            Assert.True((await users.CreateAsync(user, "PasswordExample123!")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
            db.PermissionGrants.AddRange(new[] { "items.read", "prices.read", "status.read", "knowledge.manage" }.Select(p => new PermissionGrant { UserId = user.Id, Permission = p }));
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await Login(client);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Classic", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AssignedStaffMeIncludesTrustedBusinessAndImporterReadsRemainAvailable()
    {
        await using var factory = new IdentityTestFactory();
        // Existing fixture staff is explicitly assigned by the fixture, like startup migration.
        await factory.Seed("erp", false, ["items.read", "prices.read", "status.read", "reference.read"]);
        using var client = factory.CreateClient();
        await Login(client);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("nb-jewel-demo", me.GetProperty("business").GetProperty("slug").GetString());
        Assert.Equal("staff", me.GetProperty("business").GetProperty("role").GetString());
        foreach (var path in new[] { "/api/items/storage", "/api/items", "/api/categories", "/api/designs", "/api/stores" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task CustomerHasNoBusinessAndCannotProvisionViaHttp()
    {
        await using var factory = new IdentityTestFactory();
        await factory.Seed("customer", true, ["items.read", "prices.read", "status.read"]);
        using var client = factory.CreateClient();
        await Login(client);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.False(me.TryGetProperty("business", out var business) && business.ValueKind != JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/items")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/businesses", new { slug = "forged", owner = true });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    internal static async Task Login(HttpClient client)
    {
        await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "erp@example.test", password = "PasswordExample123!" })).StatusCode);
        await Token(client);
    }
    internal static async Task Token(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}
