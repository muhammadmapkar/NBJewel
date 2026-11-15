// Role: Provides assumed Development/Sample host-only business onboarding, with terminal-only generated credentials.
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using NbJewel.Api.Identity;
namespace NbJewel.Api.Businesses;

public sealed class ProvisioningCommand(BusinessRegistry registry, BusinessMemberships memberships,
    UserManager<ApplicationUser> users, IPermissionAdapter permissions, IdentityAvailability availability,
    IConfiguration configuration, IHostEnvironment environment)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static bool Allowed(IConfiguration configuration, IHostEnvironment environment) => environment.IsDevelopment() &&
        (string.IsNullOrWhiteSpace(configuration["Repository:Provider"]) || string.Equals(configuration["Repository:Provider"], "Sample", StringComparison.OrdinalIgnoreCase)) &&
        !configuration.AsEnumerable().Any(x => !string.IsNullOrWhiteSpace(x.Value) && (x.Key.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase) || x.Key.Contains("SQL", StringComparison.OrdinalIgnoreCase)));
    public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (!Allowed(configuration, environment) || !availability.Configured)
        { await error.WriteLineAsync("Provisioning requires Development, Sample and configured memory-only identity."); return 1; }
        string? Value(string key) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null; }
        var slug = Value("--slug") ?? ""; var name = Value("--name") ?? ""; var kind = Value("--kind") ?? ""; var email = Value("--owner-email") ?? "";
        if (!BusinessRegistry.ValidSlug(slug) || string.IsNullOrWhiteSpace(name) || name.Length > 120 || kind is not ("retailer" or "wholesaler") || email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        { await error.WriteLineAsync("Invalid business arguments."); return 1; }
        await Gate.WaitAsync(ct);
        try
        {
            if (registry.Find(slug) is not null) { await error.WriteLineAsync("Business already exists."); return 1; }
            var user = await users.FindByEmailAsync(email);
            string? password = null;
            if (user is not null)
            {
                var access = await permissions.ResolveAsync(user.Id, ct);
                if (user.AccountType != "erp" || access is null || await memberships.ResolveAsync(access, ct) is not null)
                { await error.WriteLineAsync("Owner account is not eligible for a new membership."); return 1; }
            }
            else
            {
                password = "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
                user = new ApplicationUser { Email = email, UserName = email, AccountType = "erp", AllStores = true, EmailConfirmed = true };
                if (!(await users.CreateAsync(user, password)).Succeeded) { await error.WriteLineAsync("Owner creation failed Identity validation."); return 1; }
            }
            // Any interrupted onboarding remains inaccessible until its explicit membership exists.
            registry.Add(slug, name, kind);
            await memberships.AssignAsync(user.Id, slug, "owner", ct);
            await output.WriteLineAsync("Created offline business and owner membership.");
            if (password is not null) await output.WriteLineAsync("Temporary password: " + password);
            return 0;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        { await error.WriteLineAsync("Provisioning failed. Inspect offline registration before retrying."); return 1; }
        finally { Gate.Release(); }
    }
}
