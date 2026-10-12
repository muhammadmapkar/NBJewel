// Role: Exposes categories, stores, designs, latest metal rates, and the read-only
// low-stock list for API consumers.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ReferenceDataController(IItemRepository repository) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<Category>>> GetCategories(CancellationToken cancellationToken) => Ok(await repository.GetCategoriesAsync(cancellationToken));
    [HttpGet("stores")]
    public async Task<ActionResult<IReadOnlyList<Store>>> GetStores(CancellationToken cancellationToken) => Ok(await repository.GetStoresAsync(cancellationToken));
    [HttpGet("designs")]
    public async Task<ActionResult<IReadOnlyList<Design>>> GetDesigns(CancellationToken cancellationToken) => Ok(await repository.GetDesignsAsync(cancellationToken));
    [HttpGet("metal-rates/latest")]
    public async Task<ActionResult<IReadOnlyList<MetalRate>>> GetLatestRates(CancellationToken cancellationToken) => Ok(await repository.GetLatestRatesAsync(cancellationToken));
    // Stock rows with fewer than "threshold" units available. The default of 3 is assumed.
    [HttpGet("stock/low")]
    public async Task<ActionResult<IReadOnlyList<StockRecord>>> GetLowStock(int threshold = 3, CancellationToken cancellationToken = default) =>
        threshold is < 1 or > 1000 ? BadRequest("threshold must be between 1 and 1000.") : Ok(await repository.GetLowStockAsync(threshold, cancellationToken));
}
