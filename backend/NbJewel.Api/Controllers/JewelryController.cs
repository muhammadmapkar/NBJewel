// Role: HTTP endpoints for the customer try-on jewelry (not the ERP catalog, not the chatbot).
// GET /api/jewelry is public and lists active items only. The owner endpoints (list all,
// upload, toggle active, delete) need the X-Owner-Pin header to match OWNER_PIN
// (assumed, replace with real auth later) and are rate limited to slow down PIN guessing.
// Images are served as static files under /jewelry-images, set up in Program.cs.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Models;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/jewelry")]
public sealed class JewelryController(IJewelryRepository repository, OwnerPin ownerPin) : ControllerBase
{
    // A little over 5 MB so the form fields fit; UploadValidator enforces the real 5 MB file limit.
    private const long RequestLimit = UploadValidator.MaxBytes + 64 * 1024;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JewelryItem>>> GetActive(CancellationToken cancellationToken) =>
        Ok((await repository.ListAsync(activeOnly: true, cancellationToken)).Select(ToItem).ToList());

    [HttpGet("all")]
    [EnableRateLimiting(OwnerPin.RateLimitPolicy)]
    public async Task<ActionResult<IReadOnlyList<JewelryItem>>> GetAll([FromHeader(Name = OwnerPin.HeaderName)] string? pin, CancellationToken cancellationToken)
    {
        if (PinFailure(pin) is { } failure) return failure;
        return Ok((await repository.ListAsync(activeOnly: false, cancellationToken)).Select(ToItem).ToList());
    }

    [HttpPost]
    [EnableRateLimiting(OwnerPin.RateLimitPolicy)]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    public async Task<ActionResult<JewelryItem>> Upload([FromHeader(Name = OwnerPin.HeaderName)] string? pin, [FromForm] string? name, [FromForm] string? type, IFormFile? file, CancellationToken cancellationToken)
    {
        if (PinFailure(pin) is { } failure) return failure;
        if (file is null) return BadRequest(new ErrorMessage("Choose an image file."));
        if (file.Length > UploadValidator.MaxBytes) return BadRequest(new ErrorMessage("The image is larger than 5 MB."));
        byte[] content;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }
        var check = UploadValidator.Check(name, type, file.Length, content);
        if (!check.Ok) return BadRequest(new ErrorMessage(check.Error!));
        var record = await repository.AddAsync(check.Name!, check.Type!, content, check.Extension!, cancellationToken);
        return Created($"/api/jewelry/{record.Id}", ToItem(record));
    }

    [HttpPatch("{id}/active")]
    [EnableRateLimiting(OwnerPin.RateLimitPolicy)]
    public async Task<ActionResult<JewelryItem>> SetActive(string id, JewelryActiveRequest request, [FromHeader(Name = OwnerPin.HeaderName)] string? pin, CancellationToken cancellationToken)
    {
        if (PinFailure(pin) is { } failure) return failure;
        return await repository.SetActiveAsync(id, request.Active, cancellationToken) is { } record ? Ok(ToItem(record)) : NotFound();
    }

    [HttpDelete("{id}")]
    [EnableRateLimiting(OwnerPin.RateLimitPolicy)]
    public async Task<IActionResult> Delete(string id, [FromHeader(Name = OwnerPin.HeaderName)] string? pin, CancellationToken cancellationToken)
    {
        if (PinFailure(pin) is { } failure) return failure;
        return await repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    private ObjectResult? PinFailure(string? pin) => ownerPin.Check(pin) switch
    {
        PinResult.Ok => null,
        PinResult.NotConfigured => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorMessage("The owner PIN is not set on the server (OWNER_PIN).")),
        _ => StatusCode(StatusCodes.Status401Unauthorized, new ErrorMessage("Wrong PIN."))
    };

    private static JewelryItem ToItem(JewelryRecord record) =>
        new(record.Id, record.Name, record.Type, $"{JewelryStorageOptions.ImagesRequestPath}/{record.File}", record.Active, record.CreatedAt);
}
