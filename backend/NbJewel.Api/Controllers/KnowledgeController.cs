// Role: Provides permission-checked administration of knowledge and product publication.
using System.Security.Claims;
using NbJewel.Api.Businesses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Identity;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Repositories;
using NbJewel.Api.Models;
namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/ai/knowledge")]
public sealed class KnowledgeController(KnowledgeDatabase db, KnowledgeService service, IItemRepository items, BusinessContext business) : ControllerBase
{
    public sealed record Confirmation(bool Confirm, string? VersionId = null, string? AssetRevisionId = null);
    private async Task<bool> Authorized(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return false;
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id)) return false;
        try { return await business.ResolveErpAsync(HttpContext, ct) && business.Access is { AccountType: "erp", AllStores: true } access && access.Permissions.Contains("knowledge.manage"); }
        catch (Exception e) when (e is not OperationCanceledException) { return false; }
    }
    [HttpGet("documents")]
    public async Task<IActionResult> List(CancellationToken ct)
    { if (!await Authorized(ct)) return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized(); try { return Ok(await db.Documents.AsNoTracking().Join(db.Versions.AsNoTracking(), d => d.LatestVersionId, v => v.Id, (d,v) => new { d.Id, d.PublishedVersionId, d.LatestVersionId, v.Title, v.Collection, v.Audience, v.RequiredPermission, v.StoreIds, v.OwnerId }).Take(1000).ToListAsync(ct)); } catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); } }
    [HttpPost("documents")]
    public async Task<IActionResult> Import(KnowledgeInput input, CancellationToken ct) => await Save(null, input, ct);
    [HttpPut("documents/{id}")]
    public async Task<IActionResult> Update(string id, KnowledgeInput input, CancellationToken ct) => await Save(id, input, ct);
    private async Task<IActionResult> Save(string? id, KnowledgeInput input, CancellationToken ct)
    { if (!await Authorized(ct)) return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized(); if (input?.Confirm != true) return BadRequest(); try { return Ok(new { id = await service.SaveAsync(id, input, ct, User.FindFirstValue(ClaimTypes.NameIdentifier)) }); } catch (ArgumentException) { return BadRequest(); } catch (KeyNotFoundException) { return NotFound(); } catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); } }
    [HttpPost("documents/{id}/publish")]
    public Task<IActionResult> Publish(string id, Confirmation confirmation, CancellationToken ct) => DocumentState(id, confirmation, true, ct);
    [HttpPost("documents/{id}/revoke")]
    public Task<IActionResult> Revoke(string id, Confirmation confirmation, CancellationToken ct) => DocumentState(id, confirmation, false, ct);
    private async Task<IActionResult> DocumentState(string id, Confirmation confirmation, bool published, CancellationToken ct)
    { if (!await Authorized(ct)) return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized(); if (confirmation?.Confirm != true) return BadRequest(); try { await service.SetPublishedAsync(id, published, ct, User.FindFirstValue(ClaimTypes.NameIdentifier), confirmation.VersionId); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } catch (ArgumentException) { return BadRequest(); } catch (KnowledgeVersionConflictException) { return Conflict(new { message = "The document changed after it was reviewed. Review the latest version and publish again." }); } catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); } }
    [HttpPost("products/{itemId:int}/publish")]
    public Task<IActionResult> PublishProduct(int itemId, Confirmation confirmation, CancellationToken ct) => ProductState(itemId, confirmation, true, ct);
    [HttpPost("products/{itemId:int}/revoke")]
    public Task<IActionResult> RevokeProduct(int itemId, Confirmation confirmation, CancellationToken ct) => ProductState(itemId, confirmation, false, ct);
    [HttpGet("products/{itemId:int}")]
    public async Task<IActionResult> ProductStatus(int itemId, CancellationToken ct)
    {
        if (!await Authorized(ct)) return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        try { var decision = await service.ProductDecisionAsync(itemId, ct); return Ok(new { itemId, published = (await service.PublishedItemIdsAsync(ct)).Contains(itemId), assetRevisionId = decision?.AssetRevisionId }); }
        catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); }
    }
    private async Task<IActionResult> ProductState(int id, Confirmation confirmation, bool published, CancellationToken ct)
    {
        if (!await Authorized(ct)) return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        if (confirmation?.Confirm != true) return BadRequest();
        try
        {
            if (published)
            {
                var item = await items.GetItemAsync(id, ct);
                if (item is null) return NotFound();
                var category = (await items.GetCategoriesAsync(ct)).FirstOrDefault(x => x.Id == item.CategoryId)?.Name;
                if (item.Status != ItemStatusRules.Complete || category is not ("Necklaces" or "Earrings") ||
                    item.Storefront is null || await items.GetPriceAsync(id, ct) is null || string.IsNullOrWhiteSpace(confirmation.AssetRevisionId))
                    return Conflict(new { message = "Complete the item, storefront description and current preview review before publishing." });
            }
            await service.SetProductAsync(id, published, ct, User.FindFirstValue(ClaimTypes.NameIdentifier), confirmation.AssetRevisionId);
            return NoContent();
        }
        catch (ProductNotReadyException) { return Conflict(new { message = "Review and approve the current ready preview before publishing." }); }
        catch (ArgumentException) { return BadRequest(); }
        catch (Exception e) when (e is not OperationCanceledException) { return StatusCode(503); }
    }
}
