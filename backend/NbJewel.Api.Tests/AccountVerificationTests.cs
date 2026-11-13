// Role: Exercises verification-required registration through the actual Identity store.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NbJewel.Api.Identity;
namespace NbJewel.Api.Tests;
public class AccountVerificationTests
{
    [Fact]
    public async Task RegistrationRequiresVerificationBeforeLogin()
    {
        await using var baseFactory = new IdentityTestFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["AiSecurity:RequireEmailVerification"] = "true" }));
            builder.ConfigureTestServices(s => { s.RemoveAll<IAccountVerificationSender>(); s.AddSingleton<IAccountVerificationSender>(new FakeSender()); });
        });
        using var client = factory.CreateClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var credentials = new { email = "verify@example.test", password = "PasswordExample123!", accountType = "erp" };
        var registration = await client.PostAsJsonAsync("/api/auth/register", credentials);
        Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", credentials)).StatusCode);
        var sender = factory.Services.GetRequiredService<IAccountVerificationSender>() as FakeSender;
        Assert.NotNull(sender);
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = await manager.FindByEmailAsync("verify@example.test");
            Assert.NotNull(account);
            Assert.Equal("customer", account.AccountType);
            Assert.False(account.AllStores);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/confirm-email", new { sender.UserId, sender.Token })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", credentials)).StatusCode);
    }
    [Fact]
    public async Task MissingSenderRejectsRegistrationBeforeAccountCreation()
    {
        await using var baseFactory = new IdentityTestFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["AiSecurity:RequireEmailVerification"] = "true" }));
            builder.ConfigureTestServices(s => { s.RemoveAll<IAccountVerificationSender>(); s.AddSingleton<IAccountVerificationSender>(new UnavailableSender()); });
        });
        using var client = factory.CreateClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/auth/register", new { email = "new@example.test", password = "PasswordExample123!" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("new@example.test"));
    }
    private sealed class UnavailableSender : IAccountVerificationSender
    {
        public bool IsConfigured => false;
        public Task SendAsync(string email, string userId, string token, CancellationToken ct) => throw new InvalidOperationException();
    }
    [Fact]
    public async Task UnknownLoginRunsDummyPasswordVerification()
    {
        var work = new CountingWork();
        await using var baseFactory = new IdentityTestFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(s => {
            s.RemoveAll<IPasswordWork>(); s.AddSingleton<IPasswordWork>(work);
        }));
        using var client = factory.CreateClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "PasswordExample123!" })).StatusCode);
        Assert.Equal(1, work.Calls);
    }
    private sealed class CountingWork : IPasswordWork
    {
        public int Calls;
        public void VerifyDummy(string password) => Calls++;
    }
    private sealed class FakeSender : IAccountVerificationSender
    {
        public bool IsConfigured => true;
        public string UserId = "";
        public string Token = "";
        public Task SendAsync(string email, string userId, string token, CancellationToken ct) { UserId = userId; Token = token; return Task.CompletedTask; }
    }
}
