// Role: Proves configured stones/VAT, reconciling totals, and recorded-rate change evidence
// through the real swappable repositories without asserting invented historical sale prices.
using System.Text.Json;
using System.Text.Json.Nodes;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class CompletePricingTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("percent", 10, 67, 39.35, 826.35, 814.8, 11.55)]
    [InlineData("perGram", 2, 20, 37, 777, 766.5, 10.5)]
    [InlineData("flat", 25, 25, 37.25, 782.25, 771.75, 10.5)]
    public async Task RecordedRateChangeExplainsMetalMakingStonesVatAndTotal(string making, decimal makingValue,
        decimal charge, decimal vat, decimal total, decimal previous, decimal delta)
    {
        var repo = Repos.CreateWithRates("Sample", "metal-rates.ties.json");
        var payload = Payload(); payload["makingType"] = making; payload["makingValue"] = makingValue;
        var item = await repo.CreateAsync(payload.Deserialize<ItemCreateRequest>(Web)!, default);
        var price = JsonSerializer.SerializeToNode(await repo.GetPriceAsync(item.Id, default), Web)!;
        Assert.Equal(total, price["totalPrice"]!.GetValue<decimal>());
        Assert.Equal(670m, price["metalValue"]!.GetValue<decimal>());
        Assert.Equal(charge, price["makingCharge"]!.GetValue<decimal>());
        Assert.Equal(50m, price["stoneValue"]!.GetValue<decimal>());
        Assert.Equal(5m, price["vatPercent"]!.GetValue<decimal>());
        Assert.Equal(vat, price["vatAmount"]!.GetValue<decimal>());
        Assert.Equal(total, price["totalPrice"]!.GetValue<decimal>());
        Assert.Equal("2026-09-30", price["effectiveDate"]!.GetValue<string>());
        Assert.Equal(4, price["rateId"]!.GetValue<int>());
        var change = price["change"]!;
        Assert.Equal(3, change["previousRateId"]!.GetValue<int>());
        Assert.Equal(66m, change["previousRatePerGram"]!.GetValue<decimal>());
        Assert.Equal(previous, change["previousTotal"]!.GetValue<decimal>());
        Assert.Equal(delta, change["totalChange"]!.GetValue<decimal>());
        Assert.Contains("22K", change["explanation"]!.GetValue<string>());
        Assert.Contains("2026-09-30", change["explanation"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task PricingInputsSurviveReadsAndLegacyUpdates(string provider)
    {
        var repo = Repos.Create(provider); var payload = Payload();
        var item = await repo.CreateAsync(payload.Deserialize<ItemCreateRequest>(Web)!, default);
        payload.Remove("stoneValue"); payload.Remove("vatPercent"); payload["name"] = "Legacy edit";
        await repo.UpdateAsync(item.Id, payload.Deserialize<ItemUpdateRequest>(Web)!, default);
        var read = JsonSerializer.SerializeToNode(await repo.GetItemAsync(item.Id, default), Web)!;
        Assert.NotNull(read["stoneValue"]);
        Assert.Equal(50m, read["stoneValue"]!.GetValue<decimal>());
        Assert.Equal(5m, read["vatPercent"]!.GetValue<decimal>());
        payload["stoneValue"] = 0; payload["vatPercent"] = 0;
        await repo.UpdateAsync(item.Id, payload.Deserialize<ItemUpdateRequest>(Web)!, default);
        var price = JsonSerializer.SerializeToNode(await repo.GetPriceAsync(item.Id, default), Web)!;
        Assert.Equal(0m, price["stoneValue"]!.GetValue<decimal>());
        Assert.Equal(0m, price["vatAmount"]!.GetValue<decimal>());
        Assert.Null(price["change"]!["previousRateId"]);
    }

    [Theory]
    [InlineData("stoneValue", -1)]
    [InlineData("stoneValue", 10000000000)]
    [InlineData("vatPercent", -1)]
    [InlineData("vatPercent", 101)]
    public async Task InvalidPricingInputDoesNotSave(string field, decimal value)
    {
        var repo = Repos.Create("Sample"); var payload = Payload(); payload[field] = value;
        await Assert.ThrowsAsync<DomainValidationException>(() => repo.CreateAsync(payload.Deserialize<ItemCreateRequest>(Web)!, default));
        Assert.Empty((await repo.SearchItemsAsync("PRICE-SAMPLE", null, null, null, null, 1, 100, default)).Items);
    }

    [Fact]
    public async Task DisplayedComponentsReconcileAtRoundingBoundaries()
    {
        var repo = Repos.Create("Sample"); var payload = Payload();
        payload["grossWt"] = 1.005m; payload["stoneWt"] = 0;
        payload["makingType"] = "flat"; payload["makingValue"] = 0.005m;
        payload["stoneValue"] = 0.005m;
        var item = await repo.CreateAsync(payload.Deserialize<ItemCreateRequest>(Web)!, default);
        var price = JsonSerializer.SerializeToNode(await repo.GetPriceAsync(item.Id, default), Web)!;
        Assert.Equal(69.67m, price["totalPrice"]!.GetValue<decimal>());
        Assert.Equal(66.33m, price["metalValue"]!.GetValue<decimal>());
        Assert.Equal(0.01m, price["makingCharge"]!.GetValue<decimal>());
        Assert.Equal(0.01m, price["stoneValue"]!.GetValue<decimal>());
        Assert.Equal(3.32m, price["vatAmount"]!.GetValue<decimal>());
        Assert.Equal(69.67m, price["totalPrice"]!.GetValue<decimal>());
    }

    private static JsonObject Payload() => JsonNode.Parse("""
      {"code":"PRICE-SAMPLE","name":"Assumed sample pricing necklace","categoryId":3,"designId":6,
       "metalType":"Gold","purity":"22K","grossWt":11,"netWt":999,"stoneWt":1,
       "makingType":"percent","makingValue":10,"huid":null,"imageUrl":null,"stoneValue":50,"vatPercent":5}
      """)!.AsObject();
}
