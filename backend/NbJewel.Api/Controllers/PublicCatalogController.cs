// Role: Exposes anonymous, read-only storefront products and dated metal rates. Product reads
// recheck publication, supported category, metal and ERP completion status on every request.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Models;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Platform;
using NbJewel.Api.Repositories;
using NbJewel.Api.Storefront;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/storefront")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicCatalogController(IItemRepository items, IPublicationReader publications, IItemImageAssetRepository assets, ItemCutoutPipeline previews, StoreDirectory stores) : ControllerBase
{
    // Explicit DTO prevents ERP status, hallmark IDs, design links, stock and store fields leaking.
    public sealed record PublicProduct(int Id, string Code, string Name, string Category,
        string MetalType, string Purity, decimal GrossWeight, decimal NetWeight,
        string? ImageUrl, StorefrontMetadata? Storefront, ItemPrice? Price, string? PreviewUrl, decimal? PreviewAspect);

    [HttpGet("spot-rates")]
    public async Task<IActionResult> GetSpotRates([FromServices] SpotRates spot, CancellationToken ct) => Ok(await spot.ReadAsync(ct));

    [HttpGet("stores")]
    public async Task<IActionResult> GetStores(CancellationToken ct)
    {
        try { return Ok(await stores.ReadAsync(ct)); }
        catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); }
    }

    [HttpGet("products")]
    public async Task<ActionResult<PagedResult<PublicProduct>>> GetProducts(string? search, string? metal,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default, int? storeId = null)
    {
        if (page < 1 || pageSize is < 1 or > 100 || search?.Length > 100 || metal?.Length > 50)
            return BadRequest("Invalid catalog query or paging.");
        try
        {
            var published = await publications.PublishedItemRevisionsAsync(cancellationToken);
            if (published.Count > 10000) return StatusCode(503);
            var categories = await PublicCatalogPolicy.AllowedCategoriesAsync(items, cancellationToken);
            IReadOnlySet<int>? storeItems = null;
            if (storeId.HasValue) {
                if (!(await stores.ReadAsync(cancellationToken)).Any(x => x.Location.Source == "erp" && x.Location.Id == storeId.Value.ToString())) return BadRequest("Unknown store.");
                storeItems = await stores.AvailableItemIdsAsync(storeId.Value, cancellationToken);
            }
            var matches = new List<Item>();
            for (var current = 1; ; current++)
            {
                var batch = await items.SearchItemsAsync(search, null, metal, null, ItemStatusRules.Complete,
                    current, 100, cancellationToken);
                if (batch.TotalCount > 10000) return StatusCode(503);
                matches.AddRange(batch.Items.Where(item => (storeItems is null || storeItems.Contains(item.Id)) && published.ContainsKey(item.Id) && PublicCatalogPolicy.Allows(item, categories) &&
                    (string.IsNullOrWhiteSpace(search) || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        item.Code.Contains(search, StringComparison.OrdinalIgnoreCase))));
                if ((long)current * 100 >= batch.TotalCount) break;
                if (batch.Items.Count == 0) return StatusCode(503);
            }
            var shown = matches.OrderBy(x => x.Id)
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize);
            var products = new List<PublicProduct>();
            foreach (var item in shown)
            {
                var product = await PublicItem(item, categories[item.CategoryId], published[item.Id], cancellationToken);
                if (product is null) return StatusCode(503); // Refresh a coherent snapshot after a concurrent replacement.
                products.Add(product);
            }
            return Ok(new PagedResult<PublicProduct>(products, page, pageSize, matches.Count));
        }
        catch (Exception error) when (error is not OperationCanceledException) { return StatusCode(503); }
    }

    [HttpGet("products/{id:int}")]
    public async Task<ActionResult<PublicProduct>> GetProduct(int id, CancellationToken cancellationToken)
    {
        if (id < 1) return NotFound();
        try
        {
            var published = await publications.PublishedItemRevisionsAsync(cancellationToken);
            if (!published.ContainsKey(id)) return NotFound();
            var item = await items.GetItemAsync(id, cancellationToken);
            var categories = await PublicCatalogPolicy.AllowedCategoriesAsync(items, cancellationToken);
            if (item is null || !PublicCatalogPolicy.Allows(item, categories)) return NotFound();
            var product = await PublicItem(item, categories[item.CategoryId], published[id], cancellationToken);
            return product is null ? NotFound() : Ok(product);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return StatusCode(503); }
    }

    [HttpGet("metal-rates")]
    public async Task<ActionResult<IReadOnlyList<MetalRate>>> GetMetalRates(string? metal, CancellationToken cancellationToken)
    {
        if (metal?.Length > 50) return BadRequest("Invalid metal filter.");
        try
        {
            var rates = await items.GetLatestRatesAsync(cancellationToken);
            return Ok(rates.Where(x => PublicCatalogPolicy.AllowedMetal(x.MetalType) &&
                (string.IsNullOrWhiteSpace(metal) || x.MetalType.Equals(metal, StringComparison.OrdinalIgnoreCase))).ToArray());
        }
        catch (Exception error) when (error is not OperationCanceledException) { return StatusCode(503); }
    }

    [HttpGet("products/{id:int}/preview")]
    public async Task<IActionResult> GetPreview(int id, string? v, CancellationToken ct)
    {
        try
        {
            var published = await publications.PublishedItemRevisionsAsync(ct);
            if (!published.TryGetValue(id, out var revision) || revision is null || (v is not null && v != revision)) return NotFound();
            var item = await items.GetItemAsync(id, ct);
            if (item is null || !PublicCatalogPolicy.Allows(item, await PublicCatalogPolicy.AllowedCategoriesAsync(items, ct))) return NotFound();
            return await previews.PreviewAsync(id, ct, revision) is { } bytes ? File(bytes, "image/png") : NotFound();
        }
        catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); }
    }

    private async Task<PublicProduct?> PublicItem(Item item, string category, string? revision, CancellationToken ct)
    {
        var asset = await assets.GetAsync(item.Id, ct);
        if (revision is null || asset is not { State: "ready", QualityReview: "approved" } || asset.RevisionId != revision) return null;
        return new(item.Id, item.Code, item.Name, category, item.MetalType, item.Purity,
            item.GrossWt, item.NetWt, item.ImageUrl, item.Storefront, await items.GetPriceAsync(item.Id, ct),
            asset is null ? null : $"/api/storefront/products/{item.Id}/preview?v={asset.RevisionId}",
            asset is { Width: > 0 } ? (decimal)asset.Height / asset.Width : null);
    }
}
