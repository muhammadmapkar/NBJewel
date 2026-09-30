// Role: Exposes item search, details, stock, price, and memory-only write
// endpoints for the future Angular client.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/items")]
public sealed class ItemsController(IItemRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<Item>>> GetItems(string? search, int? category, string? metal, string? purity, string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest("page must be at least 1 and pageSize must be between 1 and 100.");
        return Ok(await repository.SearchItemsAsync(search, category, metal, purity, status, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Item>> GetItem(int id, CancellationToken cancellationToken) => (await repository.GetItemAsync(id, cancellationToken)) is { } item ? Ok(item) : NotFound();

    [HttpGet("{id:int}/stock")]
    public async Task<ActionResult<IReadOnlyList<StockRecord>>> GetStock(int id, CancellationToken cancellationToken) => (await repository.GetItemAsync(id, cancellationToken)) is null ? NotFound() : Ok(await repository.GetStockAsync(id, cancellationToken));

    [HttpGet("{id:int}/price")]
    public async Task<ActionResult<ItemPrice>> GetPrice(int id, CancellationToken cancellationToken)
    {
        if (await repository.GetItemAsync(id, cancellationToken) is null) return NotFound();
        var price = await repository.GetPriceAsync(id, cancellationToken);
        return price is null ? UnprocessableEntity("No latest metal rate exists for this item.") : Ok(price);
    }

    [HttpPost]
    public async Task<ActionResult<Item>> Create(ItemCreateRequest request, CancellationToken cancellationToken) { var item = await repository.CreateAsync(request, cancellationToken); return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item); }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Item>> Update(int id, ItemUpdateRequest request, CancellationToken cancellationToken) => (await repository.UpdateAsync(id, request, cancellationToken)) is { } item ? Ok(item) : NotFound();

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<Item>> UpdateStatus(int id, StatusUpdateRequest request, CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(request.Status) ? BadRequest("status is required.") : (await repository.UpdateStatusAsync(id, request.Status, cancellationToken)) is { } item ? Ok(item) : NotFound();
}
