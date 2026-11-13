// Role: Exposes POST /api/chat. It checks the question and history, then asks the
// ChatOrchestrator (the chatbot that now runs inside this API) and returns the answer.
// The request and response JSON are the same as before, so the Angular app is unchanged.
// If the model is missing, slow or fails, the caller gets 503 and a short message.
// The Gemini key is read only from the environment and never leaves the backend.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Chat;
using NbJewel.Api.Models;
using NbJewel.Api.Platform;
using Microsoft.EntityFrameworkCore;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(ILegacyAssistant gateway, ILogger<ChatController> logger) : ControllerBase
{
    public const int MaxQuestionLength = 500;
    public const int MaxHistoryMessages = 50;
    public const int MaxHistoryContentLength = 2000;
    // One question may use several model calls. This caps the whole question, not one call.
    public const int QuestionDeadlineSeconds = 45;
    public const string BusyMessage = "The assistant is busy. Please try again.";

    // Tests shorten this. The app always uses the default.
    public TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(QuestionDeadlineSeconds);

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Ask(ChatRequest request, CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim() ?? "";
        if (question.Length is 0 or > MaxQuestionLength)
            return BadRequest(new ErrorMessage($"Question must be 1 to {MaxQuestionLength} characters."));
        if (!HistoryIsValid(request.History))
            return BadRequest(new ErrorMessage("The question or history was not valid."));

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Deadline);
        try
        {
            // Browser history is untrusted. Only the current question reaches the model.
            return Ok(await gateway.AskAsync(HttpContext, question, deadline.Token));
        }
        catch (AssistantDeniedException) { return StatusCode(403); }
        catch (AssistantBusyException) { return StatusCode(429); }
        catch (AssistantConflictException) { return StatusCode(409); }
        catch (Exception error) when (error is AssistantUnavailableException or DbUpdateException or InvalidOperationException or System.Data.Common.DbException)
        { logger.LogWarning("Assistant call failed: {ErrorType}", error.GetType().Name); return StatusCode(503, new ErrorMessage(BusyMessage)); }
        catch (Exception error) when (error is LlmException || (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A timeout or the question deadline surfaces as a cancelled request that the caller did not cancel.
            logger.LogWarning("Assistant call failed: {ErrorType}.", error.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorMessage(BusyMessage));
        }
    }

    private static bool HistoryIsValid(List<ChatHistoryMessage>? history) =>
        history is null || (history.Count <= MaxHistoryMessages
            && history.All(m => m is { Role: "user" or "assistant" } && m.Content is not null && m.Content.Length <= MaxHistoryContentLength));
}
