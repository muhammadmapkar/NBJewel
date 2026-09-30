// Role: Exposes categories, stores, and latest metal rates for API consumers.

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
    [HttpGet("metal-rates/latest")]
    public async Task<ActionResult<IReadOnlyList<MetalRate>>> GetLatestRates(CancellationToken cancellationToken) => Ok(await repository.GetLatestRatesAsync(cancellationToken));
}
