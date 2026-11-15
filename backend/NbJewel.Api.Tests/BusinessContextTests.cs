// Role: Verifies assumed trusted business resolution, startup migration and SQL-safe membership behavior.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;

namespace NbJewel.Api.Tests;

public sealed class BusinessContextTests
{
    [Fact]
    public async Task MembershipIsAuthoritativeAndSlugCannotOverrideIt()
    {
        await using var factory = new IdentityTestFactory();
        var id = await factory.Seed("erp", true, []);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        var row = await db.Set<BusinessMembership>().SingleAsync();
        row.BusinessId = "sample-wholesale"; row.Role = "owner"; await db.SaveChangesAsync();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test")) };
        http.Request.RouteValues["slug"] = "nb-jewel-demo";
        var context = scope.ServiceProvider.GetRequiredService<BusinessContext>();
        Assert.False(await context.ResolveErpAsync(http, default)); Assert.Null(context.Business);
        http.Request.RouteValues["slug"] = "sample-wholesale";
        Assert.True(await context.ResolveErpAsync(http, default));
        Assert.Equal("sample-wholesale", context.Business!.Slug);
        Assert.True(context.Access!.AllStores);
        Assert.Contains("portal.publish", context.Access.Permissions);
        Assert.Equal("sample-wholesale", context.Membership!.Slug);
        await Assert.ThrowsAsync<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<BusinessMemberships>().AssignAsync(id, "nb-jewel-demo", "owner", default));
    }
    [Fact]
    public async Task AnonymousClaimsAndRevokedMembershipCannotReuseAnEarlierResolution()
    {
        await using var factory = new IdentityTestFactory();
        var id = await factory.Seed("erp", true, []);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BusinessContext>();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test")) };
        Assert.True(await context.ResolveErpAsync(http, default));
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)]));
        Assert.False(await context.ResolveErpAsync(http, default));
        Assert.Null(context.Business); Assert.Null(context.Access);
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        db.Remove(await db.Set<BusinessMembership>().SingleAsync()); await db.SaveChangesAsync();
        Assert.False(await context.ResolveErpAsync(http, default)); Assert.Null(context.Business);
    }
    [Fact]
    public async Task StartupMigrationAssignsExistingStaffOnceButNeverCustomers()
    {
        await using var factory = new IdentityTestFactory();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var type in new[] { "erp", "customer" })
            Assert.True((await users.CreateAsync(new ApplicationUser { UserName = type + "@example.test", Email = type + "@example.test", AccountType = type, AllStores = true }, "PasswordExample123!")).Succeeded);
        var memberships = scope.ServiceProvider.GetRequiredService<BusinessMemberships>();
        await memberships.MigrateAsync(default); await memberships.MigrateAsync(default);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        var member = Assert.Single(await db.Set<BusinessMembership>().ToListAsync());
        Assert.Equal("nb-jewel-demo", member.BusinessId); Assert.Equal("owner", member.Role);
        Assert.Equal("erp", (await users.FindByIdAsync(member.UserId))!.AccountType);
    }
    [Theory]
    [InlineData("Production", "Sample", false)]
    [InlineData("Development", "Sql", false)]
    [InlineData("Development", "Sample", true)]
    public void ProvisioningIsRestrictedToDevelopmentSample(string environment, string provider, bool allowed)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Repository:Provider"] = provider }).Build();
        Assert.Equal(allowed, ProvisioningCommand.Allowed(config, new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = environment }));
    }
    [Fact]
    public async Task SqlMembershipIsImplicitAndNeedsNoNewTableOrConnection()
    {
        await using var factory = new IdentityTestFactory();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Repository:Provider"] = "Sql" }).Build();
        using var db = new IdentityDatabase(new DbContextOptionsBuilder<IdentityDatabase>().UseSqlServer("Server=unused.invalid;Database=model_only;Integrated Security=True").Options);
        var registry = factory.Services.GetRequiredService<BusinessRegistry>();
        using var services = new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config)
            .AddSingleton(registry).AddScoped<BusinessMemberships>().AddScoped<BusinessContext>()
            .AddScoped<IPermissionAdapter, DatabasePermissionAdapter>().AddSingleton(new IdentityAvailability(true))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var sqlScope = services.CreateScope();
        Assert.NotNull(sqlScope.ServiceProvider.GetRequiredService<BusinessContext>());
        var members = sqlScope.ServiceProvider.GetRequiredService<BusinessMemberships>();
        Assert.Equal("owner", (await members.ResolveAsync(new("u", "", "erp", [], [], true), default))!.Role);
        Assert.Equal("staff", (await members.ResolveAsync(new("u", "", "erp", [], [1], false), default))!.Role);
        Assert.Null(await members.ResolveAsync(new("u", "", "customer", [], [], true), default));
        await members.MigrateAsync(default);
        Assert.Null(db.Model.FindEntityType(typeof(BusinessMembership)));
        Assert.DoesNotContain("BusinessMembership", db.Database.GenerateCreateScript());
    }
}
