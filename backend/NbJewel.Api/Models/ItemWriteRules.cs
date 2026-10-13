// Role: The one place that validates an item before it is saved. Both the sample
// and SQL repositories call it, so the two cannot drift apart. Net weight is
// always gross minus stone: any NetWt a client sends is ignored. A stone weight
// above the gross weight is refused with a validation error (HTTP 400).
// Length and range limits match the columns in docs/item_catalog_schema.sql (assumed).

namespace NbJewel.Api.Models;

// The cleaned values that are safe to store. NetWt is computed, never taken from the client.
public sealed record ValidItem(string Code, string Name, decimal GrossWt, decimal StoneWt, decimal NetWt, string MakingType, decimal MakingValue, string? Huid, string? ImageUrl);

public static class ItemWriteRules
{
    // Making types the API accepts, in the spelling the API returns.
    public static readonly IReadOnlyList<string> MakingTypes = ["perGram", "percent", "flat"];

    public static ValidItem Validate(string? code, string? name, decimal grossWt, decimal stoneWt, string? makingType, decimal makingValue, string? huid, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainValidationException("Item code is required.");
        if (code.Length > 30) throw new DomainValidationException("Item code can be at most 30 characters.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainValidationException("Item name is required.");
        if (name.Length > 200) throw new DomainValidationException("Item name can be at most 200 characters.");
        if (huid is { Length: > 20 }) throw new DomainValidationException("HUID can be at most 20 characters.");
        if (imageUrl is { Length: > 500 }) throw new DomainValidationException("Image URL can be at most 500 characters.");

        // The database stores weights with 3 decimals and the making value with 2. Round the same way here.
        var gross = Math.Round(grossWt, 3, MidpointRounding.AwayFromZero);
        var stone = Math.Round(stoneWt, 3, MidpointRounding.AwayFromZero);
        if (gross <= 0) throw new DomainValidationException("Gross weight must be greater than 0.");
        if (gross > 9_999_999.999m) throw new DomainValidationException("Gross weight is too large.");
        if (stone < 0) throw new DomainValidationException("Stone weight cannot be negative.");
        if (stone > gross) throw new DomainValidationException("Stone weight cannot be greater than gross weight.");

        var making = MakingTypes.FirstOrDefault(x => x.Equals(makingType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainValidationException($"Unsupported making type '{makingType}'. Allowed: {string.Join(", ", MakingTypes)}.");
        var value = Math.Round(makingValue, 2, MidpointRounding.AwayFromZero);
        if (value < 0) throw new DomainValidationException("Making value cannot be negative.");
        if (value > 99_999_999.99m) throw new DomainValidationException("Making value is too large.");

        return new ValidItem(code, name, gross, stone, gross - stone, making, value, huid, imageUrl);
    }
}
