// Role: Tests price calculation. The demo data has realistic rates (24K 72, 22K 66,
// 18K 54 AED per gram), so a 22K item prices above a lighter 18K item. A separate
// test uses the test-only poisoned fixture (22K rate 1.00) to confirm the math
// follows whatever rate the data holds, which the chatbot grounding tests rely on.

using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class PriceCalculationTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task ZorkRing_UsesLatest22KRateAndPercentMakingCharge()
    {
        var price = await SampleRepositoryFactory.Create().GetPriceAsync(1, Ct);

        Assert.NotNull(price);
        Assert.Equal(66m, price.RatePerGram);
        Assert.Equal(42.758m, price.NetWeight);
        Assert.Equal(2822.03m, price.MetalValue);
        Assert.Equal(185.69m, price.MakingCharge);
        Assert.Equal(3007.72m, price.TotalPrice);
    }

    [Fact]
    public async Task ClassicRing_18K_UsesThe18KRate()
    {
        var price = await SampleRepositoryFactory.Create().GetPriceAsync(2, Ct);

        Assert.NotNull(price);
        Assert.Equal(54m, price.RatePerGram);
        Assert.Equal(270.22m, price.MetalValue);
        Assert.Equal(28.43m, price.MakingCharge);
        Assert.Equal(298.64m, price.TotalPrice);
    }

    [Fact]
    public async Task DemoRates_AreOrdered24KAbove22KAbove18K()
    {
        var rates = await SampleRepositoryFactory.Create().GetLatestRatesAsync(Ct);
        decimal Rate(string purity) => rates.Single(r => r.MetalType == "Gold" && r.Purity == purity).RatePerGram;

        Assert.True(Rate("24K") > Rate("22K"));
        Assert.True(Rate("22K") > Rate("18K"));
    }

    [Fact]
    public async Task A22KItemIsNotPricedBelowALighter18KItemWithTheSameMakingType()
    {
        // Both items use a percent making charge. The 22K ring is far heavier, but even per gram
        // of metal the 22K rate must be higher, so a 22K ring of equal weight costs more.
        var repository = SampleRepositoryFactory.Create();
        var item22 = (await repository.GetItemAsync(1, Ct))!;
        var item18 = (await repository.GetItemAsync(2, Ct))!;
        Assert.Equal("22K", item22.Purity);
        Assert.Equal("18K", item18.Purity);
        Assert.True(item22.NetWt > item18.NetWt);

        var price22 = (await repository.GetPriceAsync(1, Ct))!;
        var price18 = (await repository.GetPriceAsync(2, Ct))!;
        Assert.True(price22.TotalPrice > price18.TotalPrice);
        Assert.True(price22.RatePerGram > price18.RatePerGram);
    }

    [Fact]
    public async Task PoisonedFixture_PricesFromThePoisonedRate_NotFromTheDemoData()
    {
        var price = await SampleRepositoryFactory.CreateWithPoisonedRates().GetPriceAsync(1, Ct);

        Assert.NotNull(price);
        Assert.Equal(1m, price.RatePerGram);
        Assert.Equal(2.81m, price.MakingCharge);
        Assert.Equal(45.57m, price.TotalPrice);
    }
}
