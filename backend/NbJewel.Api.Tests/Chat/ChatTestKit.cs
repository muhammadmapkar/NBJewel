// Role: Shared helpers for the chat tests: a fake language model that plays back a script
// (so no API key and no network are needed), and small builders for tool calls, model
// answers and tool results. Tools run on the real sample data through SampleItemRepository.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NbJewel.Api.Chat;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests.Chat;

// Plays back a script of replies and records what it was sent.
internal sealed class FakeLlm(params LlmResponse[] script) : ILlmClient
{
    private readonly Queue<LlmResponse> queue = new(script);
    public List<List<LlmMessage>> Calls { get; } = [];
    public List<double> Temperatures { get; } = [];
    public List<IReadOnlyList<ToolDeclaration>> ToolLists { get; } = [];

    public Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken cancellationToken)
    {
        Calls.Add(messages.ToList());
        Temperatures.Add(temperature);
        ToolLists.Add(tools);
        if (!queue.TryDequeue(out var reply)) throw new InvalidOperationException("The fake model ran out of replies.");
        return Task.FromResult(reply);
    }
}

internal static class ChatTestKit
{
    public static ChatTools Tools(IItemRepository? repository = null) => new(new ChatDataReader(repository ?? SampleRepositoryFactory.Create()));

    public static ChatOrchestrator Orchestrator(FakeLlm llm, IItemRepository? repository = null) =>
        new(llm, Tools(repository), NullLogger<ChatOrchestrator>.Instance);

    public static LlmResponse Call(string tool, object? args = null) =>
        new(null, [new LlmToolCall(tool, (JsonSerializer.SerializeToNode(args ?? new { }) as JsonObject)!)]);

    public static LlmResponse Say(string answer, params (string Tool, string Id)[] sources) =>
        new(JsonSerializer.Serialize(new { answer, sources = sources.Select(s => new { tool = s.Tool, recordId = s.Id }) }), []);

    public static Task<JsonObject> RunTool(string name, object? args = null, IItemRepository? repository = null) =>
        Tools(repository).RunAsync(name, JsonSerializer.SerializeToNode(args ?? new { }) as JsonObject, CancellationToken.None);

    // A tool run with a made-up result, for the guard tests.
    public static ToolRun OkRun(string tool, string dataJson, params string[] recordIds) =>
        new(tool, [], new JsonObject
        {
            ["status"] = "ok",
            ["data"] = JsonNode.Parse(dataJson),
            ["sources"] = new JsonArray(recordIds.Select(id => (JsonNode)new JsonObject { ["tool"] = tool, ["recordId"] = id }).ToArray()),
        });

    public static ToolRun NotFoundRun(string tool) =>
        new(tool, [], new JsonObject { ["status"] = "not_found", ["data"] = new JsonObject { ["message"] = "nothing" }, ["sources"] = new JsonArray() });

    public static ChatAnswer Answer(string text, params (string Tool, string Id)[] sources) =>
        new(text, sources.Select(s => new NbJewel.Api.Models.ChatSource(s.Tool, s.Id)).ToList());
}
