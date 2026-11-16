// Role: Reads explicitly configured public store locations and derives freshness and inventory counts from ERP data.
using System.Text.Json;
using NbJewel.Api.Businesses;
using Microsoft.Data.SqlClient;
using NbJewel.Api.Repositories;
using NbJewel.Api.Platform;
using NbJewel.Api.Models;
namespace NbJewel.Api.Storefront;
public sealed record StoreLocation(string Id, string Name, string City, double Latitude, double Longitude, string Source,
    bool IsSample, bool ErpEnabled, DateTimeOffset? InventorySyncedAt, DateTimeOffset? ImagesSyncedAt, string[] ProductIds);
public sealed record PublicStore(StoreLocation Location, bool LiveSynced, int ProductCount);
public sealed class StoreDirectory(IConfiguration configuration, IItemRepository items, IPublicationReader publications, BusinessStorage? storage = null, BusinessContext? business = null, ILogger<StoreDirectory>? logger = null)
{
    public static bool Fresh(DateTimeOffset? date, DateTimeOffset now) => date.HasValue && date <= now.AddMinutes(1) && date >= now.AddMinutes(-15);
    public async Task<IReadOnlyList<PublicStore>> ReadAsync(CancellationToken ct)
    {
        var sample = !string.Equals(configuration["Repository:Provider"], "Sql", StringComparison.OrdinalIgnoreCase);
        StoreLocation[] rows;
        if (!sample) rows = await SqlLocations(ct);
        else {
            var path = storage is not null && business?.Business is { } current ? storage.DirectoryPath(current) : configuration["STOREFRONT_STORES_PATH"];
            if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(AppContext.BaseDirectory, "sample-data", "store-locations.json");
            var data = !File.Exists(path) && business?.Business?.Slug is { } slug && slug != BusinessRegistry.DemoSlug ? "[]" : await File.ReadAllTextAsync(path, ct);
            if (data.Length > 256000) throw new InvalidOperationException("Invalid store directory.");
            rows = JsonSerializer.Deserialize<StoreLocation[]>(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        if (rows.Length > 100 || rows.Any(x => x is null) || rows.Select(x => x.Id).Distinct().Count() != rows.Length || rows.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 60 || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 100 || string.IsNullOrWhiteSpace(x.City) || x.City.Length > 100 || !double.IsFinite(x.Latitude) || !double.IsFinite(x.Longitude) || Math.Abs(x.Latitude) > 85 || Math.Abs(x.Longitude) > 180 || x.Source is not ("demo" or "erp") || x.ProductIds is null)) throw new InvalidOperationException("Invalid store directory.");
        var knownStores = (await items.GetStoresAsync(ct)).Select(x => x.Id).ToHashSet();
        var result = new List<PublicStore>();
        foreach (var location in rows)
        {
            if (location.Source != "erp") continue;
            if (!int.TryParse(location.Id, out var id) || !knownStores.Contains(id)) { logger?.LogWarning("Skipped store location outside current business directory."); continue; }
            var count = (await AvailableItemIdsAsync(id, ct)).Count;
            var live = !sample && !location.IsSample && location.ErpEnabled && Fresh(location.InventorySyncedAt, DateTimeOffset.UtcNow) && Fresh(location.ImagesSyncedAt, DateTimeOffset.UtcNow);
            result.Add(new(location with { IsSample = sample || location.IsSample, ProductIds = [] }, live, count));
        }
        return result;
    }
    private async Task<StoreLocation[]> SqlLocations(CancellationToken ct)
    {
        await using var connection = new SqlConnection(configuration["NBJEWEL_SQL_CONNECTION"]);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("SELECT TOP (101) s.StoreId,s.Name,COALESCE(s.City,N'Unknown'),l.Latitude,l.Longitude,l.IsSample,l.ErpEnabled,l.InventorySyncedAt,l.ImagesSyncedAt FROM dbo.Store s JOIN dbo.StorefrontLocation l ON s.StoreId=l.StoreId ORDER BY s.StoreId", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var locations = new List<StoreLocation>();
        while (await reader.ReadAsync(ct)) locations.Add(new(reader.GetInt32(0).ToString(), reader.GetString(1), reader.GetString(2), reader.GetDouble(3), reader.GetDouble(4), "erp", reader.GetBoolean(5), reader.GetBoolean(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8), []));
        return locations.ToArray();
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
