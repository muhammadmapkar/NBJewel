// Role: Loads synthetic JSON fixtures and provides read operations plus
// memory-only item mutations for local API demonstrations.

using System.Text.Json;
using NbJewel.Api.Models;

namespace NbJewel.Api.Repositories;

public sealed class SampleItemRepository : IItemRepository
{
    private readonly List<Item> items;
    private readonly List<StockRecord> stock;
    private readonly List<Category> categories;
    private readonly List<Store> stores;
    private readonly List<MetalRate> rates;
    private readonly object sync = new();

    public SampleItemRepository(IWebHostEnvironment environment)
    {
        var contentRootDirectory = Path.Combine(environment.ContentRootPath, "sample-data");
        var directory = Directory.Exists(contentRootDirectory)
            ? contentRootDirectory
            : Path.Combine(AppContext.BaseDirectory, "sample-data");
        items = Load<Item>(directory, "items.json");
        stock = Load<StockRecord>(directory, "stock.json");
        categories = Load<Category>(directory, "categories.json");
        stores = Load<Store>(directory, "stores.json");
        rates = Load<MetalRate>(directory, "metal-rates.json");
    }

    public Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            IEnumerable<Item> query = items;
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(metal)) query = query.Where(x => x.MetalType.Equals(metal, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(purity)) query = query.Where(x => x.Purity.Equals(purity, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            var result = query.OrderBy(x => x.Id).ToList();
            return Task.FromResult(new PagedResult<Item>(result.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, result.Count));
        }
    }

    public Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken) => Task.FromResult(items.FirstOrDefault(x => x.Id == id));
    public Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StockRecord>>(stock.Where(x => x.ItemId == itemId).ToList());
    public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Category>>(categories);
    public Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Store>>(stores);
    public Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MetalRate>>(rates.GroupBy(x => $"{x.MetalType}|{x.Purity}").Select(x => x.OrderByDescending(r => r.EffectiveDate).First()).ToList());

    public Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken)
    {
        var item = items.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return Task.FromResult<ItemPrice?>(null);
        var rate = rates.Where(x => x.MetalType.Equals(item.MetalType, StringComparison.OrdinalIgnoreCase) && x.Purity.Equals(item.Purity, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.EffectiveDate).FirstOrDefault();
        if (rate is null) return Task.FromResult<ItemPrice?>(null);
        var metalValue = item.NetWt * rate.RatePerGram;
        var makingCharge = item.MakingType.ToLowerInvariant() switch
        {
            "pergram" => item.NetWt * item.MakingValue,
            "percent" => metalValue * item.MakingValue / 100m,
            "flat" => item.MakingValue,
            _ => throw new InvalidOperationException($"Unsupported making type '{item.MakingType}'.")
        };
        return Task.FromResult<ItemPrice?>(new(item.Id, rate.Currency, item.NetWt, rate.RatePerGram, Round(metalValue), item.MakingType, item.MakingValue, Round(makingCharge), Round(metalValue + makingCharge)));
    }

    public Task<Item> CreateAsync(ItemCreateRequest request, CancellationToken cancellationToken) { lock (sync) { var item = Map(request, items.Max(x => x.Id) + 1); items.Add(item); return Task.FromResult(item); } }
    public Task<Item?> UpdateAsync(int id, ItemUpdateRequest request, CancellationToken cancellationToken) { lock (sync) { var index = items.FindIndex(x => x.Id == id); if (index < 0) return Task.FromResult<Item?>(null); var item = Map(request, id); items[index] = item; return Task.FromResult<Item?>(item); } }
    public Task<Item?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken) { lock (sync) { var item = items.FirstOrDefault(x => x.Id == id); if (item is null) return Task.FromResult<Item?>(null); item.Status = status; return Task.FromResult<Item?>(item); } }

    private static List<T> Load<T>(string directory, string fileName) => JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(directory, fileName)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    private static Item Map(ItemCreateRequest x, int id) => new() { Id = id, Code = x.Code, Name = x.Name, CategoryId = x.CategoryId, DesignCode = x.DesignCode, MetalType = x.MetalType, Purity = x.Purity, GrossWt = x.GrossWt, NetWt = x.NetWt, StoneWt = x.StoneWt, MakingType = x.MakingType, MakingValue = x.MakingValue, Huid = x.Huid, Status = x.Status, ImageUrl = x.ImageUrl };
    private static Item Map(ItemUpdateRequest x, int id) => new() { Id = id, Code = x.Code, Name = x.Name, CategoryId = x.CategoryId, DesignCode = x.DesignCode, MetalType = x.MetalType, Purity = x.Purity, GrossWt = x.GrossWt, NetWt = x.NetWt, StoneWt = x.StoneWt, MakingType = x.MakingType, MakingValue = x.MakingValue, Huid = x.Huid, Status = x.Status, ImageUrl = x.ImageUrl };
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
