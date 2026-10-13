// Role: Tests item create and update on every provider. Net weight is always
// gross minus stone and the NetWt a client sends is ignored. A stone weight above
// the gross weight returns 400. Also covers the other write rules both providers
// must share: unique code, known category, design and purity, and the image link.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class ItemWriteTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static ItemCreateRequest Create(string code = "T-100", decimal gross = 10m, decimal stone = 1.5m, decimal net = 999m, string? image = null, string making = "percent") =>
        new(code, "Test Item", 2, 1, "Gold", "22K", gross, net, stone, making, 8, null, image);

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_IgnoresTheClientNetWeightAndComputesGrossMinusStone(string provider)
    {
        var repository = Repos.Create(provider);

        var item = await repository.CreateAsync(Create(gross: 10m, stone: 1.5m, net: 999m), Ct);

        Assert.Equal(8.5m, item.NetWt);
        Assert.Equal(8.5m, (await repository.GetItemAsync(item.Id, Ct))!.NetWt);
        Assert.Equal(8.5m, (await repository.GetPriceAsync(item.Id, Ct))!.NetWeight);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Update_IgnoresTheClientNetWeightAndComputesGrossMinusStone(string provider)
    {
        var repository = Repos.Create(provider);
        var item = await repository.CreateAsync(Create(), Ct);

        var updated = await repository.UpdateAsync(item.Id, new ItemUpdateRequest("T-100", "Test Item", 2, 1, "Gold", "22K", 20m, 0m, 2.25m, "percent", 8, null, null), Ct);

        Assert.Equal(17.75m, updated!.NetWt);
        Assert.Equal(17.75m, (await repository.GetItemAsync(item.Id, Ct))!.NetWt);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task SampleItems_AllHaveNetWeightEqualToGrossMinusStone(string provider)
    {
        var items = (await Repos.Create(provider).SearchItemsAsync(null, null, null, null, null, 1, 100, Ct)).Items;
        Assert.Equal(30, items.Count);
        Assert.All(items, x => Assert.Equal(x.GrossWt - x.StoneWt, x.NetWt));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_RejectsStoneHeavierThanGross_AndSavesNothing(string provider)
    {
        var repository = Repos.Create(provider);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create(gross: 5m, stone: 5.001m), Ct));

        Assert.Equal("Stone weight cannot be greater than gross weight.", ex.Message);
        Assert.Equal(30, (await repository.SearchItemsAsync(null, null, null, null, null, 1, 100, Ct)).TotalCount);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Update_RejectsStoneHeavierThanGross_AndLeavesTheItemAsItWas(string provider)
    {
        var repository = Repos.Create(provider);
        var item = await repository.CreateAsync(Create(), Ct);

        await Assert.ThrowsAsync<DomainValidationException>(() => repository.UpdateAsync(item.Id, new ItemUpdateRequest("T-100", "Changed", 2, 1, "Gold", "22K", 5m, 5m, 6m, "percent", 8, null, null), Ct));

        var after = (await repository.GetItemAsync(item.Id, Ct))!;
        Assert.Equal("Test Item", after.Name);
        Assert.Equal(8.5m, after.NetWt);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Controller_ReturnsBadRequestWhenStoneIsHeavierThanGross(string provider)
    {
        var controller = new ItemsController(Repos.Create(provider));

        var create = await controller.Create(Create(gross: 5m, stone: 6m), Ct);
        Assert.Equal("Stone weight cannot be greater than gross weight.", Assert.IsType<BadRequestObjectResult>(create.Result).Value);

        var update = await controller.Update(1, new ItemUpdateRequest("RNG-0142", "Zork Ring", 2, 1, "Gold", "22K", 5m, 5m, 6m, "percent", 8, null, null), Ct);
        Assert.IsType<BadRequestObjectResult>(update.Result);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_AllowsStoneEqualToGrossAndGivesZeroNet(string provider)
    {
        var item = await Repos.Create(provider).CreateAsync(Create(gross: 5m, stone: 5m), Ct);
        Assert.Equal(0m, item.NetWt);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_RejectsZeroGrossNegativeStoneAndBadMakingType(string provider)
    {
        var repository = Repos.Create(provider);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create(gross: 0m, stone: 0m), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create(stone: -1m), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create(making: "weird"), Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_RejectsDuplicateCodeUnknownCategoryAndUnknownPurity(string provider)
    {
        var repository = Repos.Create(provider);
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create(code: "RNG-0142"), Ct));   // already used by item 1
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create(code: "rng-0142"), Ct));   // codes ignore case
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create() with { CategoryId = 999 }, Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create() with { Purity = "99K" }, Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(Create() with { MetalType = "Silver", Purity = "22K" }, Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Update_KeepsItsOwnCodeButRejectsAnotherItemsCode(string provider)
    {
        var repository = Repos.Create(provider);
        var item = await repository.CreateAsync(Create(), Ct);

        Assert.NotNull(await repository.UpdateAsync(item.Id, new ItemUpdateRequest("T-100", "Renamed", 2, 1, "Gold", "22K", 10m, 0m, 1m, "percent", 8, null, null), Ct));
        await Assert.ThrowsAsync<DomainValidationException>(() => repository.UpdateAsync(item.Id, new ItemUpdateRequest("RNG-0142", "Renamed", 2, 1, "Gold", "22K", 10m, 0m, 1m, "percent", 8, null, null), Ct));
        Assert.Null(await repository.UpdateAsync(9999, new ItemUpdateRequest("T-101", "Nobody", 2, 1, "Gold", "22K", 10m, 0m, 1m, "percent", 8, null, null), Ct));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task ImageUrl_IsSavedOnCreateChangedOnUpdateAndClearedWithNull(string provider)
    {
        var repository = Repos.Create(provider);
        var item = await repository.CreateAsync(Create(image: "/images/a.jpg"), Ct);
        Assert.Equal("/images/a.jpg", (await repository.GetItemAsync(item.Id, Ct))!.ImageUrl);

        ItemUpdateRequest Update(string? image) => new("T-100", "Test Item", 2, 1, "Gold", "22K", 10m, 0m, 1.5m, "percent", 8, null, image);
        Assert.Equal("/images/b.jpg", (await repository.UpdateAsync(item.Id, Update("/images/b.jpg"), Ct))!.ImageUrl);
        Assert.Null((await repository.UpdateAsync(item.Id, Update(null), Ct))!.ImageUrl);
        Assert.Equal("/images/c.jpg", (await repository.UpdateAsync(item.Id, Update("/images/c.jpg"), Ct))!.ImageUrl);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_ReturnsTheNextIdAndStartsAsDraft(string provider)
    {
        var item = await Repos.Create(provider).CreateAsync(Create(), Ct);
        Assert.Equal(31, item.Id);
        Assert.Equal("Draft", item.Status);
        Assert.Equal("DSN-ZORK", item.DesignCode);
    }
}
