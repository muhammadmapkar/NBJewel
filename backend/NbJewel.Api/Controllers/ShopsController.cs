// Role: Exposes only an online assumed portal publication and assets referenced by that exact live revision.
using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Businesses;
namespace NbJewel.Api.Controllers;
[ApiController, Route("api/shops/{slug}"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ShopsController(BusinessContext context, PortalStore store) : ControllerBase
{
    private Business Business => context.Business ?? throw new InvalidOperationException("Business context is missing.");
    private IActionResult Missing() { Response.StatusCode = 404; return new EmptyResult(); }
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var document = await store.ReadAsync(Business, ct);
        if (!document.Online || document.Publication is null) return Missing();
        return Ok(new { slug = Business.Slug, document.Publication.Revision, document.Publication.Settings });
    }
    [HttpGet("assets/{id}")]
    public async Task<IActionResult> GetAsset(string id, CancellationToken ct)
    {
        var document = await store.ReadAsync(Business, ct);
        if (!document.Online || document.Publication is null || (document.Publication.Settings.LogoAssetId != id && document.Publication.Settings.BannerAssetId != id)) return Missing();
        if (store.Asset(Business, id) is not { } path) return Missing();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, Path.GetExtension(path) == ".png" ? "image/png" : "image/jpeg");
    }
}
