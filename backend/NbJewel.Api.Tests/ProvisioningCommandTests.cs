// Role: Proves assumed host-only provisioning validates ownership and emits credentials only to its terminal.
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NbJewel.Api.Tests;

public sealed class ProvisioningCommandTests
{
    [Theory]
    [InlineData("api")]
    [InlineData("preview")]
    [InlineData("assets")]
    [InlineData("UPPER")]
    [InlineData("../escape")]
    [InlineData("nb-jewel-demo")]
    public async Task ReservedMalformedAndDuplicateSlugsAreRejected(string slug)
    {
        await using var factory = new IdentityTestFactory();
        using var scope = factory.Services.CreateScope();
        var command = scope.ServiceProvider.GetRequiredService<ProvisioningCommand>();
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(1, await command.RunAsync(Args(slug), output, error, default));
        Assert.Empty(output.ToString());
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Users.CountAsync());
    }
    [Fact]
    public async Task ASecondMembershipCannotBeProvisionedAndCustomerCannotBeElevated()
    {
        foreach (var type in new[] { "erp", "customer" })
        {
            await using var factory = new IdentityTestFactory();
            await factory.Seed(type, true, []);
            using var scope = factory.Services.CreateScope();
            var command = scope.ServiceProvider.GetRequiredService<ProvisioningCommand>();
            var output = new StringWriter();
            Assert.Equal(1, await command.RunAsync(Args("another-shop", "erp@example.test"), output, new StringWriter(), default));
            Assert.Empty(output.ToString());
            Assert.Null(scope.ServiceProvider.GetRequiredService<BusinessRegistry>().Find("another-shop"));
        }
    }
    [Fact]
    public async Task CreatesOneOwnerWithGeneratedPasswordWithoutPersistingPlaintext()
    {
        await using var factory = new IdentityTestFactory();
        using var scope = factory.Services.CreateScope();
        var output = new StringWriter();
        var command = scope.ServiceProvider.GetRequiredService<ProvisioningCommand>();
        Assert.Equal(0, await command.RunAsync(Args("new-shop"), output, new StringWriter(), default));
        var line = Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), x => x.StartsWith("Temporary password: "));
        var password = line["Temporary password: ".Length..].Trim();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("new@example.test");
        Assert.True(await users.CheckPasswordAsync(user!, password));
        var member = await scope.ServiceProvider.GetRequiredService<BusinessMemberships>().ResolveAsync(new(user!.Id, user.Email!, user.AccountType, [], [], user.AllStores), default);
        Assert.Equal("new-shop", member!.Slug); Assert.Equal("owner", member.Role);
        Assert.DoesNotContain(password, user.PasswordHash!);
        var root = factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["BUSINESS_STORAGE_PATH"]!;
        foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) Assert.DoesNotContain(password, await File.ReadAllTextAsync(path));
        Assert.Equal(1, await command.RunAsync(Args("new-shop"), new StringWriter(), new StringWriter(), default));
    }
    private static string[] Args(string slug, string email = "new@example.test") => ["--provision-business", "--slug", slug, "--name", "Assumed provisioned shop", "--kind", "retailer", "--owner-email", email];
}
