// Role: Enforces real customer sessions and ownership for profile, bag and reservation requests. No payment or inventory hold is implied.
using System.ComponentModel.DataAnnotations;
using NbJewel.Api.Businesses;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Identity;
using NbJewel.Api.Models;
using NbJewel.Api.Platform;
using NbJewel.Api.Repositories;
using NbJewel.Api.Storefront;
namespace NbJewel.Api.Controllers;
[ApiController, Authorize, Route("api/customer"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerPortalController(IPermissionAdapter permissions, CustomerPortal portal, StoreDirectory stores, IItemRepository items, IPublicationReader publications, BusinessContext business) : ControllerBase
{
    private string Slug => business.Business?.Slug ?? throw new InvalidOperationException("Business context is missing.");
    public sealed record ProfileInput([Required, MaxLength(100)] string Name, [MaxLength(30)] string Phone);
    public sealed record BagInput([Required, MaxLength(100)] string Id, [Required, RegularExpression("^(demo|erp)$")] string Source);
    public sealed record ReserveInput([Required, MaxLength(36)] string RequestId, [Required, MaxLength(60)] string StoreId, [Required, RegularExpression("^(demo|erp)$")] string Source, [Required, MaxLength(100)] string Name, [Required, MaxLength(30)] string Phone, DateOnly Date, [Required, MinLength(1), MaxLength(50)] string[] Items);
    private async Task<IActionResult> Owned(Func<string, Task<IActionResult>> action, CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null) return Unauthorized();
        var account = await permissions.ResolveAsync(id, ct);
        if (account?.AccountType != "customer") return Forbid();
        try { return await action(id); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Your account changed in another tab. Refresh and try again." }); }
        catch (Exception e) when (e is DbUpdateException or InvalidOperationException or IOException or JsonException or AssistantUnavailableException)
        { return StatusCode(503, new { message = "Customer services are unavailable. Please try again." }); }
    }
    [HttpGet("/api/customer/shops/{slug}/state")]
    public Task<IActionResult> State(CancellationToken ct) => Owned(async id => Ok((await portal.ReadAsync(id, ct, Slug)).State), ct);
    [HttpGet("profile")]
    public Task<IActionResult> Get(CancellationToken ct) => Owned(async id => Ok((await portal.ReadAsync(id, ct, Slug)).State), ct);
    [HttpPut("profile")]
    public Task<IActionResult> Profile(ProfileInput input, CancellationToken ct) => Owned(async id => {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Phone is null) return BadRequest();
        var (row, state) = await portal.ReadAsync(id, ct, Slug); state = state with { Name = input.Name.Trim(), Phone = input.Phone.Trim() };
        await portal.SaveAsync(id, row, state, ct, Slug); return Ok(state);
    }, ct);
    [HttpPost("bag")]
    [HttpPost("/api/customer/shops/{slug}/bag")]
    public Task<IActionResult> Add(BagInput input, CancellationToken ct) => Owned(async id => {
        if (!await ProductExists(input.Id, input.Source, ct)) return NotFound(new { message = "This product is no longer available." });
        var (row, state) = await portal.ReadAsync(id, ct, Slug);
        if (!state.Bag.Contains(new(input.Id, input.Source))) { if (state.Bag.Count >= 50) return BadRequest(new { message = "Your bag is full." }); state.Bag.Add(new(input.Id, input.Source)); }
        await portal.SaveAsync(id, row, state, ct, Slug); return Ok(state);
    }, ct);
    [HttpDelete("bag/{source}/{productId}")]
    [HttpDelete("/api/customer/shops/{slug}/bag/{productId}")]
    public Task<IActionResult> Remove(string? source, string productId, CancellationToken ct) => Owned(async id => {
        var (row, state) = await portal.ReadAsync(id, ct, Slug); state.Bag.RemoveAll(x => x.Id == productId && x.Source == (source ?? "erp"));
        await portal.SaveAsync(id, row, state, ct, Slug); return Ok(state);
    }, ct);
    [HttpPost("reservations")]
    [HttpPost("/api/customer/shops/{slug}/reservations")]
    public Task<IActionResult> Reserve(ReserveInput input, CancellationToken ct) => Owned(async id => {
        if (!Guid.TryParse(input.RequestId, out _) || input.Date < DateOnly.FromDateTime(DateTime.UtcNow) || input.Date > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(180) || input.Phone.Count(char.IsDigit) < 7 || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { message = "Check your contact details and visit date." });
        var (row, state) = await portal.ReadAsync(id, ct, Slug);
        var existing = state.Reservations.FirstOrDefault(x => x.RequestId == input.RequestId);
        if (existing is not null) {
            if (existing.StoreId != input.StoreId || existing.Source != input.Source || existing.Date != input.Date.ToString("yyyy-MM-dd") || existing.Name != input.Name.Trim() || existing.Phone != input.Phone.Trim() || !existing.Items.Order().SequenceEqual(input.Items.Order())) return Conflict();
            return Ok(state);
        }
        var store = (await stores.ReadAsync(ct)).FirstOrDefault(x => x.Location.Id == input.StoreId && x.Location.Source == input.Source);
        if (store is null) return BadRequest(new { message = "Choose an available store." });
        var bag = state.Bag.Where(x => x.Source == input.Source && input.Items.Contains(x.Id)).ToArray();
        if (bag.Length != input.Items.Length || input.Items.Distinct().Count() != input.Items.Length) return BadRequest(new { message = "Choose designs from your own bag." });
        if (bag.Length == 0 || state.Reservations.Count >= 100) return BadRequest(new { message = "Add an available product to your bag first." });
        var available = input.Source == "erp" ? await stores.AvailableItemIdsAsync(int.Parse(input.StoreId), ct) : null;
        foreach (var entry in bag)
            if (!await ProductExists(entry.Id, entry.Source, ct) || (available is not null ? !int.TryParse(entry.Id, out var itemId) || !available.Contains(itemId) : !store.Location.ProductIds.Contains(entry.Id))) return Conflict(new { message = "A product is unavailable at this store. Refresh your bag." });
        state.Reservations.Insert(0, new("NBJ-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), input.RequestId, input.Name.Trim(), input.Phone.Trim(), store.Location.Name, input.StoreId, input.Source, input.Date.ToString("yyyy-MM-dd"), bag.Select(x => x.Id).ToArray(), DateTimeOffset.UtcNow));
        state.Bag.RemoveAll(x => x.Source == input.Source && input.Items.Contains(x.Id)); state = state with { Name = input.Name.Trim(), Phone = input.Phone.Trim() };
        await portal.SaveAsync(id, row, state, ct, Slug); return Ok(state);
    }, ct);
    private async Task<bool> ProductExists(string id, string source, CancellationToken ct)
    {
        if (source != "erp" || !int.TryParse(id, out var number) || !(await publications.PublishedItemIdsAsync(ct)).Contains(number)) return false;
        var item = await items.GetItemAsync(number, ct);
        return item is not null && PublicCatalogPolicy.Allows(item, await PublicCatalogPolicy.AllowedCategoriesAsync(items, ct));
    }
}
