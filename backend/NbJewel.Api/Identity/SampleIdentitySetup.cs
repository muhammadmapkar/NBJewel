// Role: Enables explicitly requested localhost-only sample staff identity using the existing
// Identity password/cookie/grant pipeline. Credentials and accounts are memory-only per API run.
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace NbJewel.Api.Identity;

public sealed record SampleIdentityOptions(bool Enabled, string Password, string DatabaseName)
{
    public bool AllowCustomers { get; init; }
    public override string ToString() => $"SampleIdentityOptions {{ Enabled = {Enabled}, Password = [redacted] }}";
    public const string Email = "sample.staff@nbjewel.invalid";
    public static bool LocalHost(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));
    public static SampleIdentityOptions Read(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>("SampleIdentity:Enabled")) return new(false, "", "");
        var provider = configuration["Repository:Provider"];
        if (!environment.IsDevelopment() || (!string.IsNullOrWhiteSpace(provider) && !provider.Equals("Sample", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Sample identity requires Development and the Sample repository.");
        if (configuration.AsEnumerable().Any(x => !string.IsNullOrWhiteSpace(x.Value) &&
            (x.Key.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase) || x.Key.Contains("SQL", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Sample identity requires all SQL/connection settings unset.");
        if (configuration.GetSection("Kestrel:Endpoints").GetChildren().Any() || !string.IsNullOrWhiteSpace(configuration["TRUSTED_PROXIES"]))
            throw new InvalidOperationException("Sample identity cannot use configured Kestrel endpoints or trusted proxies.");
        foreach (var address in (configuration["urls"] ?? configuration["ASPNETCORE_URLS"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || !LocalHost(uri.Host) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException("Sample identity cannot bind a nonloopback address.");
        var password = configuration["SampleIdentity:StaffPassword"] ?? "";
        if (password.Length is < 16 or > 128) throw new InvalidOperationException("Supply a strong runtime-only sample staff password of 16 to 128 characters.");
        return new(true, password, "sample-identity-" + Guid.NewGuid().ToString("N")) { AllowCustomers = configuration.GetValue<bool>("SampleIdentity:AllowCustomers") };
    }
}

public static class SampleIdentitySetup
{
    public static void ConfigureSampleIdentityRuntime(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<SampleIdentityOptions>().Enabled) return;
        // Always pin this development mode to localhost, regardless of HTTP_PORTS defaults.
        app.Urls.Clear(); app.Urls.Add("http://localhost:5155");
        app.Use(async (context, next) => {
            if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip) || !SampleIdentityOptions.LocalHost(context.Request.Host.Host))
            { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            await next(context);
        });
    }
}

public sealed class SampleIdentityBootstrap(IServiceProvider services) : IHostedService
{
    private bool started;
    public async Task StartAsync(CancellationToken ct)
    {
        if (started) return;
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<SampleIdentityOptions>();
        if (!options.Enabled) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // Never elevate an existing account. This provider has a unique database per API host.
        if (await users.Users.AnyAsync(ct)) throw new InvalidOperationException("Sample identity must start with an empty isolated account store.");
        var user = new ApplicationUser { Email = SampleIdentityOptions.Email, UserName = SampleIdentityOptions.Email,
            AccountType = "erp", AllStores = true, EmailConfirmed = true };
        if (!(await users.CreateAsync(user, options.Password)).Succeeded)
            throw new InvalidOperationException("The sample staff password failed Identity validation.");
        var database = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        foreach (var permission in new[] { "items.read", "items.write", "prices.read", "stock.read", "status.read", "status.write", "reference.read", "knowledge.manage" })
            database.PermissionGrants.Add(new PermissionGrant { UserId = user.Id, Permission = permission });
        await database.SaveChangesAsync(ct);
        started = true;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
