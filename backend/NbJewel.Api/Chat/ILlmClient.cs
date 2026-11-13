// Role: The language model interface. The chat loop depends only on this, so tests can
// use a fake model and the real one (Gemini) can be swapped later. Tool results and
// tool arguments are plain JSON objects.

using System.Text.Json.Nodes;

namespace NbJewel.Api.Chat;

// RawPart is the model's own reply part. Gemini needs it sent back unchanged (thought signatures).
public sealed record LlmToolCall(string Name, JsonObject Args, JsonNode? RawPart = null);

// Role is "system", "user", "assistant" or "tool". Tool messages carry ToolName and ToolResult.
public sealed record LlmMessage(string Role, string Content = "", IReadOnlyList<LlmToolCall>? ToolCalls = null, string? ToolName = null, JsonObject? ToolResult = null);

public sealed record LlmUsage(int? InputTokens, int? OutputTokens, int? TotalTokens);
public sealed class ModelRetryBudget
{
    private int remaining = 1;
    private int omitThinking;
    public bool OmitThinking => Volatile.Read(ref omitThinking) == 1;
    public void MarkThinkingUnsupported() => Interlocked.Exchange(ref omitThinking, 1);
    public bool TryConsume() => Interlocked.Exchange(ref remaining, 0) == 1;
}
public sealed record LlmRequestSettings(string Model, int MaxOutputTokens, ModelRetryBudget? RetryBudget = null);
public sealed record LlmResponse(string? Text, IReadOnlyList<LlmToolCall> ToolCalls,
    LlmUsage? Usage = null, string? FinishReason = null);

// The message never contains the API key, prompts or model output.
public sealed class LlmException(string message) : Exception(message);

public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken cancellationToken);
    // Default preserves deterministic legacy fakes; real providers override to forward trusted settings.
    Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools,
        double temperature, LlmRequestSettings settings, CancellationToken cancellationToken)
        => GenerateAsync(messages, tools, temperature, cancellationToken);
}
