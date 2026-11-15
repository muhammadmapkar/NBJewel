// Role: Exposes assumed membership-checked draft, asset, authenticated preview and publish operations using fixed portal permissions.
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Businesses;
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Controllers;
[ApiController, Route("api/businesses/{slug}/portal"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PortalController(BusinessContext context, PortalStore store, PortalService service) : ControllerBase
{
    public sealed record DraftInput(long Revision, PortalSettings Settings);
    public sealed record RevisionInput(long Revision);
    private Business Business => context.Business ?? throw new InvalidOperationException("Business context is missing.");
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Actor is missing.");
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> operation)
    {
        Response.Headers.CacheControl = "no-store";
        try { return await operation(); }
        catch (PortalConflictException) { return Conflict(new { message = "The draft changed. Reload the latest revision and keep your edits before trying again." }); }
        catch (PortalValidationException error) { return BadRequest(new { message = error.Message }); }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException) { return StatusCode(503, new { message = "Portal storage is unavailable. Try again." }); }
    }
    private static object DraftView(PortalDocument document) => new { document.Draft.Revision, document.Draft.Settings, document.Online, publishedRevision = document.Publication?.Revision, document.Audit };
    [HttpGet("draft")]
    public Task<IActionResult> Draft(CancellationToken ct) => Execute(async () => Ok(DraftView(await store.ReadAsync(Business, ct))));
    [HttpPut("draft")]
    public Task<IActionResult> SaveDraft(DraftInput input, CancellationToken ct) => Execute(async () => Ok(DraftView(await service.SaveAsync(Business, input.Revision, input.Settings, Actor, ct))));
    [HttpPost("publish")]
    public Task<IActionResult> Publish(RevisionInput input, CancellationToken ct) => Execute(async () => Ok(DraftView(await service.PublishAsync(Business, input.Revision, Actor, true, ct))));
    [HttpPost("offline")]
    public Task<IActionResult> Offline(RevisionInput input, CancellationToken ct) => Execute(async () => Ok(DraftView(await service.PublishAsync(Business, input.Revision, Actor, false, ct))));
    [HttpPost("assets"), RequestSizeLimit(5308416), RequestFormLimits(MultipartBodyLengthLimit = 5308416)]
    public Task<IActionResult> UploadAsset(IFormFile? file, CancellationToken ct) => Execute(async () => {
        if (file is null || file.Length is 0 or > UploadValidator.MaxBytes) return BadRequest(new { message = "Choose a PNG or JPG image of at most 5 MB." });
        using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct);
        var id = await store.UploadAsync(Business, memory.ToArray(), ct);
        return StatusCode(201, new { id });
    });
    [HttpGet("assets/{id}")]
    public IActionResult GetAsset(string id) => store.Asset(Business, id) is { } path ? AssetFile(path) : NotFound();
    private IActionResult AssetFile(string path) { Response.Headers["X-Content-Type-Options"] = "nosniff"; return PhysicalFile(path, Path.GetExtension(path) == ".png" ? "image/png" : "image/jpeg"); }
    private PublicCatalogController Catalog()
    {
        var catalog = ActivatorUtilities.CreateInstance<PublicCatalogController>(HttpContext.RequestServices);
        catalog.ControllerContext = ControllerContext; return catalog;
    }
    [HttpGet("preview")]
    public Task<IActionResult> Preview(CancellationToken ct, int page = 1) => Execute(async () => {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        var draft = (await store.ReadAsync(Business, ct)).Draft;
        var products = await Catalog().GetProducts(null, null, page, 100, ct);
        if (products.Result is not OkObjectResult ok) return products.Result ?? StatusCode(503);
        return Ok(new { draft.Revision, draft.Settings, preview = true, products = ok.Value });
    });
    [HttpGet("products/{id:int}/preview")]
    public Task<IActionResult> ProductPreview(int id, string? v, CancellationToken ct) => Catalog().GetPreview(id, v, ct);
}
