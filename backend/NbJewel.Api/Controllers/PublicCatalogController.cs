// Role: Exposes anonymous, read-only storefront products and dated metal rates. Product reads
// recheck publication, supported category, metal and ERP completion status on every request.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Models;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Platform;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/storefront")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicCatalogController(IItemRepository items, IPublicationReader publications, IItemImageAssetRepository assets, ItemCutoutPipeline previews) : ControllerBase
{
    // Assumed demo policy, shared with the customer storefront's supported try-on kinds.
    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Necklaces"] = "necklace", ["Earrings"] = "earrings",
        ["Sets"] = "set", ["Maang tikka"] = "tikka"
    };

    // Explicit DTO prevents ERP status, hallmark IDs, design links, stock and store fields leaking.
    public sealed record PublicProduct(int Id, string Code, string Name, string Category,
        string MetalType, string Purity, decimal GrossWeight, decimal NetWeight,
        string? ImageUrl, StorefrontMetadata? Storefront, ItemPrice? Price, string? PreviewUrl, decimal? PreviewAspect);

    [HttpGet("products")]
    public async Task<ActionResult<PagedResult<PublicProduct>>> GetProducts(string? search, string? metal,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || search?.Length > 100 || metal?.Length > 50)
            return BadRequest("Invalid catalog query or paging.");
        try
        {
            var published = await publications.PublishedItemIdsAsync(cancellationToken);
            if (published.Count > 10000) return StatusCode(503);
            var categories = await AllowedCategories(cancellationToken);
            var matches = new List<Item>();
            for (var current = 1; ; current++)
            {
                var batch = await items.SearchItemsAsync(search, null, metal, null, ItemStatusRules.Complete,
                    current, 100, cancellationToken);
                if (batch.TotalCount > 10000) return StatusCode(503);
                matches.AddRange(batch.Items.Where(item => published.Contains(item.Id) && Allowed(item, categories) &&
                    (string.IsNullOrWhiteSpace(search) || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        item.Code.Contains(search, StringComparison.OrdinalIgnoreCase))));
                if ((long)current * 100 >= batch.TotalCount) break;
                if (batch.Items.Count == 0) return StatusCode(503);
            }
            var shown = matches.OrderBy(x => x.Id)
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize);
            var products = new List<PublicProduct>();
            foreach (var item in shown) products.Add(await PublicItem(item, categories[item.CategoryId], cancellationToken));
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
            var published = await publications.PublishedItemIdsAsync(cancellationToken);
            if (!published.Contains(id)) return NotFound();
            var item = await items.GetItemAsync(id, cancellationToken);
            var categories = await AllowedCategories(cancellationToken);
            return item is null || !Allowed(item, categories)
                ? NotFound() : Ok(await PublicItem(item, categories[item.CategoryId], cancellationToken));
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
            return Ok(rates.Where(x => AllowedMetal(x.MetalType) &&
                (string.IsNullOrWhiteSpace(metal) || x.MetalType.Equals(metal, StringComparison.OrdinalIgnoreCase))).ToArray());
        }
        catch (Exception error) when (error is not OperationCanceledException) { return StatusCode(503); }
    }

    [HttpGet("products/{id:int}/preview")]
    public async Task<IActionResult> GetPreview(int id, CancellationToken ct)
    {
        try
        {
            if (!(await publications.PublishedItemIdsAsync(ct)).Contains(id)) return NotFound();
            var item = await items.GetItemAsync(id, ct);
            if (item is null || !Allowed(item, await AllowedCategories(ct))) return NotFound();
            return await previews.PreviewAsync(id, ct) is { } bytes ? File(bytes, "image/png") : NotFound();
        }
        catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); }
    }

    private async Task<Dictionary<int, string>> AllowedCategories(CancellationToken ct) =>
        (await items.GetCategoriesAsync(ct)).Where(x => Categories.ContainsKey(x.Name))
            .ToDictionary(x => x.Id, x => Categories[x.Name]);

    private static bool AllowedMetal(string metal) =>
        metal.Equals("Gold", StringComparison.OrdinalIgnoreCase) ||
        metal.Equals("Silver", StringComparison.OrdinalIgnoreCase) ||
        metal.Equals("Platinum", StringComparison.OrdinalIgnoreCase);

    private static bool Allowed(Item item, IReadOnlyDictionary<int, string> categories) =>
        item.Status.Equals(ItemStatusRules.Complete, StringComparison.OrdinalIgnoreCase) &&
        categories.ContainsKey(item.CategoryId) && AllowedMetal(item.MetalType);

    private async Task<PublicProduct> PublicItem(Item item, string category, CancellationToken ct)
    {
        var asset = await assets.GetAsync(item.Id, ct);
        return new(item.Id, item.Code, item.Name, category, item.MetalType, item.Purity,
            item.GrossWt, item.NetWt, item.ImageUrl, item.Storefront, await items.GetPriceAsync(item.Id, ct),
            asset is null ? null : $"/api/storefront/products/{item.Id}/preview?v={asset.RevisionId}",
            asset is { Width: > 0 } ? (decimal)asset.Height / asset.Width : null);
    }
}
