// Role: Resolves assumed explicit Sample memberships and implicit single-business SQL memberships.
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Identity;
namespace NbJewel.Api.Businesses;

public sealed class BusinessMembership
{
    public string UserId { get; set; } = "";
    public string BusinessId { get; set; } = "";
    public string Role { get; set; } = "staff";
}
public sealed record BusinessAccess(string Slug, string Name, string Role);

public sealed class BusinessMemberships(IdentityDatabase database, IConfiguration configuration, BusinessRegistry registry)
{
    public static readonly string[] OwnerPermissions = ["items.read", "items.write", "prices.read", "stock.read", "stock.write", "status.read", "status.write", "reference.read", "chat.read", "jewelry.manage", "knowledge.manage", "portal.manage", "portal.publish"];
    private bool Sql => string.Equals(configuration["Repository:Provider"], "Sql", StringComparison.OrdinalIgnoreCase);
    public async Task<BusinessAccess?> ResolveAsync(AccountAccess access, CancellationToken ct)
    {
        if (access.AccountType != "erp") return null;
        if (Sql) return new(BusinessRegistry.DemoSlug, registry.Find(BusinessRegistry.DemoSlug)!.Name, access.AllStores ? "owner" : "staff");
        var member = await database.Set<BusinessMembership>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == access.Id, ct);
        var business = member is null ? null : registry.Find(member.BusinessId);
        return business is null || member!.Role is not ("owner" or "staff") ? null : new(business.Slug, business.Name, member.Role);
    }
    public async Task AssignAsync(string userId, string slug, string role, CancellationToken ct)
    {
        if (Sql || registry.Find(slug) is null || role is not ("owner" or "staff")) throw new ArgumentException("Invalid Sample membership.");
        if (!await database.Users.AnyAsync(x => x.Id == userId && x.AccountType == "erp", ct)) throw new ArgumentException("Only staff may hold a membership.");
        var existing = await database.Set<BusinessMembership>().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (existing is not null) { if (existing.BusinessId != slug || existing.Role != role) throw new ArgumentException("Staff already has a membership."); return; }
        database.Add(new BusinessMembership { UserId = userId, BusinessId = slug, Role = role });
        await database.SaveChangesAsync(ct);
    }
    public async Task MigrateAsync(CancellationToken ct)
    {
        if (Sql) return;
        var users = await database.Users.AsNoTracking().Where(x => x.AccountType == "erp").ToListAsync(ct);
        var assigned = await database.Set<BusinessMembership>().Select(x => x.UserId).ToListAsync(ct);
        foreach (var user in users.Where(x => !assigned.Contains(x.Id)))
            database.Add(new BusinessMembership { UserId = user.Id, BusinessId = BusinessRegistry.DemoSlug, Role = user.AllStores ? "owner" : "staff" });
        await database.SaveChangesAsync(ct);
    }
    public static AccountAccess Effective(AccountAccess access, BusinessAccess member) => member.Role == "owner"
        ? access with { Permissions = OwnerPermissions.ToArray(), AllStores = true } : access;
}

public sealed class MembershipMigration(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        if (scope.ServiceProvider.GetRequiredService<IdentityAvailability>().Configured)
            await scope.ServiceProvider.GetRequiredService<BusinessMemberships>().MigrateAsync(ct);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
