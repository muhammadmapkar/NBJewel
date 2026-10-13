// Role: The language model interface. The chat loop depends only on this, so tests can
// use a fake model and the real one (Gemini) can be swapped later. Tool results and
// tool arguments are plain JSON objects.

using System.Text.Json.Nodes;

namespace NbJewel.Api.Chat;

// RawPart is the model's own reply part. Gemini needs it sent back unchanged (thought signatures).
public sealed record LlmToolCall(string Name, JsonObject Args, JsonNode? RawPart = null);

// Role is "system", "user", "assistant" or "tool". Tool messages carry ToolName and ToolResult.
public sealed record LlmMessage(string Role, string Content = "", IReadOnlyList<LlmToolCall>? ToolCalls = null, string? ToolName = null, JsonObject? ToolResult = null);

public sealed record LlmResponse(string? Text, IReadOnlyList<LlmToolCall> ToolCalls);

// The message never contains the API key, prompts or model output.
public sealed class LlmException(string message) : Exception(message);

public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken cancellationToken);
}
