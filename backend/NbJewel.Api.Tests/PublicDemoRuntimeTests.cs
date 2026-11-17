// Role: Verifies public demo startup, core readiness and single-hop forwarding with synthetic stores, never SQL or external providers.
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

public sealed class PublicDemoRuntimeTests
{
    [Fact]
    public async Task Public_demo_rejects_development_sample_start_before_serving()
    {
        await using var factory = new Factory(publicDemo: true);
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
    [Fact]
    public async Task Core_readiness_is_independent_of_missing_optional_AI_rate_and_email_keys()
    {
        await using var factory = new Factory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready/core")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
    [Fact]
    public async Task Missing_storage_fails_readiness_without_leaking_path_or_repairing_it()
    {
        await using var factory = new Factory(missingStorage: true); using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready/core");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(factory.MissingPath, await response.Content.ReadAsStringAsync());
        Assert.False(Directory.Exists(factory.MissingPath));
    }
    [Theory]
    [InlineData("127.0.0.1", "https", "203.0.113.5")]
    [InlineData("198.51.100.2", "http", "198.51.100.2")]
    public async Task Forwarded_scheme_and_client_are_used_only_from_exact_trusted_loopback(string peer, string scheme, string clientIp)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["TRUSTED_PROXIES"] = "127.0.0.1" });
        TrustedProxySetup.Configure(builder.Services);
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(peer); return next(context); });
        app.UseForwardedHeaders();
        app.Run(context => context.Response.WriteAsync(context.Request.Scheme + "|" + context.Connection.RemoteIpAddress));
        await app.StartAsync(); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.1, 203.0.113.5");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "http, https");
        Assert.Equal(scheme + "|" + clientIp, await client.GetStringAsync("/"));
    }
    [Theory]
    [InlineData("http", "/api/auth/csrf", 400)]
    [InlineData("https", "/api/auth/csrf", 204)]
    [InlineData("http", "/health/live", 204)]
    [InlineData("http", "/health/ready/core", 204)]
    public async Task Public_transport_requires_verified_HTTPS_except_narrow_readiness_probes(string scheme, string path, int status)
    {
        var context = new DefaultHttpContext(); context.Request.Method = "GET"; context.Request.Scheme = scheme; context.Request.Path = path;
        await PublicDemoSetup.RequireHttps(context, c => { c.Response.StatusCode = 204; return Task.CompletedTask; });
        Assert.Equal(status, context.Response.StatusCode);
    }
    [Theory]
    [InlineData("Repository:Provider", "Sample")]
    [InlineData("SampleIdentity:Enabled", "true")]
    [InlineData("urls", "http://0.0.0.0:5157")]
    [InlineData("TRUSTED_PROXIES", "127.0.0.0/8")]
    [InlineData("Kestrel:Endpoints:Public:Url", "http://0.0.0.0:80")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("APP_PUBLIC_ORIGIN", "https://evil.example.test")]
    [InlineData("CORS_ORIGINS", "https://evil.example.test")]
    [InlineData("FORWARDEDHEADERS_ENABLED", "true")]
    [InlineData("NBJEWEL_IDENTITY_CONNECTION", "")]
    [InlineData("AI_DATA_PROTECTION_SECRET", "private-invalid-value")]
    [InlineData("ITEM_ASSET_STORAGE_PATH", "relative")]
    public void Public_startup_rejects_unsafe_configuration_without_opening_SQL(string key, string value)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["PublicDemo:Enabled"] = "true", ["SampleIdentity:Enabled"] = "false", ["Repository:Provider"] = "Sql",
            ["urls"] = "http://127.0.0.1:5157", ["TRUSTED_PROXIES"] = "127.0.0.1",
            ["AllowedHosts"] = "nb-jewel.vercel.app;localhost;127.0.0.1", ["APP_PUBLIC_ORIGIN"] = "https://nb-jewel.vercel.app",
            ["CORS_ORIGINS"] = "https://nb-jewel.vercel.app", ["NBJEWEL_SQL_CONNECTION"] = "synthetic-never-opened",
            ["NBJEWEL_IDENTITY_CONNECTION"] = "synthetic-never-opened", ["AI_PLATFORM_CONNECTION"] = "synthetic-never-opened",
            ["AI_DATA_PROTECTION_SECRET"] = Convert.ToBase64String(new byte[32]),
            ["ITEM_ASSET_STORAGE_PATH"] = Path.GetTempPath(), ["BUSINESS_STORAGE_PATH"] = Path.GetTempPath(), ["JEWELRY_STORAGE_PATH"] = Path.GetTempPath()
        });
        PublicDemoSetup.Validate(builder.Configuration, builder.Environment);
        builder.Configuration[key] = value;
        var error = Assert.Throws<InvalidOperationException>(() => PublicDemoSetup.Validate(builder.Configuration, builder.Environment));
        Assert.DoesNotContain("private-invalid-value", error.Message);
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "nbjewel-readiness-" + Guid.NewGuid().ToString("N"));
        private readonly bool publicDemo;
        private readonly bool missingStorage;
        public string MissingPath => Path.Combine(root, "missing-private-storage");
        public Factory(bool publicDemo = false, bool missingStorage = false)
        { this.publicDemo = publicDemo; this.missingStorage = missingStorage; Directory.CreateDirectory(root); }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["PublicDemo:Enabled"] = publicDemo.ToString(), ["Repository:Provider"] = "Sample",
                ["ITEM_ASSET_STORAGE_PATH"] = missingStorage ? MissingPath : root,
                ["BUSINESS_STORAGE_PATH"] = root, ["JEWELRY_STORAGE_PATH"] = root,
                ["GEMINI_API_KEY"] = "", ["GEMINI_ERP_API_KEY"] = "", ["METAL_RATES_API_KEY"] = "", ["SMTP_HOST"] = ""
            }));
            builder.ConfigureTestServices(services =>
            {
                Replace<IdentityDatabase>(services); Replace<PlatformDatabase>(services); Replace<KnowledgeDatabase>(services);
            });
        }
        private void Replace<T>(IServiceCollection services) where T : DbContext
        {
            services.RemoveAll<T>(); services.RemoveAll<DbContextOptions<T>>(); services.RemoveAll<IDbContextOptionsConfiguration<T>>();
            services.AddDbContext<T>(o => o.UseInMemoryDatabase(root + typeof(T).Name));
        }
    }
}
