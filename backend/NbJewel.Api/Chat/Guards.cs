// Role: The four answer guards, ported from the old Python ai-service/app/guards.
// An answer is only allowed if it passes all four, in this order:
//   1. tool_calls: at least one tool was called.
//   2. sources: each source names a tool that was called and a recordId that tool returned.
//      If no tool returned records (all not_found or error), the sources must be empty.
//   3. numbers: every number in the answer comes from the tool results. A number is allowed if it
//      appears in the results (as a number or inside text such as "22K" or "RNG-0142"), equals the
//      length of a list in the results, or equals a sum of one numeric field over a list or over
//      all results. Averages, differences, rounded values and sums of different fields are rejected.
//      The guard only compares numbers. It cannot tell if the sentence around a number is true.
//   4. leak: the answer must not repeat a line of the system prompt (only word-for-word lines,
//      ignoring case and punctuation, are caught).

using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NbJewel.Api.Models;

namespace NbJewel.Api.Chat;

public sealed record ToolRun(string Name, JsonObject Args, JsonObject Result)
{
    public string Status => Result["status"]?.GetValue<string>() ?? "";

    public ISet<string> RecordIds =>
        (Result["sources"] as JsonArray ?? []).OfType<JsonObject>().Select(s => s["recordId"]?.ToString() ?? "").ToHashSet();
}

public sealed record ChatAnswer(string Text, IReadOnlyList<ChatSource> Sources);

public sealed record GuardResult(bool Passed, string Guard = "", string Reason = "")
{
    public static readonly GuardResult Pass = new(true);
}

public static partial class Guards
{
    public const string RefusalText = "I can only answer from NB Jewel data. I have no tool for that.";
    private const int MinPromptWords = 5;

    public static GuardResult Run(ChatAnswer answer, IReadOnlyList<ToolRun> runs, string? systemPrompt)
    {
        foreach (var result in new[] { CheckToolCalls(runs), CheckSources(answer, runs), CheckNumbers(answer, runs), CheckPromptLeak(answer, systemPrompt) })
            if (!result.Passed) return result;
        return GuardResult.Pass;
    }

    // ---------- guard 1 ----------

    public static GuardResult CheckToolCalls(IReadOnlyList<ToolRun> runs) =>
        runs.Count == 0 ? new GuardResult(false, "tool_calls", "No tool was called.") : GuardResult.Pass;

    // ---------- guard 2 ----------

    public static GuardResult CheckSources(ChatAnswer answer, IReadOnlyList<ToolRun> runs)
    {
        if (!runs.Any(r => r.Status == "ok"))
            return answer.Sources.Count > 0 ? new GuardResult(false, "sources", "Sources were given but no tool returned records.") : GuardResult.Pass;
        if (answer.Sources.Count == 0) return new GuardResult(false, "sources", "Sources are empty.");

        var idsByTool = new Dictionary<string, HashSet<string>>();
        foreach (var run in runs)
        {
            if (!idsByTool.TryGetValue(run.Name, out var ids)) idsByTool[run.Name] = ids = [];
            ids.UnionWith(run.RecordIds);
        }
        foreach (var source in answer.Sources)
        {
            if (!idsByTool.TryGetValue(source.Tool, out var ids))
                return new GuardResult(false, "sources", $"Source names a tool that was not called: {source.Tool}.");
            if (!ids.Contains(source.RecordId))
                return new GuardResult(false, "sources", $"Source record {source.RecordId} is not in the results of {source.Tool}.");
        }
        return GuardResult.Pass;
    }

    // ---------- guard 3 ----------

    [GeneratedRegex(@"\d[\d,]*(?:\.\d+)?")]
    private static partial Regex NumberPattern();

    // "1. Zork Ring" is list numbering, not a claim about data.
    [GeneratedRegex(@"^\s*(?:[-*]\s+)?\d+[.)]\s+", RegexOptions.Multiline)]
    private static partial Regex ListMarkerPattern();

