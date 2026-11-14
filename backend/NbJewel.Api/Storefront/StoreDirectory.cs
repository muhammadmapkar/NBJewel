// Role: Reads explicitly configured public store locations and derives freshness and inventory counts from ERP data.
using System.Text.Json;
using NbJewel.Api.Repositories;
using NbJewel.Api.Platform;
using NbJewel.Api.Models;
namespace NbJewel.Api.Storefront;
public sealed record StoreLocation(string Id, string Name, string City, double Latitude, double Longitude, string Source,
    bool IsSample, bool ErpEnabled, DateTimeOffset? InventorySyncedAt, DateTimeOffset? ImagesSyncedAt, string[] ProductIds);
public sealed record PublicStore(StoreLocation Location, bool LiveSynced, int ProductCount);
public sealed class StoreDirectory(IConfiguration configuration, IItemRepository items, IPublicationReader publications)
{
    public static bool Fresh(DateTimeOffset? date, DateTimeOffset now) => date.HasValue && date <= now.AddMinutes(1) && date >= now.AddMinutes(-15);
    public async Task<IReadOnlyList<PublicStore>> ReadAsync(CancellationToken ct)
    {
        var path = configuration["STOREFRONT_STORES_PATH"];
        if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(AppContext.BaseDirectory, "sample-data", "store-directory.json");
        var data = await File.ReadAllTextAsync(path, ct);
        if (data.Length > 256000) throw new InvalidOperationException("Invalid store directory.");
        var rows = JsonSerializer.Deserialize<StoreLocation[]>(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        if (rows.Length > 100 || rows.Any(x => x is null) || rows.Select(x => x.Id).Distinct().Count() != rows.Length || rows.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 60 || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 100 || string.IsNullOrWhiteSpace(x.City) || x.City.Length > 100 || !double.IsFinite(x.Latitude) || !double.IsFinite(x.Longitude) || Math.Abs(x.Latitude) > 85 || Math.Abs(x.Longitude) > 180 || x.Source is not ("demo" or "erp") || x.ProductIds is null)) throw new InvalidOperationException("Invalid store directory.");
        var knownStores = (await items.GetStoresAsync(ct)).Select(x => x.Id).ToHashSet();
        var sample = !string.Equals(configuration["Repository:Provider"], "Sql", StringComparison.OrdinalIgnoreCase);
        var result = new List<PublicStore>();
        foreach (var location in rows)
        {
            if (location.Source == "demo") { if (sample) result.Add(new(location with { IsSample = true, ErpEnabled = false }, false, location.ProductIds.Length)); continue; }
            if (!int.TryParse(location.Id, out var id) || !knownStores.Contains(id)) continue;
            var count = (await AvailableItemIdsAsync(id, ct)).Count;
            var live = !sample && !location.IsSample && location.ErpEnabled && Fresh(location.InventorySyncedAt, DateTimeOffset.UtcNow) && Fresh(location.ImagesSyncedAt, DateTimeOffset.UtcNow);
            result.Add(new(location with { IsSample = sample || location.IsSample, ProductIds = [] }, live, count));
        }
        return result;
    }
    public async Task<IReadOnlySet<int>> AvailableItemIdsAsync(int storeId, CancellationToken ct)
    {
        var published = await publications.PublishedItemIdsAsync(ct);
        if (published.Count > 10000) throw new InvalidOperationException("Catalog is too large.");
        var stock = await items.GetStockForItemsAsync(published.ToArray(), ct);
        var stocked = stock.Where(x => x.StoreId == storeId).GroupBy(x => x.ItemId).Where(g => g.Sum(x => (long)x.OnHand - x.Reserved) > 0).Select(g => g.Key).ToHashSet();
        var eligible = new HashSet<int>(); var categories = await PublicCatalogPolicy.AllowedCategoriesAsync(items, ct);
        for (var page = 1; ; page++) {
            var batch = await items.SearchItemsAsync(null, null, null, null, ItemStatusRules.Complete, page, 100, ct);
            if (batch.TotalCount > 10000) throw new InvalidOperationException("Catalog is too large.");
            foreach (var item in batch.Items) if (stocked.Contains(item.Id) && PublicCatalogPolicy.Allows(item, categories)) eligible.Add(item.Id);
            if (page * 100 >= batch.TotalCount) break;
            if (batch.Items.Count == 0) throw new InvalidOperationException("Incomplete catalog.");
        }
        return eligible;
    }
}
