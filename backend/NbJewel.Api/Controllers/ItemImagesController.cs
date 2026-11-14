// Role: ERP-authorized item image upload, processing metadata and private preview reads.
// Generation does not publish the item; pending quality review and open licensing remain explicit.
using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/items/{itemId:int}/image")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ItemImagesController(IItemRepository items, IItemImageAssetRepository assets, ItemCutoutPipeline pipeline, IJewelryVision vision) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ItemImageAsset>> GetAsset(int itemId, CancellationToken ct)
    {
        if (await items.GetItemAsync(itemId, ct) is null) return NotFound();
        return await assets.GetAsync(itemId, ct) is { } asset ? Ok(asset) : NotFound();
    }

    [HttpPost]
    [RequestSizeLimit(UploadValidator.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadValidator.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<ItemImageAsset>> Upload(int itemId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > UploadValidator.MaxBytes)
            return BadRequest(new ErrorMessage("Choose a PNG or JPG image of at most 5 MB."));
        using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct);
        try
        {
            var asset = await pipeline.ProcessAsync(itemId, stream.ToArray(), ct);
            if (asset is null) return NotFound();
            return asset.State == "ready" ? Created($"/api/items/{itemId}/image", asset) : UnprocessableEntity(asset);
        }
        catch (DomainValidationException error) { return BadRequest(new ErrorMessage(error.Message)); }
    }

    public sealed record AnalyzeRequest(string RevisionId);
    [HttpPost("analyze")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public async Task<IActionResult> Analyze(int itemId, AnalyzeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RevisionId) || request.RevisionId.Length != 32) return BadRequest();
        if (await items.GetItemAsync(itemId, ct) is null) return NotFound();
        var bytes = await pipeline.PreviewAsync(itemId, ct, request.RevisionId);
        if (bytes is null) return Conflict(new ErrorMessage("The preview changed. Refresh before generating copy."));
        try {
            var result = await vision.AnalyzeAsync(bytes, ct);
            if ((await assets.GetAsync(itemId, ct))?.RevisionId != request.RevisionId) return Conflict(new ErrorMessage("The preview changed. Generate copy for the current image."));
            return Ok(new { revisionId = request.RevisionId, result.Description, result.Collection });
        }
        catch (VisionUnavailableException e) { return StatusCode(e.Status, new ErrorMessage(e.Message)); }
    }

    public sealed record ReviewRequest(string RevisionId, bool Approve, bool Confirm);
    [HttpPost("review")]
    public async Task<ActionResult<ItemImageAsset>> Review(int itemId, ReviewRequest request, CancellationToken ct)
    {
        if (!request.Confirm || string.IsNullOrWhiteSpace(request.RevisionId)) return BadRequest();
        if (await pipeline.PreviewAsync(itemId, ct) is null) return Conflict(new ErrorMessage("Generate a ready preview before reviewing it."));
        try { return await assets.ReviewAsync(itemId, request.RevisionId, request.Approve, ct) is { } asset ? Ok(asset) : NotFound(); }
        catch (ItemAssetConflictException) { return Conflict(new ErrorMessage("The image changed. Review the current preview.")); }
    }

    [HttpGet("preview")]
    public async Task<IActionResult> GetPreview(int itemId, string? revisionId, CancellationToken ct) =>
        await pipeline.PreviewAsync(itemId, ct, revisionId) is { } bytes ? File(bytes, "image/png") : NotFound();
}
