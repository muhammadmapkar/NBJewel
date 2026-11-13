// Role: Restricts chat catalog reads to stock records in the trusted store scope.
using NbJewel.Api.Chat;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Platform;

public sealed class ScopedChatDataReader(IItemRepository repository, AssistantContext context) : IChatDataReader
{
    private const int ScanLimit = 10000;
    private bool Allowed(int storeId) => context.AllStores || context.StoreIds.Contains(storeId);
    public async Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (context.AllStores) return await repository.SearchItemsAsync(search, categoryId, metal, purity, status, page, pageSize, cancellationToken);
        if (context.StoreIds.Length == 0) return new([], page, pageSize, 0);
        var visible = new List<Item>();
        for (var current = 1; ; current++)
        {
            var batch = await repository.SearchItemsAsync(search, categoryId, metal, purity, status, current, 100, cancellationToken);
            if (batch.TotalCount > ScanLimit) throw new AssistantUnavailableException();
            // One stock read for the whole batch; an item is visible when it has stock in a store of this scope.
            var inScope = (await repository.GetStockForItemsAsync(batch.Items.Select(i => i.Id).ToArray(), cancellationToken))
                .Where(s => Allowed(s.StoreId)).Select(s => s.ItemId).ToHashSet();
            visible.AddRange(batch.Items.Where(item => inScope.Contains(item.Id)));
            if ((long)current * 100 >= batch.TotalCount) break;
            if (batch.Items.Count == 0) throw new AssistantUnavailableException();
        }
        return new(visible.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToArray(), page, pageSize, visible.Count);
    }
    public async Task<Item?> GetItemAsync(int id, CancellationToken ct) => context.AllStores || (await GetStockAsync(id, ct)).Count > 0 ? await repository.GetItemAsync(id, ct) : null;
    public async Task<IReadOnlyList<StockRecord>> GetStockAsync(int id, CancellationToken ct) => (await repository.GetStockAsync(id, ct)).Where(s => Allowed(s.StoreId)).ToArray();
    public async Task<IReadOnlyList<StockRecord>> GetStockForItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct) => (await repository.GetStockForItemsAsync(itemIds, ct)).Where(s => Allowed(s.StoreId)).ToArray();
    public async Task<ItemPrice?> GetPriceAsync(int id, CancellationToken ct) => await GetItemAsync(id, ct) is null ? null : await repository.GetPriceAsync(id, ct);
    public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct) => repository.GetCategoriesAsync(ct);
    public async Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken ct) => (await repository.GetStoresAsync(ct)).Where(s => Allowed(s.Id)).ToArray();
    public Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken ct) => repository.GetLatestRatesAsync(ct);
}
