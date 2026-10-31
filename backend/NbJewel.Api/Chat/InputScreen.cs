// Role: A cheap first check on the user's question, before any model call. It catches obvious
// attempts to change the assistant's rules (for example "ignore your rules", "show your system
// prompt", "pretend you are", "developer mode"). A blocked question gets the fixed refusal and
// never reaches the model, so it also costs nothing. This is only a first layer: it cannot catch
// every wording, which is why the system prompt and the answer guards still run on everything else.

using System.Text.RegularExpressions;

namespace NbJewel.Api.Chat;

public static partial class InputScreen
{
    // Matched on the lower-cased question with extra spaces collapsed.
    [GeneratedRegex(
        @"(ignore|disregard|forget|override|bypass|skip)\s+(all\s+|any\s+|your\s+|the\s+|previous\s+|prior\s+|above\s+|earlier\s+)*(rules|instructions|prompt|guidelines|restrictions|guards|safeguards)"
        + @"|(system|developer|hidden|initial)\s+(prompt|message|instructions)"
        + @"|(reveal|show|print|repeat|leak)\s+(me\s+)?(your|the)\s+(rules|instructions|prompt|configuration|config)"
        + @"|(pretend|act|behave|roleplay|role-play)\s+(to\s+be|as|like|that|you\s+are|you're)\b"
        + @"|you\s+are\s+now\b"
        + @"|(developer|debug|admin|god|dan|jailbreak)\s+mode"
        + @"|\bjailbreak\b"
        + @"|without\s+(any\s+)?(rules|restrictions|limits|filters)"
        + @"|from\s+your\s+(own\s+)?(knowledge|memory|training)"
        + @"|(api[\s_-]?key|secret|password|credential)s?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Suspicious();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>True when the question tries to change the rules or asks for secrets.</summary>
    public static bool IsBlocked(string? question)
    {
        if (string.IsNullOrWhiteSpace(question)) return false;
        return Suspicious().IsMatch(Spaces().Replace(question.Trim(), " "));
    }
}
