// Role: Exercises the opt-in local sample identity through the real provider, password hashing,
// session cookie, permission and CSRF pipeline without replacing the production identity store.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using NbJewel.Api.Identity;
namespace NbJewel.Api.Tests;

public sealed class SampleIdentityRuntimeTests
{
    private const string Email = "sample.staff@nbjewel.invalid";
    private const string Password = "OnlySyntheticTest!12345";
    private static async Task Token(HttpClient client)
    {
        var value = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", value.GetProperty("token").GetString());
    }
    [Fact]
    public async Task ExplicitSampleStaffUsesRealPasswordCookieGrantsAndCsrf()
    {
        await using var factory = new SampleRuntimeFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/items/7")).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = "WrongPassword!123" })).StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), x => x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var access = await client.GetFromJsonAsync<AccountAccess>("/api/auth/me");
        Assert.Equal("erp", access!.AccountType); Assert.True(access.AllStores);
        Assert.Contains("items.write", access.Permissions); Assert.Contains("knowledge.manage", access.Permissions);
        Assert.DoesNotContain("chat.read", access.Permissions); Assert.DoesNotContain("jewelry.manage", access.Permissions);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/items/7")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/items/7/status", new { status = "Complete" })).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/items/7/status", new { status = "Complete" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(Email);
        Assert.NotNull(user); Assert.NotEqual(Password, user.PasswordHash); Assert.True(user.EmailConfirmed);
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Database.ProviderName);
    }
    [Fact]
    public void SampleStartupRequiresExplicitStrongRuntimePassword()
    {
        foreach (var password in new[] { "", "weak" })
        {
            using var factory = new SampleRuntimeFactory { PasswordOverride = password };
            Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        }
    }
    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("http://localhost")]
    public async Task SampleModeRejectsNonlocalHostsAndNonloopbackPeers(string address)
    {
        await using var factory = new SampleRuntimeFactory { Peer = address.EndsWith("localhost") ? "10.0.0.2" : "127.0.0.1" };
        using var client = factory.CreateClient(new() { BaseAddress = new Uri(address) });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/csrf")).StatusCode);
    }
    [Fact]
    public async Task DefaultModeDoesNotSeedOrEnableSampleStaff()
    {
        await using var factory = new SampleRuntimeFactory { Enabled = false }; using var client = factory.CreateClient();
        await Token(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password })).StatusCode);
    }
    [Theory]
    [InlineData("Production", "Repository:Provider", "Sample")]
    [InlineData("Development", "Repository:Provider", "Sql")]
    [InlineData("Development", "NBJEWEL_IDENTITY_CONNECTION", "unused")]
    [InlineData("Development", "AI_PLATFORM_CONNECTION", "unused")]
    [InlineData("Development", "ConnectionStrings:Other", "unused")]
    [InlineData("Development", "urls", "http://0.0.0.0:5155")]
    [InlineData("Development", "ASPNETCORE_URLS", "http://*:5155")]
    [InlineData("Development", "Kestrel:Endpoints:Public:Url", "http://localhost:9999")]
    [InlineData("Development", "TRUSTED_PROXIES", "127.0.0.1")]
    public void SamplePolicyRejectsUnsafeHostingAndConnectionSettings(string environment, string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["SampleIdentity:Enabled"] = "true", ["SampleIdentity:StaffPassword"] = Password,
            ["Repository:Provider"] = "Sample", [key] = value
        }).Build();
        Assert.Throws<InvalidOperationException>(() => SampleIdentityOptions.Read(configuration, new HostingEnvironment { EnvironmentName = environment }));
    }

    private sealed class SampleRuntimeFactory : WebApplicationFactory<Program>
    {
        public string PasswordOverride { get; init; } = Password;
        public bool Enabled { get; init; } = true;
        public string Peer { get; init; } = "127.0.0.1";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var path = Path.Combine(Path.GetTempPath(), "nbjewel-sample-runtime-" + Guid.NewGuid().ToString("N"));
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["SampleIdentity:Enabled"] = Enabled.ToString(), ["SampleIdentity:StaffPassword"] = PasswordOverride,
                ["NBJEWEL_IDENTITY_CONNECTION"] = "", ["AI_PLATFORM_CONNECTION"] = "", ["NBJEWEL_SQL_CONNECTION"] = "",
                ["Repository:Provider"] = "Sample", ["ITEM_ASSET_STORAGE_PATH"] = path, ["JEWELRY_STORAGE_PATH"] = Path.Combine(path, "legacy")
            }));
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new PeerFilter(Peer)));
        }
    }
    private sealed class PeerFilter(string peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
            app.Use((context, nextMiddleware) => { context.Connection.RemoteIpAddress = IPAddress.Parse(peer); return nextMiddleware(context); }); next(app);
        };
    }
}
