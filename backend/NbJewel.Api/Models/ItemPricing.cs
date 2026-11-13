// Role: Calculates a reconciling metal/making/stones/VAT breakdown and deterministic evidence
// of the latest recorded rate change. The previous total uses the same current item inputs.

namespace NbJewel.Api.Models;

public static class ItemPricing
{
    public static ItemPrice Calculate(Item item, MetalRate rate, MetalRate? previousRate = null)
    {
        var current = AtRate(item, rate);
        var previous = previousRate is null ? null : AtRate(item, previousRate);
        var explanation = previousRate is null
            ? FormattableString.Invariant($"No earlier recorded rate for {rate.MetalType} {rate.Purity}. Uses {rate.Currency} {rate.RatePerGram:0.00}/g effective {rate.EffectiveDate:yyyy-MM-dd}; includes configured making, stones and VAT.")
            : FormattableString.Invariant($"Recorded {rate.MetalType} {rate.Purity} rate changed from {previousRate.Currency} {previousRate.RatePerGram:0.00}/g effective {previousRate.EffectiveDate:yyyy-MM-dd} to {rate.Currency} {rate.RatePerGram:0.00}/g effective {rate.EffectiveDate:yyyy-MM-dd}. Totals compare the same current weight, making, stones and VAT inputs, not a historical sale.");
        // Currency changes are not comparable and must not be reported as a monetary delta.
        var comparable = previous is not null && previous.Currency == current.Currency;
        return current with { Change = new(previousRate?.Id, previousRate?.RatePerGram,
            previousRate?.EffectiveDate, comparable ? previous!.TotalPrice : null,
            comparable ? current.TotalPrice - previous!.TotalPrice : null, explanation) };
    }

    private static ItemPrice AtRate(Item item, MetalRate rate)
    {
        var metal = Round(item.NetWt * rate.RatePerGram);
        var making = Round(item.MakingType.ToLowerInvariant() switch
        {
            "pergram" => item.NetWt * item.MakingValue,
            "percent" => item.NetWt * rate.RatePerGram * item.MakingValue / 100m,
            "flat" => item.MakingValue,
            _ => throw new InvalidOperationException($"Unsupported making type '{item.MakingType}'.")
        });
        var stones = Round(item.StoneValue);
        var subtotal = metal + making + stones;
        var vat = Round(subtotal * item.VatPercent / 100m);
        return new(item.Id, rate.Currency, item.NetWt, rate.RatePerGram, metal, item.MakingType,
            item.MakingValue, making, subtotal + vat, stones, item.VatPercent, vat, rate.EffectiveDate, rate.Id);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
