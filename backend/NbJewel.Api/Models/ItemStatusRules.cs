// Role: The one place that defines the valid item statuses. Both the sample
// and SQL repositories call it, so the rule cannot drift between them.
// Statuses are assumed, not confirmed by NB Jewel. Assumed: any status can be
// chosen from any other status (forward, backward, or skipping), and Complete
// is not final. Only an unknown status is refused.

namespace NbJewel.Api.Models;

public static class ItemStatusRules
{
    public const string Draft = "Draft";
    public const string WithSmith = "With Smith";
    public const string Received = "Received";
    public const string Complete = "Complete";

    // The four allowed statuses, in display order. The order is not a rule.
    public static readonly IReadOnlyList<string> Workflow = [Draft, WithSmith, Received, Complete];

    // Returns the canonical spelling of a status, or null if it is not one of the four.
    public static string? Normalize(string? status) =>
        Workflow.FirstOrDefault(x => x.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));

    // Returns the canonical status to store. An unknown status throws
    // DomainValidationException (HTTP 400). Choosing the current status is not an
    // error: the caller compares it with the current value and writes nothing.
    public static string Resolve(string? requested) =>
        Normalize(requested)
            ?? throw new DomainValidationException($"Unknown status '{requested}'. Allowed statuses: {string.Join(", ", Workflow)}.");
}
