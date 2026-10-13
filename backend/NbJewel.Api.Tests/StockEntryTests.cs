// Role: Tests stock entry. Adding stock ADDS to on-hand, a quantity of 0 or
// less is rejected, and item, variant, and store must all be valid. Uses item 1
// (variants 11, 12, 13) and item 3 (no variants) from the sample data. Variant 11
// already holds seeded stock, so tests that need an empty start use variant 12.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class StockEntryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_CreatesARecordWhenNoneExists(string provider)
    {
        var repository = Repos.Create(provider);
        var record = await repository.AddStockAsync(1, new StockAddRequest(12, 1, 5), Ct);
        Assert.Equal(5, record!.OnHand);
        Assert.Equal(12, record.VariantId);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_AddsToTheExistingQuantityInsteadOfReplacingIt(string provider)
    {
        var repository = Repos.Create(provider);
        await repository.AddStockAsync(1, new StockAddRequest(12, 1, 5), Ct);
        var record = await repository.AddStockAsync(1, new StockAddRequest(12, 1, 3), Ct);
        Assert.Equal(8, record!.OnHand);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_IsVisibleInGetStock(string provider)
    {
        var repository = Repos.Create(provider);
        await repository.AddStockAsync(1, new StockAddRequest(12, 2, 7), Ct);
        var stock = await repository.GetStockAsync(1, Ct);
        Assert.Contains(stock, x => x.VariantId == 12 && x.StoreId == 2 && x.OnHand == 7);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_RejectsZeroAndNegativeQuantity(string provider)
    {
        var repository = Repos.Create(provider);
        var before = (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand);

        foreach (var quantity in new[] { 0, -1, -100 })
        {
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(12, 1, quantity), Ct));
            Assert.Equal("Quantity must be greater than 0.", ex.Message);
        }
        Assert.Equal(before, (await repository.GetStockAsync(1, Ct)).Sum(x => x.OnHand));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_RejectsATotalThatWouldOverflow(string provider)
    {
        var repository = Repos.Create(provider);
        await repository.AddStockAsync(1, new StockAddRequest(12, 1, int.MaxValue), Ct);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(12, 1, 1), Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_ReturnsNullForAMissingItem(string provider)
    {
        var repository = Repos.Create(provider);
        Assert.Null(await repository.AddStockAsync(9999, new StockAddRequest(12, 1, 1), Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_RejectsAnUnknownStore(string provider)
    {
        var repository = Repos.Create(provider);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(12, 999, 1), Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_RequiresAVariantThatBelongsToTheItem(string provider)
    {
        var repository = Repos.Create(provider);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(null, 1, 1), Ct)); // item 1 has variants
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(1, new StockAddRequest(21, 1, 1), Ct));   // 21 belongs to item 2
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task AddStock_ForAnItemWithoutVariantsUsesNoVariant(string provider)
    {
        var repository = Repos.Create(provider);
        var record = await repository.AddStockAsync(3, new StockAddRequest(null, 1, 2), Ct);
        Assert.Null(record!.VariantId);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.AddStockAsync(3, new StockAddRequest(12, 1, 2), Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Controller_MapsResultsToHttpCodes(string provider)
    {
        var controller = new ItemsController(Repos.Create(provider));

        var ok = await controller.AddStock(1, new StockAddRequest(12, 1, 4), Ct);
        Assert.Equal(4, Assert.IsType<StockRecord>(Assert.IsType<OkObjectResult>(ok.Result).Value).OnHand);

        var bad = await controller.AddStock(1, new StockAddRequest(12, 1, 0), Ct);
        Assert.Equal("Quantity must be greater than 0.", Assert.IsType<BadRequestObjectResult>(bad.Result).Value);

        Assert.IsType<NotFoundResult>((await controller.AddStock(9999, new StockAddRequest(12, 1, 1), Ct)).Result);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Designs_AreSeededWithTen(string provider)
    {
        var designs = await Repos.Create(provider).GetDesignsAsync(Ct);
        Assert.Equal(10, designs.Count);
        Assert.Equal(designs.Count, designs.Select(x => x.DesignCode).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_RejectsAnUnknownDesign(string provider)
    {
        var repository = Repos.Create(provider);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(new ItemCreateRequest("T-2", "X", 2, 999, "Gold", "22K", 5, 5, 0, "percent", 8, null, null), Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_FillsDesignCodeFromTheDesignMaster(string provider)
    {
        var repository = Repos.Create(provider);
        var item = await repository.CreateAsync(new ItemCreateRequest("T-3", "X", 2, 2, "Gold", "22K", 5, 5, 0, "percent", 8, null, null), Ct);
        Assert.Equal("DSN-102", item.DesignCode);
    }
}
