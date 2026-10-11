// Role: Tests stock entry. Adding stock ADDS to on-hand, a quantity of 0 or
// less is rejected, and item, variant, and store must all be valid. Uses item 1
// (variants 11, 12, 13) and item 3 (no variants) from the sample data.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class StockEntryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task AddStock_CreatesARecordWhenNoneExists()
    {
        var repository = SampleRepositoryFactory.Create();
        var record = await repository.AddStockAsync(1, new StockAddRequest(11, 1, 5), Ct);
        Assert.Equal(5, record!.OnHand);
        Assert.Equal(11, record.VariantId);
    }

    [Fact]
    public async Task AddStock_AddsToTheExistingQuantityInsteadOfReplacingIt()
    {
        var repository = SampleRepositoryFactory.Create();
        await repository.AddStockAsync(1, new StockAddRequest(11, 1, 5), Ct);
        var record = await repository.AddStockAsync(1, new StockAddRequest(11, 1, 3), Ct);
        Assert.Equal(8, record!.OnHand);
    }

    [Fact]
    public async Task AddStock_IsVisibleInGetStock()
    {
        var repository = SampleRepositoryFactory.Create();
        await repository.AddStockAsync(1, new StockAddRequest(12, 2, 7), Ct);
        var stock = await repository.GetStockAsync(1, Ct);
        Assert.Contains(stock, x => x.VariantId == 12 && x.StoreId == 2 && x.OnHand == 7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task AddStock_RejectsZeroAndNegativeQuantity(int quantity)
    {
        var repository = SampleRepositoryFactory.Create();
        var before = (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(11, 1, quantity), Ct));

        Assert.Equal("Quantity must be greater than 0.", ex.Message);
        Assert.Equal(before, (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand));
    }

    [Fact]
    public async Task AddStock_RejectsATotalThatWouldOverflow()
    {
        var repository = SampleRepositoryFactory.Create();
        await repository.AddStockAsync(1, new StockAddRequest(11, 1, int.MaxValue), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(11, 1, 1), Ct));
    }

    [Fact]
    public async Task AddStock_ReturnsNullForAMissingItem()
    {
        var repository = SampleRepositoryFactory.Create();
        Assert.Null(await repository.AddStockAsync(9999, new StockAddRequest(11, 1, 1), Ct));
    }

    [Fact]
    public async Task AddStock_RejectsAnUnknownStore()
    {
        var repository = SampleRepositoryFactory.Create();
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(11, 999, 1), Ct));
    }

    [Fact]
    public async Task AddStock_RequiresAVariantThatBelongsToTheItem()
    {
        var repository = SampleRepositoryFactory.Create();
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(null, 1, 1), Ct)); // item 1 has variants
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(21, 1, 1), Ct));   // 21 belongs to item 2
    }

    [Fact]
    public async Task AddStock_ForAnItemWithoutVariantsUsesNoVariant()
    {
        var repository = SampleRepositoryFactory.Create();
        var record = await repository.AddStockAsync(3, new StockAddRequest(null, 1, 2), Ct);
        Assert.Null(record!.VariantId);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(3, new StockAddRequest(11, 1, 2), Ct));
    }

    [Fact]
    public async Task Controller_MapsResultsToHttpCodes()
    {
        var controller = new ItemsController(SampleRepositoryFactory.Create());

        var ok = await controller.AddStock(1, new StockAddRequest(11, 1, 4), Ct);
        Assert.Equal(4, Assert.IsType<StockRecord>(Assert.IsType<OkObjectResult>(ok.Result).Value).OnHand);

        var bad = await controller.AddStock(1, new StockAddRequest(11, 1, 0), Ct);
        Assert.Equal("Quantity must be greater than 0.", Assert.IsType<BadRequestObjectResult>(bad.Result).Value);

        Assert.IsType<NotFoundResult>((await controller.AddStock(9999, new StockAddRequest(11, 1, 1), Ct)).Result);
    }

    [Fact]
    public async Task Designs_AreSeededWithTen()
    {
        var designs = await SampleRepositoryFactory.Create().GetDesignsAsync(Ct);
        Assert.Equal(10, designs.Count);
        Assert.Equal(designs.Count, designs.Select(x => x.DesignCode).Distinct().Count());
    }

    [Fact]
    public async Task Create_RejectsAnUnknownDesign()
    {
        var repository = SampleRepositoryFactory.Create();
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(new ItemCreateRequest("T-2", "X", 2, 999, "Gold", "22K", 5, 5, 0, "percent", 8, null, null), Ct));
    }

    [Fact]
    public async Task Create_FillsDesignCodeFromTheDesignMaster()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(new ItemCreateRequest("T-3", "X", 2, 2, "Gold", "22K", 5, 5, 0, "percent", 8, null, null), Ct);
        Assert.Equal("DSN-102", item.DesignCode);
    }
}
