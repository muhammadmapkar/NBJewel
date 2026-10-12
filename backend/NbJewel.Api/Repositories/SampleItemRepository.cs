// Role: Loads synthetic JSON fixtures and provides read operations plus
// memory-only item, status, and stock changes for local API demonstrations.
// Changes reset when the API restarts. This is the default repository.

using System.Text.Json;
using NbJewel.Api.Models;

namespace NbJewel.Api.Repositories;

public sealed class SampleItemRepository : IItemRepository
{
    private readonly List<Item> items;
    private readonly List<StockRecord> stock;
    private readonly List<ItemVariant> variants;
    private readonly List<Category> categories;
    private readonly List<Store> stores;
    private readonly List<Design> designs;
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
        variants = Load<ItemVariant>(directory, "variants.json");
        categories = Load<Category>(directory, "categories.json");
        stores = Load<Store>(directory, "stores.json");
        designs = Load<Design>(directory, "designs.json");
        rates = Load<MetalRate>(directory, "metal-rates.json");
        foreach (var item in items) ApplyDesign(item);
    }

    public Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            IEnumerable<Item> query = items;
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(search, StringComparison.OrdinalIgnoreCase) || x.DesignCode.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(metal)) query = query.Where(x => x.MetalType.Equals(metal, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(purity)) query = query.Where(x => x.Purity.Equals(purity, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            var result = query.OrderBy(x => x.Id).ToList();
            return Task.FromResult(new PagedResult<Item>(result.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, result.Count));
        }
    }

    public Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken) { lock (sync) { return Task.FromResult(items.FirstOrDefault(x => x.Id == id)); } }
    public Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken) { lock (sync) { return Task.FromResult<IReadOnlyList<StockRecord>>(stock.Where(x => x.ItemId == itemId).Select(Copy).ToList()); } }
    public Task<IReadOnlyList<StockRecord>> GetLowStockAsync(int threshold, CancellationToken cancellationToken) { lock (sync) { return Task.FromResult<IReadOnlyList<StockRecord>>(stock.Where(x => x.OnHand - x.Reserved < threshold).OrderBy(x => x.ItemId).ThenBy(x => x.VariantId).ThenBy(x => x.StoreId).Select(Copy).ToList()); } }
    public Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(int itemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ItemVariant>>(variants.Where(x => x.ItemId == itemId).ToList());
    public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Category>>(categories);
    public Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Store>>(stores);
    public Task<IReadOnlyList<Design>> GetDesignsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Design>>(designs);
    public Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MetalRate>>(rates.GroupBy(x => $"{x.MetalType}|{x.Purity}").Select(x => x.OrderByDescending(r => r.EffectiveDate).First()).ToList());

    public Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken)
    {
        var item = items.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return Task.FromResult<ItemPrice?>(null);
        var rate = rates.Where(x => x.MetalType.Equals(item.MetalType, StringComparison.OrdinalIgnoreCase) && x.Purity.Equals(item.Purity, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.EffectiveDate).FirstOrDefault();
        return Task.FromResult(rate is null ? null : ItemPricing.Calculate(item, rate));
    }

    public Task<Item> CreateAsync(ItemCreateRequest request, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            EnsureDesign(request.DesignId);
            var item = Map(request, items.Max(x => x.Id) + 1);
            items.Add(item);
            return Task.FromResult(item);
        }
    }

    public Task<Item?> UpdateAsync(int id, ItemUpdateRequest request, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var index = items.FindIndex(x => x.Id == id);
            if (index < 0) return Task.FromResult<Item?>(null);
            EnsureDesign(request.DesignId);
            var item = Map(request, id, items[index].Status);
            items[index] = item;
            return Task.FromResult<Item?>(item);
        }
    }

    public Task<Item?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var item = items.FirstOrDefault(x => x.Id == id);
            if (item is null) return Task.FromResult<Item?>(null);
            // Any known status is allowed. Choosing the current one changes nothing.
            item.Status = ItemStatusRules.Resolve(status);
            return Task.FromResult<Item?>(item);
        }
    }

    public Task<StockRecord?> AddStockAsync(int itemId, StockAddRequest request, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (items.All(x => x.Id != itemId)) return Task.FromResult<StockRecord?>(null);
            StockRules.EnsureQuantity(request.Quantity);
            if (stores.All(x => x.Id != request.StoreId)) throw new DomainValidationException($"Store {request.StoreId} does not exist.");

            var itemVariants = variants.Where(x => x.ItemId == itemId).ToList();
            if (itemVariants.Count > 0)
            {
                if (request.VariantId is null) throw new DomainValidationException("variantId is required for an item that has variants.");
                if (itemVariants.All(x => x.Id != request.VariantId)) throw new DomainValidationException($"Variant {request.VariantId} does not belong to item {itemId}.");
            }
            else if (request.VariantId is not null)
            {
                throw new DomainValidationException("This item has no variants, so variantId must be empty.");
            }

            var record = stock.FirstOrDefault(x => x.ItemId == itemId && x.VariantId == request.VariantId && x.StoreId == request.StoreId);
            if (record is null)
            {
                record = new StockRecord { ItemId = itemId, VariantId = request.VariantId, StoreId = request.StoreId, OnHand = request.Quantity };
                stock.Add(record);
            }
            else
            {
                record.OnHand = StockRules.AddOnHand(record.OnHand, request.Quantity);
            }
            return Task.FromResult<StockRecord?>(Copy(record));
        }
    }

    private void EnsureDesign(int designId)
    {
        if (designs.All(x => x.DesignId != designId)) throw new DomainValidationException($"Design {designId} does not exist.");
    }

    private void ApplyDesign(Item item)
    {
        var design = designs.FirstOrDefault(x => x.DesignId == item.DesignId);
        if (design is not null) item.DesignCode = design.DesignCode;
    }

    private static List<T> Load<T>(string directory, string fileName) => JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(directory, fileName)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    private static StockRecord Copy(StockRecord x) => new() { ItemId = x.ItemId, VariantId = x.VariantId, StoreId = x.StoreId, OnHand = x.OnHand, Reserved = x.Reserved };

    private Item Map(ItemCreateRequest x, int id) => Fill(new Item { Id = id, Code = x.Code, Name = x.Name, CategoryId = x.CategoryId, DesignId = x.DesignId, MetalType = x.MetalType, Purity = x.Purity, GrossWt = x.GrossWt, NetWt = x.NetWt, StoneWt = x.StoneWt, MakingType = x.MakingType, MakingValue = x.MakingValue, Huid = x.Huid, ImageUrl = x.ImageUrl }, ItemStatusRules.Draft);
    private Item Map(ItemUpdateRequest x, int id, string status) => Fill(new Item { Id = id, Code = x.Code, Name = x.Name, CategoryId = x.CategoryId, DesignId = x.DesignId, MetalType = x.MetalType, Purity = x.Purity, GrossWt = x.GrossWt, NetWt = x.NetWt, StoneWt = x.StoneWt, MakingType = x.MakingType, MakingValue = x.MakingValue, Huid = x.Huid, ImageUrl = x.ImageUrl }, status);

    private Item Fill(Item item, string status)
    {
        item.Status = status;
        ApplyDesign(item);
        return item;
    }
}
