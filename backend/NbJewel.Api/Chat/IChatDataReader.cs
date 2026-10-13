// Role: The only door the chat tools use to reach ERP data. It is read-only on purpose:
// every method starts with Get or Search, and there is no way to create, update or
// delete anything. ChatDataReader just passes each call to IItemRepository, the same
// repository the API controllers use, so the tools and the endpoints see the same data.

using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Chat;

public interface IChatDataReader
{
    Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken);
    Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken);
}

public sealed class ChatDataReader(IItemRepository repository) : IChatDataReader
{
    public Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken) =>
        repository.SearchItemsAsync(search, categoryId, metal, purity, status, page, pageSize, cancellationToken);
    public Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken) => repository.GetItemAsync(id, cancellationToken);
    public Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken) => repository.GetStockAsync(itemId, cancellationToken);
    public Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken) => repository.GetPriceAsync(itemId, cancellationToken);
    public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken) => repository.GetCategoriesAsync(cancellationToken);
    public Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken) => repository.GetStoresAsync(cancellationToken);
    public Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken) => repository.GetLatestRatesAsync(cancellationToken);
}
