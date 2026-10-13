// Role: Proves the "latest rate" rule and the rate order with a test-only rates file
// (Fixtures/metal-rates.ties.json), on every provider. The rule: for each metal and
// purity the newest date wins, a higher id wins a same-day tie, and the list is
// ordered by id. The file also has older rows, so a wrong rule would show up here.

using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class RateRulesTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string Fixture = "metal-rates.ties.json";

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task LatestRates_PickNewestDateThenHigherId_AndAreOrderedById(string provider)
    {
        var rates = await Repos.CreateWithRates(provider, Fixture).GetLatestRatesAsync(Ct);

        // 22K: id 3 and 4 share the newest date, so id 4 wins. 24K: id 6 is newer than id 1. 925: only id 5.
        Assert.Equal([4, 5, 6], rates.Select(x => x.Id).ToArray());
        Assert.Equal([67m, 0.9m, 71m], rates.Select(x => x.RatePerGram).ToArray());
        Assert.All(rates, x => Assert.Equal("AED", x.Currency));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Price_UsesTheSameLatestRate_AndIsNullWhenAPurityHasNoRate(string provider)
    {
        var repository = Repos.CreateWithRates(provider, Fixture);

        Assert.Equal(67m, (await repository.GetPriceAsync(1, Ct))!.RatePerGram); // item 1 is 22K gold
        Assert.Null(await repository.GetPriceAsync(2, Ct));                       // item 2 is 18K gold, the file has no 18K rate
    }
}
