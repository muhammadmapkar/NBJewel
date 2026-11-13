// Role: Exposes separate ERP and public conversation APIs with server identity, ownership checks and safe failures.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Chat;
using NbJewel.Api.Platform;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/ai/{assistant}/conversations")]
[EnableRateLimiting("ai-ip")]
[RequestSizeLimit(16384)]
public sealed class AiConversationsController(AssistantAccess access, ConversationService conversations,
    ILogger<AiConversationsController> logger) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Create(string assistant, CancellationToken ct) => Execute(assistant, async context =>
        StatusCode(201, await conversations.CreateAsync(context, ct)), ct);

    [HttpGet("{id}/messages")]
    public Task<IActionResult> Messages(string assistant, string id, CancellationToken ct) => Execute(assistant,
        async context => Ok(await conversations.MessagesAsync(context, id, ct)), ct);

    [HttpPost("{id}/messages")]
    public Task<IActionResult> Send(string assistant, string id, MessageRequest request, CancellationToken ct) => Execute(assistant,
        async context => Ok(await conversations.SendAsync(context, id, request, ct)), ct);

    [HttpDelete("{id}")]
    public Task<IActionResult> Delete(string assistant, string id, CancellationToken ct) => Execute(assistant, async context =>
    { await conversations.DeleteAsync(context, id, ct); return NoContent(); }, ct, forDeletion: true);

    private async Task<IActionResult> Execute(string assistant, Func<AssistantContext, Task<IActionResult>> action, CancellationToken ct, bool forDeletion = false)
    {
        Response.Headers.CacheControl = "no-store";
        try { return await action(forDeletion ? access.ResolveForDeletion(assistant, HttpContext) : await access.ResolveAsync(assistant, HttpContext, ct)); }
        catch (AssistantDeniedException) { return StatusCode(User.Identity?.IsAuthenticated == true ? 403 : 401); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest(new { message = "The request was not valid." }); }
        catch (AssistantConflictException) { return Conflict(new { message = "This conversation is busy or the request ID was reused with different text." }); }
        catch (AssistantBusyException) { Response.Headers.RetryAfter = "60"; return StatusCode(429, new { message = "Assistant limit reached. Try again later." }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        catch (System.Security.Cryptography.CryptographicException)
        { return StatusCode(410, new { code = "conversation_unreadable", message = "This conversation cannot be recovered. Start a new chat or delete it." }); }
        catch (Exception error) when (error is LlmException or AssistantUnavailableException or DbUpdateException or InvalidOperationException or System.Data.Common.DbException)
        { logger.LogWarning("Assistant request failed: {ErrorType}", error.GetType().Name); return Unavailable(); }
    }
    private ObjectResult Unavailable() => StatusCode(503, new { message = "The assistant is unavailable. Please try again." });
}
