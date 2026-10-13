// Role: Talks to Google Gemini over its REST API (generateContent) with a plain HttpClient.
// The request has the same shape the old Python provider sent: system instruction, the
// conversation as contents, function declarations, temperature and a thinking level.
// The API key comes only from the GEMINI_API_KEY environment variable and goes only in a
// request header. This class never logs the key, prompts, replies or tool results.
// It retries once at most: when the model rejects the thinking setting, or on 429/500/503.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace NbJewel.Api.Chat;

// ThinkingLevel: minimal, low, medium, high, or empty for the model default.
public sealed record GeminiOptions(string ApiKey, string Model, string ThinkingLevel)
{
    private static readonly string[] Levels = ["minimal", "low", "medium", "high"];

    // Unset GEMINI_THINKING_LEVEL means "low". An empty value means the model default.
    public static GeminiOptions FromConfiguration(IConfiguration configuration) =>
        new(configuration["GEMINI_API_KEY"] ?? "", configuration["GEMINI_MODEL"] ?? "", (configuration["GEMINI_THINKING_LEVEL"] ?? "low").Trim().ToLowerInvariant());

    public string? ThinkingLevelOrNull => Levels.Contains(ThinkingLevel) ? ThinkingLevel.ToUpperInvariant() : null;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
    public bool ThinkingLevelIsValid => ThinkingLevel.Length == 0 || Levels.Contains(ThinkingLevel);
}

public sealed class GeminiLlmClient(HttpClient http, GeminiOptions options, ILogger<GeminiLlmClient> logger) : ILlmClient
{
    public static readonly Uri BaseAddress = new("https://generativelanguage.googleapis.com/");
    private bool thinkingRejected;

    public async Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken cancellationToken)
    {
        if (!options.IsConfigured) throw new LlmException("GEMINI_API_KEY or GEMINI_MODEL is not set.");
        if (!options.ThinkingLevelIsValid) throw new LlmException("GEMINI_THINKING_LEVEL must be minimal, low, medium, high or empty.");

        var retried = false;
        while (true)
        {
            var (status, body) = await SendAsync(BuildRequest(messages, tools, temperature), cancellationToken);
            if (status == HttpStatusCode.OK) return ParseResponse(body);

            if (!retried && status == HttpStatusCode.BadRequest && !thinkingRejected && options.ThinkingLevelOrNull is not null && body.Contains("think", StringComparison.OrdinalIgnoreCase))
            {
                // This model does not accept the thinking setting. Carry on without it.
                logger.LogWarning("The model rejected the thinking level. Retrying once without it.");
                thinkingRejected = true;
                retried = true;
                continue;
            }
            if (!retried && status is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable)
            {
                logger.LogWarning("Gemini returned status {Status}. Retrying once.", (int)status);
                retried = true;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }
            logger.LogWarning("Gemini request failed with status {Status}.", (int)status);
            throw new LlmException($"The Gemini request failed (status {(int)status}).");
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(JsonObject request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(options.Model)}:generateContent")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("x-goog-api-key", options.ApiKey);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception error) when (error is HttpRequestException || (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A timeout surfaces as a cancelled request that the caller did not cancel.
            logger.LogWarning("Gemini call failed: {ErrorType}.", error.GetType().Name);
            throw new LlmException($"The Gemini request failed ({error.GetType().Name}).");
        }
    }

    // ---------- request ----------

    private JsonObject BuildRequest(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature)
    {
        var systemText = string.Join("\n\n", messages.Where(m => m.Role == "system" && m.Content.Length > 0).Select(m => m.Content));
        var generationConfig = new JsonObject { ["temperature"] = temperature };
        if (options.ThinkingLevelOrNull is { } level && !thinkingRejected)
            generationConfig["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = level };

        var request = new JsonObject { ["contents"] = ToContents(messages), ["generationConfig"] = generationConfig };
        if (systemText.Length > 0)
            request["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = systemText }) };
        if (tools.Count > 0)
        {
            var declarations = new JsonArray();
            foreach (var tool in tools)
                declarations.Add(new JsonObject { ["name"] = tool.Name, ["description"] = tool.Description, ["parametersJsonSchema"] = tool.Parameters.DeepClone() });
            request["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });
        }
        return request;
    }

    private static JsonArray ToContents(IReadOnlyList<LlmMessage> messages)
    {
        var contents = new JsonArray();
        foreach (var m in messages)
        {
            if (m.Role == "user")
                contents.Add(Content("user", new JsonObject { ["text"] = m.Content }));
            else if (m.Role == "assistant")
            {
                var parts = new JsonArray();
                if (m.Content.Length > 0) parts.Add(new JsonObject { ["text"] = m.Content });
                foreach (var call in m.ToolCalls ?? [])
                    // Reuse the original part when we have it, so thought signatures survive.
                    parts.Add(call.RawPart?.DeepClone() ?? new JsonObject { ["functionCall"] = new JsonObject { ["name"] = call.Name, ["args"] = call.Args.DeepClone() } });
                contents.Add(new JsonObject { ["role"] = "model", ["parts"] = parts });
            }
            else if (m.Role == "tool")
            {
                var part = new JsonObject { ["functionResponse"] = new JsonObject { ["name"] = m.ToolName ?? "", ["response"] = m.ToolResult?.DeepClone() ?? new JsonObject() } };
                // Results of one model turn go together in a single user message.
                if (contents.Count > 0 && contents[^1] is JsonObject last && last["role"]?.GetValue<string>() == "user"
                    && last["parts"] is JsonArray lastParts && lastParts.All(p => p?["functionResponse"] is not null))
                    lastParts.Add(part);
                else
                    contents.Add(Content("user", part));
            }
        }
        return contents;
    }

    private static JsonObject Content(string role, JsonObject part) => new() { ["role"] = role, ["parts"] = new JsonArray(part) };

    // ---------- response ----------

    private static LlmResponse ParseResponse(string body)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(body); }
        catch (System.Text.Json.JsonException) { throw new LlmException("The Gemini reply was not valid JSON."); }

        var texts = new List<string>();
        var calls = new List<LlmToolCall>();
        if (root?["candidates"]?[0]?["content"]?["parts"] is JsonArray parts)
        {
            foreach (var part in parts.OfType<JsonObject>())
            {
                if (part["functionCall"] is JsonObject functionCall)
                    calls.Add(new LlmToolCall(functionCall["name"]?.GetValue<string>() ?? "", functionCall["args"] as JsonObject ?? new JsonObject(), part.DeepClone()));
                else if (part["text"] is JsonValue text && text.TryGetValue<string>(out var value) && part["thought"]?.GetValue<bool>() != true)
                    texts.Add(value);
            }
        }
        var joined = string.Concat(texts);
        return new LlmResponse(joined.Length > 0 ? joined : null, calls);
    }
}
