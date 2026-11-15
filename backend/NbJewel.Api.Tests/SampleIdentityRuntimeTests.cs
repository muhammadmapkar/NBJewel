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
using Microsoft.EntityFrameworkCore;
namespace NbJewel.Api.Tests;

public sealed class SampleIdentityRuntimeTests
{
    [Fact]
    public async Task ExplicitCustomerOptInCreatesCustomerWithoutErpGrants()
    {
        await using var factory = new SampleRuntimeFactory { AllowCustomers = true };
        using var client = factory.CreateClient(); await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register", new { email = "customer@sample.invalid", password = Password })).StatusCode);
        var access = await client.GetFromJsonAsync<AccountAccess>("/api/auth/me");
        Assert.Equal("customer", access!.AccountType); Assert.Empty(access.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/items/7")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/customer/profile")).StatusCode);
    }

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
        Assert.Contains("chat.read", access.Permissions); Assert.Contains("jewelry.manage", access.Permissions);
        Assert.Contains("portal.manage", access.Permissions); Assert.Contains("portal.publish", access.Permissions);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/items/7")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/items/7/status", new { status = "Complete" })).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/items/7/status", new { status = "Complete" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(Email);
        Assert.NotNull(user); Assert.NotEqual(Password, user.PasswordHash); Assert.True(user.EmailConfirmed);
        var stored = await scope.ServiceProvider.GetRequiredService<IPermissionAdapter>().ResolveAsync(user.Id, default);
        Assert.DoesNotContain("chat.read", stored!.Permissions); Assert.DoesNotContain("jewelry.manage", stored.Permissions);
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Database.ProviderName);
    }
    [Fact]
    public async Task AHostCanBootstrapForProvisioningBeforeServingWithoutCreatingAnotherStaffAccount()
    {
        await using var factory = new SampleRuntimeFactory();
        using var client = factory.CreateClient();
        var bootstrap = Assert.Single(factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<SampleIdentityBootstrap>());
        await bootstrap.StartAsync(default);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Users.CountAsync());
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
    [Fact]
    public async Task SampleModeCannotCreateAdditionalCustomerAccounts()
    {
        await using var factory = new SampleRuntimeFactory(); using var client = factory.CreateClient(); await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/register", new { email = "another@sample.invalid", password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Single(scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Users);
    }
    [Fact]
    public async Task FiveWrongPasswordsLockOutEvenTheCorrectSamplePassword()
    {
        await using var factory = new SampleRuntimeFactory(); using var client = factory.CreateClient(); await Token(client);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = "WrongPassword!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().IsLockedOutAsync((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(Email))!));
    }
    [Fact]
    public async Task RestartRejectsPriorCookieAndPriorGeneratedPassword()
    {
        string cookie;
        await using (var first = new SampleRuntimeFactory())
        {
            using var client = first.CreateClient(); await Token(client);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nbjewel.session=", StringComparison.Ordinal)).Split(';')[0];
        }
        const string nextPassword = "AnotherSyntheticTest!45678";
        await using var restarted = new SampleRuntimeFactory { PasswordOverride = nextPassword }; using var next = restarted.CreateClient();
        next.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await next.GetAsync("/api/auth/me")).StatusCode);
        next.DefaultRequestHeaders.Remove("Cookie"); await Token(next);
        Assert.Equal(HttpStatusCode.Unauthorized, (await next.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await next.PostAsJsonAsync("/api/auth/login", new { email = Email, password = nextPassword })).StatusCode);
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
        public bool AllowCustomers { get; init; }
        public bool Enabled { get; init; } = true;
        public string Peer { get; init; } = "127.0.0.1";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var path = Path.Combine(Path.GetTempPath(), "nbjewel-sample-runtime-" + Guid.NewGuid().ToString("N"));
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["SampleIdentity:AllowCustomers"] = AllowCustomers.ToString(), ["SampleIdentity:Enabled"] = Enabled.ToString(), ["SampleIdentity:StaffPassword"] = PasswordOverride,
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
