// Role: Tests owner bootstrap against isolated Identity data with synthetic configuration and no SQL connection.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NbJewel.Api.Identity;
using NbJewel.Api.Businesses;
namespace NbJewel.Api.Tests;

public sealed class OwnerCredentialCommandTests
{
    private const string Password = "SyntheticOwnerPassword123!";
    private static WebApplication App(string provider = "Sql", string environment = "Development", bool sample = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Repository:Provider"] = provider, ["SampleIdentity:Enabled"] = sample.ToString(),
            ["NBJEWEL_IDENTITY_CONNECTION"] = "synthetic-not-opened", ["NBJEWEL_BOOTSTRAP_EMAIL"] = "owner@example.test",
            ["NBJEWEL_BOOTSTRAP_PASSWORD"] = Password
        });
        builder.Services.AddSingleton(new IdentityAvailability(true));
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<IdentityDatabase>(o => o.UseInMemoryDatabase(databaseName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddIdentityCore<ApplicationUser>(o => o.Password.RequiredLength = 12).AddEntityFrameworkStores<IdentityDatabase>().AddDefaultTokenProviders();
        return builder.Build();
    }
    [Fact]
    public async Task Creates_one_hashed_owner_and_repeat_never_changes_password_or_grants()
    {
        await using var app = App();
        Assert.Equal(0, await IdentitySetup.BootstrapAsync(app));
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("owner@example.test");
        Assert.NotNull(user); Assert.Equal("erp", user.AccountType); Assert.True(user.AllStores);
        Assert.NotEqual(Password, user.PasswordHash); Assert.True(await users.CheckPasswordAsync(user, Password));
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        Assert.Contains(await db.PermissionGrants.ToListAsync(), x => x.Permission == "portal.publish");
        var hash = user.PasswordHash;
        app.Configuration["NBJEWEL_BOOTSTRAP_PASSWORD"] = "AnotherSyntheticPassword123!";
        Assert.Equal(1, await IdentitySetup.BootstrapAsync(app));
        await db.Entry(user).ReloadAsync(); Assert.Equal(hash, user.PasswordHash);
        Assert.Single(await db.Users.ToListAsync());
    }
    [Fact]
    public async Task Local_reset_replaces_only_existing_owner_password_and_security_stamp()
    {
        await using var app = App();
        Assert.Equal(0, await IdentitySetup.BootstrapAsync(app));
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        var user = (await users.FindByEmailAsync("owner@example.test"))!;
        var stamp = user.SecurityStamp;
        var grants = await db.PermissionGrants.CountAsync();
        app.Configuration["NBJEWEL_BOOTSTRAP_PASSWORD"] = "ReplacementSynthetic123!";
        Assert.Equal(0, await IdentitySetup.ResetOwnerPasswordAsync(app));
        await db.Entry(user).ReloadAsync();
        Assert.False(await users.CheckPasswordAsync(user, Password));
        Assert.True(await users.CheckPasswordAsync(user, "ReplacementSynthetic123!"));
        Assert.NotEqual(stamp, user.SecurityStamp); Assert.Equal(grants, await db.PermissionGrants.CountAsync());
    }
    [Theory]
    [InlineData("customer", false)]
    [InlineData("erp", false)]
    [InlineData("missing", false)]
    public async Task Reset_refuses_customer_staff_and_missing_account(string type, bool allStores)
    {
        await using var app = App();
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (type != "missing")
            Assert.True((await users.CreateAsync(new ApplicationUser { Email = "owner@example.test", UserName = "owner@example.test", AccountType = type, AllStores = allStores }, Password)).Succeeded);
        Assert.Equal(1, await IdentitySetup.ResetOwnerPasswordAsync(app));
        var user = await users.FindByEmailAsync("owner@example.test");
        if (type == "missing") Assert.Null(user);
        else { Assert.NotNull(user); Assert.True(await users.CheckPasswordAsync(user, Password)); Assert.False(user.AllStores); }
    }
    [Theory]
    [InlineData("Sample", "Development", false)]
    [InlineData("Sql", "Production", false)]
    [InlineData("Sql", "Development", true)]
    public async Task Refuses_unsafe_modes_before_creating_accounts(string provider, string environment, bool sample)
    {
        await using var app = App(provider, environment, sample);
        Assert.Equal(1, await IdentitySetup.BootstrapAsync(app));
        Assert.Equal(1, await IdentitySetup.ResetOwnerPasswordAsync(app));
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Users.ToListAsync());
    }
    [Theory]
    [InlineData("customer", false)]
    [InlineData("erp", false)]
    public async Task Existing_customer_or_staff_is_never_elevated(string type, bool allStores)
    {
        await using var app = App();
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Email = "owner@example.test", UserName = "owner@example.test", AccountType = type, AllStores = allStores };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.Equal(1, await IdentitySetup.BootstrapAsync(app));
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        await db.Entry(user).ReloadAsync();Assert.Equal(type,user.AccountType);Assert.False(user.AllStores);
        Assert.Empty(await db.PermissionGrants.ToListAsync());
    }
}
