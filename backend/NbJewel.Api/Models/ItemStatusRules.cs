// Role: The one place that defines the item status workflow. Both the sample
// and SQL repositories call it, so the rule cannot drift between them.
// Statuses and their order are assumed, not confirmed by NB Jewel.

namespace NbJewel.Api.Models;

public static class ItemStatusRules
{
    public const string Draft = "Draft";
    public const string WithSmith = "With Smith";
    public const string Received = "Received";
    public const string Complete = "Complete";

    // Order matters: a status can only move to the one right after it.
    public static readonly IReadOnlyList<string> Workflow = [Draft, WithSmith, Received, Complete];

    // Returns the canonical spelling of a status, or null if it is not a workflow status.
    public static string? Normalize(string? status) =>
        Workflow.FirstOrDefault(x => x.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));

    // Returns the only status allowed after this one, or null when there is none.
    public static string? Next(string current)
    {
        var index = Workflow.ToList().IndexOf(Normalize(current) ?? string.Empty);
        return index >= 0 && index < Workflow.Count - 1 ? Workflow[index + 1] : null;
    }

    // Checks a requested move and returns the canonical new status.
    // Unknown requested status: DomainValidationException (400).
    // Backward, repeated, or skipping move: InvalidStatusMoveException (409).
    public static string EnsureMove(string current, string requested)
    {
        var target = Normalize(requested)
            ?? throw new DomainValidationException($"Unknown status '{requested}'. Allowed statuses: {string.Join(", ", Workflow)}.");
        var from = Normalize(current)
            ?? throw new InvalidStatusMoveException($"The current status '{current}' is not part of the workflow, so it cannot be moved.");
        var next = Next(from);
        if (next is null)
            throw new InvalidStatusMoveException($"Cannot move from {from} to {target}. {from} is the last status.");
        if (!next.Equals(target, StringComparison.Ordinal))
            throw new InvalidStatusMoveException($"Cannot move from {from} to {target}. The next allowed status is {next}.");
        return target;
    }
}
