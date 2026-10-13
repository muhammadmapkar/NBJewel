// Role: Loads synthetic JSON fixtures and provides read operations plus
// memory-only item, status, and stock changes for local API demonstrations.
// Changes reset when the API restarts. This is the default repository.
// It must behave like SqlItemRepository: same validation (ItemWriteRules), same
// computed net weight, same ordering (by id), same "latest rate" rule.

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
    // Valid (metal, purity) pairs, taken from the rates and the items. The SQL schema keeps these in its Purity table.
    private readonly List<(string Metal, string Purity)> purities;
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
        purities = rates.Select(x => (x.MetalType, x.Purity)).Concat(items.Select(x => (x.MetalType, x.Purity)))
            .DistinctBy(x => (x.Item1.ToLowerInvariant(), x.Item2.ToLowerInvariant())).ToList();
        foreach (var item in items)
        {
            ApplyDesign(item);
            item.NetWt = item.GrossWt - item.StoneWt; // net weight is always computed, never trusted from the file
        }
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
    public Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken) { lock (sync) { return Task.FromResult<IReadOnlyList<StockRecord>>(stock.Where(x => x.ItemId == itemId).OrderBy(x => x.VariantId ?? 0).ThenBy(x => x.StoreId).Select(Copy).ToList()); } }
    public Task<IReadOnlyList<StockRecord>> GetLowStockAsync(int threshold, CancellationToken cancellationToken) { lock (sync) { return Task.FromResult<IReadOnlyList<StockRecord>>(stock.Where(x => x.OnHand - x.Reserved < threshold).OrderBy(x => x.ItemId).ThenBy(x => x.VariantId ?? 0).ThenBy(x => x.StoreId).Select(Copy).ToList()); } }
    public Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(int itemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ItemVariant>>(variants.Where(x => x.ItemId == itemId).OrderBy(x => x.Id).ToList());
    public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Category>>(categories.OrderBy(x => x.Id).ToList());
    public Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Store>>(stores.OrderBy(x => x.Id).ToList());
    public Task<IReadOnlyList<Design>> GetDesignsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Design>>(designs.OrderBy(x => x.DesignId).ToList());

    // Latest rate per metal and purity: newest date wins, and the higher id wins a tie. Listed by id.
    public Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MetalRate>>(rates
            .GroupBy(x => (x.MetalType.ToLowerInvariant(), x.Purity.ToLowerInvariant()))
            .Select(g => g.OrderByDescending(r => r.EffectiveDate).ThenByDescending(r => r.Id).First())
            .OrderBy(x => x.Id).ToList());

    public async Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken)
    {
        var item = items.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return null;
        var latest = await GetLatestRatesAsync(cancellationToken);
        var rate = latest.FirstOrDefault(x => x.MetalType.Equals(item.MetalType, StringComparison.OrdinalIgnoreCase) && x.Purity.Equals(item.Purity, StringComparison.OrdinalIgnoreCase));
        return rate is null ? null : ItemPricing.Calculate(item, rate);
    }

    public Task<Item> CreateAsync(ItemCreateRequest request, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var valid = ItemWriteRules.Validate(request.Code, request.Name, request.GrossWt, request.StoneWt, request.MakingType, request.MakingValue, request.Huid, request.ImageUrl);
            EnsureLinks(request.DesignId, request.CategoryId, request.MetalType, request.Purity, valid.Code, null);
            var item = Build(valid, items.Count == 0 ? 1 : items.Max(x => x.Id) + 1, request.CategoryId, request.DesignId, request.MetalType, request.Purity, ItemStatusRules.Draft);
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
            var valid = ItemWriteRules.Validate(request.Code, request.Name, request.GrossWt, request.StoneWt, request.MakingType, request.MakingValue, request.Huid, request.ImageUrl);
            EnsureLinks(request.DesignId, request.CategoryId, request.MetalType, request.Purity, valid.Code, id);
            var item = Build(valid, id, request.CategoryId, request.DesignId, request.MetalType, request.Purity, items[index].Status);
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

    // Checks the same links the SQL foreign keys and unique code check enforce. Code match ignores case, like SQL Server's default.
    private void EnsureLinks(int designId, int categoryId, string metal, string purity, string code, int? ownId)
    {
        if (designs.All(x => x.DesignId != designId)) throw new DomainValidationException($"Design {designId} does not exist.");
        if (categories.All(x => x.Id != categoryId)) throw new DomainValidationException($"Category {categoryId} does not exist.");
        if (!purities.Any(x => x.Metal.Equals(metal, StringComparison.OrdinalIgnoreCase) && x.Purity.Equals(purity, StringComparison.OrdinalIgnoreCase)))
            throw new DomainValidationException($"No purity '{purity}' exists for metal '{metal}'.");
        if (items.Any(x => x.Id != ownId && x.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) throw new DomainValidationException("That item code already exists.");
    }

    private Item Build(ValidItem v, int id, int categoryId, int designId, string metal, string purity, string status)
    {
        var pair = purities.First(x => x.Metal.Equals(metal, StringComparison.OrdinalIgnoreCase) && x.Purity.Equals(purity, StringComparison.OrdinalIgnoreCase));
        var item = new Item
        {
            Id = id, Code = v.Code, Name = v.Name, CategoryId = categoryId, DesignId = designId,
            MetalType = pair.Metal, Purity = pair.Purity, GrossWt = v.GrossWt, NetWt = v.NetWt, StoneWt = v.StoneWt,
            MakingType = v.MakingType, MakingValue = v.MakingValue, Huid = v.Huid, ImageUrl = v.ImageUrl, Status = status
        };
        ApplyDesign(item);
        return item;
    }

    private void ApplyDesign(Item item)
    {
        var design = designs.FirstOrDefault(x => x.DesignId == item.DesignId);
        if (design is not null) item.DesignCode = design.DesignCode;
    }

    private static List<T> Load<T>(string directory, string fileName) => JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(directory, fileName)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    private static StockRecord Copy(StockRecord x) => new() { ItemId = x.ItemId, VariantId = x.VariantId, StoreId = x.StoreId, OnHand = x.OnHand, Reserved = x.Reserved };
}
