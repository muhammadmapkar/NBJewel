// Role: Stores current image revision and manual review in SQL with optimistic concurrency.
// Each operation uses a fresh context; SQL mode never consults sample JSON links.
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Repositories;

public sealed class SqlItemImageAssetRepository(DbContextOptions<ItemAssetDatabase> options) : IItemImageAssetRepository
{
    public async Task<ItemImageAsset?> GetAsync(int itemId, CancellationToken ct)
    {
        await using var db = new ItemAssetDatabase(options);
        return await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == itemId, ct);
    }
    public async Task SaveAsync(ItemImageAsset asset, CancellationToken ct)
    {
        if (asset.ItemId < 1 || !Guid.TryParseExact(asset.RevisionId, "N", out _)) throw new ArgumentException("Invalid item asset link.");
        await using var db = new ItemAssetDatabase(options);
        var current = await db.Assets.SingleOrDefaultAsync(x => x.ItemId == asset.ItemId, ct);
        if (current is null) db.Assets.Add(asset);
        else
        {
            if (asset.State != "processing" && current.RevisionId != asset.RevisionId) throw new ItemAssetConflictException();
            db.Entry(current).CurrentValues.SetValues(asset);
        }
        await Save(db, ct);
    }
    public async Task<ItemImageAsset?> ReviewAsync(int itemId, string revisionId, bool approve, CancellationToken ct)
    {
        await using var db = new ItemAssetDatabase(options);
        var current = await db.Assets.SingleOrDefaultAsync(x => x.ItemId == itemId, ct);
        if (current is null) return null;
        if (current.RevisionId != revisionId || current.State != "ready") throw new ItemAssetConflictException();
        var updated = current with { QualityReview = approve ? "approved" : "pending" };
        db.Entry(current).CurrentValues.SetValues(updated);
        await Save(db, ct);
        return updated;
    }
    private static async Task Save(ItemAssetDatabase db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ItemAssetConflictException(); }
    }
}
