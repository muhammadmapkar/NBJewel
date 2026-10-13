// Role: Tests the read-only low-stock list. A row is low when on hand minus
// reserved is below the threshold. Equal is not low. A threshold outside 1 to
// 1000 returns 400. Uses item 1, variant 12, which starts with no stock.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class LowStockTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task LowStock_IncludesRowsBelowTheThreshold(string provider)
    {
        var repository = Repos.Create(provider);
        await repository.AddStockAsync(1, new StockAddRequest(12, 1, 1), Ct);
        var low = await repository.GetLowStockAsync(2, Ct);
        Assert.Contains(low, x => x.ItemId == 1 && x.VariantId == 12 && x.StoreId == 1);
        Assert.All(low, x => Assert.True(x.OnHand - x.Reserved < 2));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task LowStock_ExcludesRowsEqualToOrAboveTheThreshold(string provider)
    {
        var repository = Repos.Create(provider);
        await repository.AddStockAsync(1, new StockAddRequest(12, 1, 2), Ct);
        var low = await repository.GetLowStockAsync(2, Ct);
        Assert.DoesNotContain(low, x => x.ItemId == 1 && x.VariantId == 12 && x.StoreId == 1);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task LowStock_DoesNotChangeStock(string provider)
    {
        var repository = Repos.Create(provider);
        var before = (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand);
        await repository.GetLowStockAsync(1000, Ct);
        Assert.Equal(before, (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Controller_RejectsAThresholdOutsideTheRange(string provider)
    {
        var controller = new ReferenceDataController(Repos.Create(provider));
        foreach (var threshold in new[] { 0, -1, 1001 })
            Assert.IsType<BadRequestObjectResult>((await controller.GetLowStock(threshold, Ct)).Result);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Controller_ReturnsTheListForTheDefaultThreshold(string provider)
    {
        var repository = Repos.Create(provider);
        var result = await new ReferenceDataController(repository).GetLowStock(cancellationToken: Ct);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<StockRecord>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((await repository.GetLowStockAsync(2, Ct)).Count, rows.Count);
    }
}
