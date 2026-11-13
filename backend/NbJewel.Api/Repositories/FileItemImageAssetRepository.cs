// Role: Persists item-id preview links as atomic JSON files with no in-memory cache.
// Revision folders are retained when a link changes, so replacing an upload deletes no assets.
using System.Text.Json;
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Repositories;

public sealed class FileItemImageAssetRepository(ItemCutoutOptions options) : IItemImageAssetRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim gate = new(1, 1);
    private string Link(int itemId) => Path.Combine(options.StoragePath, "links", $"item-{itemId}.json");

    public async Task<ItemImageAsset?> GetAsync(int itemId, CancellationToken ct)
    {
        if (itemId < 1 || !File.Exists(Link(itemId))) return null;
        var asset = JsonSerializer.Deserialize<ItemImageAsset>(await File.ReadAllTextAsync(Link(itemId), ct), Json);
        if (asset is null || asset.ItemId != itemId || !Guid.TryParseExact(asset.RevisionId, "N", out _))
            throw new InvalidDataException("Invalid item asset link.");
        return asset;
    }

    public async Task<ItemImageAsset?> ReviewAsync(int itemId, string revisionId, bool approve, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var asset = await GetAsync(itemId, ct);
            if (asset is null) return null;
            if (asset.RevisionId != revisionId || asset.State != "ready") throw new ItemAssetConflictException();
            var updated = asset with { QualityReview = approve ? "approved" : "pending" };
            await WriteAsync(updated, ct);
            return updated;
        }
        finally { gate.Release(); }
    }
    private async Task WriteAsync(ItemImageAsset asset, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Link(asset.ItemId))!);
        var temp = Link(asset.ItemId) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(asset, Json), ct);
        File.Move(temp, Link(asset.ItemId), overwrite: true);
    }

    public async Task SaveAsync(ItemImageAsset asset, CancellationToken ct)
    {
        if (asset.ItemId < 1 || !Guid.TryParseExact(asset.RevisionId, "N", out _))
            throw new ArgumentException("Invalid item asset link.");
        await gate.WaitAsync(ct);
        try
        {
            await WriteAsync(asset, ct);
        }
        finally { gate.Release(); }
    }
}
