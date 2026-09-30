// Role: Exposes POST /api/chat. It checks the question, forwards it to the
// Python AI service, and returns the answer. The API holds no AI keys. If the
// AI service is down, slow, or fails, the caller gets 503 and a short message.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Models;

namespace NbJewel.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(IHttpClientFactory httpClientFactory, ILogger<ChatController> logger) : ControllerBase
{
    public const string ClientName = "AiService";
    private const int MaxQuestionLength = 500;

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Ask(ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > MaxQuestionLength)
            return BadRequest(new ErrorMessage($"Question must be 1 to {MaxQuestionLength} characters."));

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.PostAsJsonAsync("chat", request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                return BadRequest(new ErrorMessage("The question or history was not valid."));
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI service returned status {Status}.", (int)response.StatusCode);
                return Unavailable();
            }
            var body = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
            return body is null ? Unavailable() : Ok(body);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or NotSupportedException
            || (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A timeout surfaces as a cancelled request that the caller did not cancel.
            logger.LogWarning("AI service call failed: {ErrorType}.", error.GetType().Name);
            return Unavailable();
        }
    }

    private ObjectResult Unavailable() => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorMessage("Assistant unavailable. Retry."));
}
