// Role: Price calculation shared by the sample and SQL repositories, so both
// return the same numbers for the same item and metal rate.

namespace NbJewel.Api.Models;

public static class ItemPricing
{
    public static ItemPrice Calculate(Item item, MetalRate rate)
    {
        var metalValue = item.NetWt * rate.RatePerGram;
        var makingCharge = item.MakingType.ToLowerInvariant() switch
        {
            "pergram" => item.NetWt * item.MakingValue,
            "percent" => metalValue * item.MakingValue / 100m,
            "flat" => item.MakingValue,
            _ => throw new InvalidOperationException($"Unsupported making type '{item.MakingType}'.")
        };
        return new(item.Id, rate.Currency, item.NetWt, rate.RatePerGram, Round(metalValue), item.MakingType, item.MakingValue, Round(makingCharge), Round(metalValue + makingCharge));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
