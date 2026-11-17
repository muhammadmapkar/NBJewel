// Role: Checks read-only core data access and durable storage availability without requiring AI, rates or email providers.
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
using NbJewel.Api.Knowledge;
namespace NbJewel.Api.Platform;

public sealed class CoreReadiness(IServiceProvider services, IConfiguration configuration)
{
    public async Task<bool> CheckAsync(CancellationToken ct)
    {
        try
        {
            // A temporary DeleteOnClose probe tests writes without changing existing files or creating missing roots.
            foreach (var key in PublicDemoSetup.StorageKeys)
            {
                var path = configuration[key];
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path)) return false;
                await using var probe = new FileStream(Path.Combine(path, ".readiness-" + Guid.NewGuid().ToString("N")),
                    FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                await probe.WriteAsync(new byte[] { 1 }, ct);
                await probe.FlushAsync(ct);
            }
            var identity = services.GetRequiredService<IdentityDatabase>();
            _ = await identity.Users.AsNoTracking().Take(1).ToListAsync(ct);
            _ = await identity.PermissionGrants.AsNoTracking().Take(1).ToListAsync(ct);
            _ = await identity.StoreGrants.AsNoTracking().Take(1).ToListAsync(ct);
            _ = await identity.CustomerPortals.AsNoTracking().Take(1).ToListAsync(ct);
            var knowledge = services.GetRequiredService<KnowledgeDatabase>();
            _ = await knowledge.Products.AsNoTracking().Take(1).ToListAsync(ct);
            _ = await knowledge.Audits.AsNoTracking().Take(1).ToListAsync(ct);
            var platform = services.GetRequiredService<PlatformDatabase>();
            _ = await platform.DataProtectionKeys.AsNoTracking().Take(1).ToListAsync(ct);
            var registry = services.GetRequiredService<BusinessRegistry>();
            var partition = services.GetRequiredService<BusinessStorage>().For(registry.Find(BusinessRegistry.DemoSlug)!);
            _ = await partition.Items.SearchItemsAsync(null, null, null, null, null, 1, 1, ct);
            _ = await partition.Items.GetStoresAsync(ct);
            _ = await partition.Items.GetCategoriesAsync(ct);
            _ = await partition.Items.GetLatestRatesAsync(ct);
            _ = await partition.Assets.GetAsync(1, ct);
            return true;
        }
        catch (Exception) { return false; }
    }
}