    public static GuardResult CheckNumbers(ChatAnswer answer, IReadOnlyList<ToolRun> runs)
    {
        var allowed = AllowedNumbers(runs);
        foreach (var number in AnswerNumbers(answer.Text).Order())
            if (!allowed.Contains(number))
                return new GuardResult(false, "numbers", $"The number {number.ToString(CultureInfo.InvariantCulture)} is not in the tool results.");
        return GuardResult.Pass;
    }

    public static HashSet<decimal> AnswerNumbers(string text) => NumbersInText(ListMarkerPattern().Replace(text, ""));

    public static HashSet<decimal> NumbersInText(string text)
    {
        var found = new HashSet<decimal>();
        foreach (Match match in NumberPattern().Matches(text))
            if (decimal.TryParse(match.Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) found.Add(Normalize(value));
        return found;
    }

    // 1.50 and 1.5 must count as the same number.
    private static decimal Normalize(decimal value) => value / 1.0000000000000000000000000000m;

    // Ids identify records. Adding them up never gives a meaningful number.
    private static bool Summable(string key) => !key.EndsWith("id", StringComparison.OrdinalIgnoreCase);

    private static bool TryNumber(JsonNode? node, out decimal value)
    {
        value = 0;
        if (node is not JsonValue v || v.GetValueKind() != System.Text.Json.JsonValueKind.Number) return false;
        if (!decimal.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        value = Normalize(value);
        return true;
    }

    public static HashSet<decimal> AllowedNumbers(IReadOnlyList<ToolRun> runs)
    {
        var found = new HashSet<decimal>();
        var lengths = new HashSet<decimal>();
        var sums = new HashSet<decimal>();
        var fieldValues = new Dictionary<string, List<decimal>>();
        foreach (var run in runs) Collect(run.Result["data"], found, lengths, fieldValues, sums);
        foreach (var values in fieldValues.Values)
            if (values.Count > 1) sums.Add(Normalize(values.Sum()));
        found.UnionWith(lengths);
        found.UnionWith(sums);
        return found;
    }

    private static void Collect(JsonNode? node, HashSet<decimal> found, HashSet<decimal> lengths, Dictionary<string, List<decimal>> fieldValues, HashSet<decimal> sums)
    {
        switch (node)
        {
            case JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.Number:
                if (TryNumber(value, out var number)) found.Add(number);
                break;
            case JsonValue value when value.TryGetValue<string>(out var text):
                found.UnionWith(NumbersInText(text));
                break;
            case JsonArray array:
                lengths.Add(array.Count);
                var perField = new Dictionary<string, List<decimal>>();
                foreach (var child in array.OfType<JsonObject>())
                    foreach (var (key, member) in child)
                        if (Summable(key) && TryNumber(member, out var fieldNumber))
                            (perField.TryGetValue(key, out var list) ? list : perField[key] = []).Add(fieldNumber);
                foreach (var values in perField.Values) sums.Add(Normalize(values.Sum()));
                foreach (var child in array) Collect(child, found, lengths, fieldValues, sums);
                break;
            case JsonObject obj:
                foreach (var (key, member) in obj)
                {
                    if (Summable(key) && TryNumber(member, out var fieldNumber))
                        (fieldValues.TryGetValue(key, out var list) ? list : fieldValues[key] = []).Add(fieldNumber);
                    Collect(member, found, lengths, fieldValues, sums);
                }
                break;
        }
    }

    // ---------- guard 4 ----------

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    private static string NormalizeText(string text) =>
        string.Join(' ', NonAlphanumeric().Replace(text.ToLowerInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static List<string> PromptLines(string systemPrompt) =>
        systemPrompt.Split('\n')
            .Select(line => NormalizeText(line.Trim().TrimStart('-').Trim()))
            .Where(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= MinPromptWords)
            .ToList();

    public static GuardResult CheckPromptLeak(ChatAnswer answer, string? systemPrompt)
    {
        if (string.IsNullOrEmpty(systemPrompt)) return GuardResult.Pass;
        var text = NormalizeText(answer.Text);
        return PromptLines(systemPrompt).Any(text.Contains)
            ? new GuardResult(false, "leak", "The answer repeats part of the system prompt.")
            : GuardResult.Pass;
    }
}
