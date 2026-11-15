// Role: Exposes item search, details, variants, stock, price, and write
// endpoints for the Angular client. Writes go through IItemRepository, which is
// memory-only by default and saves to SQL Server when that provider is chosen.
// Bad input returns 400, including an unknown status. Any known status can be
// set at any time, and choosing the current one is a harmless 200.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
using NbJewel.Api.Chat;
using NbJewel.Api.Identity;
using NbJewel.Api.Platform;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/items")]
public sealed class ItemsController(IItemRepository repository) : ControllerBase
{
    private AccountAccess? Access => HttpContext?.Items["ErpAccess"] as AccountAccess;
    private IChatDataReader Reader => Access is { } a
        ? new ScopedChatDataReader(repository, new("erp", a.Id, a.AccountType, a.Permissions, a.StoreIds, a.AllStores))
        : new ChatDataReader(repository);
    // Authenticated diagnostic for the local importer; never returns connection details.
    [HttpGet("storage")]
    public IActionResult GetStorage() => Ok(new { provider = repository is SqlItemRepository ? "Sql" : "Sample" });

    [HttpGet]
    public async Task<ActionResult<PagedResult<Item>>> GetItems(string? search, int? category, string? metal, string? purity, string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest("page must be at least 1 and pageSize must be between 1 and 100.");
        return Ok(await Reader.SearchItemsAsync(search, category, metal, purity, status, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Item>> GetItem(int id, CancellationToken cancellationToken) => (await Reader.GetItemAsync(id, cancellationToken)) is { } item ? Ok(item) : NotFound();

    [HttpGet("{id:int}/stock")]
    public async Task<ActionResult<IReadOnlyList<StockRecord>>> GetStock(int id, CancellationToken cancellationToken) => (await Reader.GetItemAsync(id, cancellationToken)) is null ? NotFound() : Ok(await Reader.GetStockAsync(id, cancellationToken));

    [HttpGet("{id:int}/variants")]
    public async Task<ActionResult<IReadOnlyList<ItemVariant>>> GetVariants(int id, CancellationToken cancellationToken) => (await Reader.GetItemAsync(id, cancellationToken)) is null ? NotFound() : Ok(await repository.GetVariantsAsync(id, cancellationToken));

    [HttpPost("{id:int}/stock")]
    public async Task<ActionResult<StockRecord>> AddStock(int id, StockAddRequest request, CancellationToken cancellationToken)
    {
        if (Access is { AllStores: false } a && (!a.StoreIds.Contains(request.StoreId) || await Reader.GetItemAsync(id, cancellationToken) is null)) return Forbid();
        try { return (await repository.AddStockAsync(id, request, cancellationToken)) is { } record ? Ok(record) : NotFound(); }
        catch (DomainValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:int}/price")]
    public async Task<ActionResult<ItemPrice>> GetPrice(int id, CancellationToken cancellationToken)
    {
        if (await Reader.GetItemAsync(id, cancellationToken) is null) return NotFound();
        var price = await Reader.GetPriceAsync(id, cancellationToken);
        return price is null ? UnprocessableEntity("No latest metal rate exists for this item.") : Ok(price);
    }

    [HttpPost]
    public async Task<ActionResult<Item>> Create(ItemCreateRequest request, CancellationToken cancellationToken)
    {
        try { var item = await repository.CreateAsync(request, cancellationToken); return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item); }
        catch (DomainValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Item>> Update(int id, ItemUpdateRequest request, CancellationToken cancellationToken)
    {
        try { return (await repository.UpdateAsync(id, request, cancellationToken)) is { } item ? Ok(item) : NotFound(); }
        catch (DomainValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<Item>> UpdateStatus(int id, StatusUpdateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Status)) return BadRequest("status is required.");
        try { return (await repository.UpdateStatusAsync(id, request.Status, cancellationToken)) is { } item ? Ok(item) : NotFound(); }
        catch (DomainValidationException ex) { return BadRequest(ex.Message); }
    }
}
