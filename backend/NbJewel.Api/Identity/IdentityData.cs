// Role: Persists login accounts and explicit permission grants separately from ERP data.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NbJewel.Api.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string AccountType { get; set; } = "customer";
    public bool AllStores { get; set; }
}

public sealed class PermissionGrant
{
    public string UserId { get; set; } = "";
    public string Permission { get; set; } = "";
}

public sealed class StoreGrant
{
    public string UserId { get; set; } = "";
    public int StoreId { get; set; }
}

public sealed class IdentityDatabase(DbContextOptions<IdentityDatabase> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
    public DbSet<StoreGrant> StoreGrants => Set<StoreGrant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<PermissionGrant>().HasKey(x => new { x.UserId, x.Permission });
        builder.Entity<PermissionGrant>().Property(x => x.Permission).HasMaxLength(100);
        builder.Entity<PermissionGrant>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId);
        builder.Entity<StoreGrant>().HasKey(x => new { x.UserId, x.StoreId });
        builder.Entity<StoreGrant>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId);
    }
}

public sealed record IdentityAvailability(bool Configured);

public sealed record AccountAccess(string Id, string Email, string AccountType,
    string[] Permissions, int[] StoreIds, bool AllStores);

// An external ERP adapter must resolve trusted grants by user ID, never by browser claims.
public interface IPermissionAdapter
{
    Task<AccountAccess?> ResolveAsync(string userId, CancellationToken cancellationToken);
}

public sealed class DatabasePermissionAdapter(IdentityDatabase database, IdentityAvailability availability)
    : IPermissionAdapter
{
    public async Task<AccountAccess?> ResolveAsync(string userId, CancellationToken cancellationToken)
    {
        if (!availability.Configured) return null;
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return null;
        var permissions = await database.PermissionGrants.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => x.Permission).ToArrayAsync(cancellationToken);
        var stores = await database.StoreGrants.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => x.StoreId).ToArrayAsync(cancellationToken);
        return new(user.Id, user.Email ?? "", user.AccountType, permissions, stores, user.AllStores);
    }
}
