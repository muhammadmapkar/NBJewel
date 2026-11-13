// Role: Swappable durable link from an ERP item ID to its current preview revision.
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Repositories;

public interface IItemImageAssetRepository
{
    Task<ItemImageAsset?> GetAsync(int itemId, CancellationToken ct);
    Task SaveAsync(ItemImageAsset asset, CancellationToken ct);
}
