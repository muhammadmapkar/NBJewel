// Role: The chat loop, ported from the old Python ai-service/app/chat.py. It sends a question to
// the model with the tool declarations, runs the tools the model asks for, feeds the results back,
// and checks the final answer with the four guards. Anything that fails a check becomes the fixed
// refusal. The loop keeps no state between questions.
// Limits: at most 5 tool calls and 8 model calls per question, and the last 6 history messages.
// Rules for the answer:
//   - No tool ran (off-topic question): the refusal text, refused = true.
//   - Tools ran but every one found nothing (not_found): "No data found.", refused = false.
//   - Otherwise the model's answer must pass all four guards, or it becomes the refusal.
// It never logs the question, the model's text or tool results. It logs tool names, statuses and guard names.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NbJewel.Api.Models;

namespace NbJewel.Api.Chat;

public sealed partial class ChatOrchestrator(ILlmClient llm, ChatTools tools, ILogger<ChatOrchestrator> logger)
{
    public const double Temperature = 0.1;
    public const int MaxToolCalls = 5;
    public const int MaxModelCalls = 8;
    public const int MaxHistory = 6;
    public const string NoDataText = "No data found.";
    private const string JsonRetryNote = "Your last reply was not valid JSON. Reply with JSON only: {\"answer\": string, \"sources\": [{\"tool\": string, \"recordId\": string}]}";

    private static ChatResponse Refusal() => new(Guards.RefusalText, [], true);

    public async Task<ChatResponse> AskAsync(string question, IReadOnlyList<ChatHistoryMessage>? history, CancellationToken cancellationToken)
    {
        var messages = new List<LlmMessage> { new("system", SystemPrompt.Text) };
        foreach (var message in (history ?? []).TakeLast(MaxHistory))
            messages.Add(new LlmMessage(message.Role == "assistant" ? "assistant" : "user", message.Content));
        messages.Add(new LlmMessage("user", question));

        var runs = new List<ToolRun>();
        var retriedJson = false;
        var modelCalls = 0;

        while (true)
        {
            if (++modelCalls > MaxModelCalls)
            {
                logger.LogInformation("guard=model_limit result=refused");
                return Refusal();
            }
            var response = await llm.GenerateAsync(messages, tools.Declarations, Temperature, cancellationToken);

            if (response.ToolCalls.Count > 0)
            {
                if (runs.Count + response.ToolCalls.Count > MaxToolCalls)
                {
                    logger.LogInformation("guard=tool_limit result=refused calls={Calls}", runs.Count);
                    return Refusal();
                }
                messages.Add(new LlmMessage("assistant", response.Text ?? "", response.ToolCalls));
                foreach (var call in response.ToolCalls)
                {
                    var result = await tools.RunAsync(call.Name, call.Args, cancellationToken);
                    var run = new ToolRun(call.Name, call.Args, result);
                    runs.Add(run);
                    logger.LogInformation("tool={Tool} status={Status} records={Records}", call.Name, run.Status, run.RecordIds.Count);
                    messages.Add(new LlmMessage("tool", ToolName: call.Name, ToolResult: result));
                }
                continue;
            }

            // Tools ran but found nothing: say so, without asking the model to word it.
            if (runs.Count > 0 && runs.All(r => r.Status == "not_found"))
            {
                logger.LogInformation("guard=no_data result=no_data_found");
                return new ChatResponse(NoDataText, [], false);
            }

            var answer = ParseAnswer(response.Text);
            if (answer is null)
            {
                if (retriedJson)
                {
                    logger.LogInformation("guard=json result=refused");
                    return Refusal();
                }
                retriedJson = true;
                messages.Add(new LlmMessage("assistant", response.Text ?? ""));
                messages.Add(new LlmMessage("user", JsonRetryNote));
                continue;
            }

            var verdict = Guards.Run(answer, runs, SystemPrompt.Text);
            if (!verdict.Passed)
            {
                logger.LogInformation("guard={Guard} result=refused reason={Reason}", verdict.Guard, verdict.Reason);
                return Refusal();
            }
            logger.LogInformation("guard=all result=passed sources={Sources}", answer.Sources.Count);
            return new ChatResponse(answer.Text, answer.Sources.ToList(), false);
        }
    }

    [GeneratedRegex(@"^```(?:json)?\s*|\s*```$")]
    private static partial Regex CodeFence();

    /// <summary>Reads the model's JSON reply. Returns null if it is not the expected shape.</summary>
    public static ChatAnswer? ParseAnswer(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        JsonNode? root;
        try { root = JsonNode.Parse(CodeFence().Replace(text.Trim(), "")); }
        catch (JsonException) { return null; }

        if (root is not JsonObject obj || obj["answer"] is not JsonValue answerNode || !answerNode.TryGetValue<string>(out var answer) || obj["sources"] is not JsonArray sourceNodes)
            return null;
        var sources = new List<ChatSource>();
        foreach (var node in sourceNodes)
        {
            if (node is not JsonObject source || source["tool"] is not JsonValue toolNode || !toolNode.TryGetValue<string>(out var tool) || source["recordId"] is null)
                return null;
            sources.Add(new ChatSource(tool, source["recordId"]!.ToString()));
        }
        return new ChatAnswer(answer, sources);
    }
}
