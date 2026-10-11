// Role: Defines the swappable item data boundary. SampleItemRepository (JSON,
// memory only) and SqlItemRepository (SQL Server) both implement it.
// Implementations throw DomainValidationException for bad input, including an
// unknown status. Any known status can be set from any other (assumed).

using NbJewel.Api.Models;

namespace NbJewel.Api.Repositories;

public interface IItemRepository
{
    Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(int itemId, CancellationToken cancellationToken);
    Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Design>> GetDesignsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken);
    Task<Item> CreateAsync(ItemCreateRequest request, CancellationToken cancellationToken);
    Task<Item?> UpdateAsync(int id, ItemUpdateRequest request, CancellationToken cancellationToken);
    Task<Item?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken);
    // Adds stock and returns the new on-hand record. Returns null when the item does not exist.
    Task<StockRecord?> AddStockAsync(int itemId, StockAddRequest request, CancellationToken cancellationToken);
}
