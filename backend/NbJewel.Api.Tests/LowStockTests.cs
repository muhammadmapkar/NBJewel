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

    [Fact]
    public async Task LowStock_IncludesRowsBelowTheThreshold()
    {
        var repository = SampleRepositoryFactory.Create();
        await repository.AddStockAsync(1, new StockAddRequest(12, 1, 1), Ct);
        var low = await repository.GetLowStockAsync(2, Ct);
        Assert.Contains(low, x => x.ItemId == 1 && x.VariantId == 12 && x.StoreId == 1);
        Assert.All(low, x => Assert.True(x.OnHand - x.Reserved < 2));
    }

    [Fact]
    public async Task LowStock_ExcludesRowsEqualToOrAboveTheThreshold()
    {
        var repository = SampleRepositoryFactory.Create();
        await repository.AddStockAsync(1, new StockAddRequest(12, 1, 2), Ct);
        var low = await repository.GetLowStockAsync(2, Ct);
        Assert.DoesNotContain(low, x => x.ItemId == 1 && x.VariantId == 12 && x.StoreId == 1);
    }

    [Fact]
    public async Task LowStock_DoesNotChangeStock()
    {
        var repository = SampleRepositoryFactory.Create();
        var before = (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand);
        await repository.GetLowStockAsync(1000, Ct);
        Assert.Equal(before, (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task Controller_RejectsAThresholdOutsideTheRange(int threshold)
    {
        var controller = new ReferenceDataController(SampleRepositoryFactory.Create());
        var result = await controller.GetLowStock(threshold, Ct);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_ReturnsTheListForTheDefaultThreshold()
    {
        var repository = SampleRepositoryFactory.Create();
        var result = await new ReferenceDataController(repository).GetLowStock(cancellationToken: Ct);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<StockRecord>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((await repository.GetLowStockAsync(2, Ct)).Count, rows.Count);
    }
}
