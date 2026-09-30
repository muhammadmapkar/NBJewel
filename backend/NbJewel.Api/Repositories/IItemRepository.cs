// Role: Defines the swappable item data boundary for sample JSON now and a
// future SQL Server or verified ERP implementation later.

using NbJewel.Api.Models;

namespace NbJewel.Api.Repositories;

public interface IItemRepository
{
    Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken);
    Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken);
    Task<Item> CreateAsync(ItemCreateRequest request, CancellationToken cancellationToken);
    Task<Item?> UpdateAsync(int id, ItemUpdateRequest request, CancellationToken cancellationToken);
    Task<Item?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken);
}
