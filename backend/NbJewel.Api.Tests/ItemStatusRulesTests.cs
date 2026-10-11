// Role: Tests the status rules: any of the four statuses can be set from any
// other (forward, backward, skipping), the same status is a harmless no-op,
// Complete is not final, and an unknown status returns 400. Any-to-any is assumed.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class ItemStatusRulesTests
{
    private static readonly string[] All = ["Draft", "With Smith", "Received", "Complete"];
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Theory]
    [InlineData("Draft")]
    [InlineData("With Smith")]
    [InlineData("Received")]
    [InlineData("Complete")]
    public void Resolve_AcceptsEachOfTheFourStatuses(string status) =>
        Assert.Equal(status, ItemStatusRules.Resolve(status));

    [Fact]
    public void Resolve_IsCaseInsensitiveAndReturnsCanonicalName() =>
        Assert.Equal("With Smith", ItemStatusRules.Resolve("  with smith "));

    [Theory]
    [InlineData("Pending")]
    [InlineData("Active")]
    [InlineData("Discontinued")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_RejectsUnknownStatusAsValidationError(string? requested)
    {
        var ex = Assert.Throws<DomainValidationException>(() => ItemStatusRules.Resolve(requested));
        Assert.Contains("Allowed statuses: Draft, With Smith, Received, Complete.", ex.Message);
    }

    [Fact]
    public async Task Repository_AllowsEveryMoveBetweenEveryPairOfStatuses()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(NewItem(), Ct);
        foreach (var from in All)
            foreach (var to in All)
            {
                await repository.UpdateStatusAsync(item.Id, from, Ct);
                var moved = await repository.UpdateStatusAsync(item.Id, to, Ct);
                Assert.Equal(to, moved!.Status);
                Assert.Equal(to, (await repository.GetItemAsync(item.Id, Ct))!.Status);
            }
    }

    [Fact]
    public async Task Repository_CompleteIsNotFinal_AndBackwardAndSkipMovesWork()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(NewItem(), Ct);
        Assert.Equal("Draft", item.Status);

        Assert.Equal("Complete", (await repository.UpdateStatusAsync(item.Id, "Complete", Ct))!.Status);   // skips two steps
        Assert.Equal("Received", (await repository.UpdateStatusAsync(item.Id, "Received", Ct))!.Status);   // back from Complete
        Assert.Equal("Draft", (await repository.UpdateStatusAsync(item.Id, "Draft", Ct))!.Status);         // back two steps
    }

    [Fact]
    public async Task Repository_SameStatusIsANoOp()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(NewItem(), Ct);
        await repository.UpdateStatusAsync(item.Id, "Received", Ct);

        var again = await repository.UpdateStatusAsync(item.Id, "received", Ct);

        Assert.Equal("Received", again!.Status);
    }

    [Fact]
    public async Task Repository_UnknownStatusIsRejectedAndLeavesTheItemUnchanged()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(NewItem(), Ct);
        await repository.UpdateStatusAsync(item.Id, "With Smith", Ct);

        await Assert.ThrowsAsync<DomainValidationException>(() => repository.UpdateStatusAsync(item.Id, "Pending", Ct));

        Assert.Equal("With Smith", (await repository.GetItemAsync(item.Id, Ct))!.Status);
    }

    [Fact]
    public async Task Repository_UpdateDoesNotChangeStatus()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(NewItem(), Ct);
        await repository.UpdateStatusAsync(item.Id, "With Smith", Ct);

        var updated = await repository.UpdateAsync(item.Id, new ItemUpdateRequest("T-1", "Renamed", 2, 1, "Gold", "22K", 5, 5, 0, "percent", 8, null, null), Ct);

        Assert.Equal("With Smith", updated!.Status);
        Assert.Equal("Renamed", updated.Name);
    }

    [Fact]
    public async Task Controller_ReturnsOkForABackwardMove()
    {
        var repository = SampleRepositoryFactory.Create();
        var controller = new ItemsController(repository);
        var item = await repository.CreateAsync(NewItem(), Ct);
        await repository.UpdateStatusAsync(item.Id, "Complete", Ct);

        var result = await controller.UpdateStatus(item.Id, new StatusUpdateRequest("Draft"), Ct);

        Assert.Equal("Draft", Assert.IsType<Item>(Assert.IsType<OkObjectResult>(result.Result).Value).Status);
    }

    [Fact]
    public async Task Controller_ReturnsOkForTheSameStatus()
    {
        var controller = new ItemsController(SampleRepositoryFactory.Create());
        var result = await controller.UpdateStatus(1, new StatusUpdateRequest("Complete"), Ct); // item 1 is already Complete in the sample data
        Assert.Equal("Complete", Assert.IsType<Item>(Assert.IsType<OkObjectResult>(result.Result).Value).Status);
    }

    [Fact]
    public async Task Controller_ReturnsBadRequestForUnknownStatusAndNotFoundForAMissingItem()
    {
        var controller = new ItemsController(SampleRepositoryFactory.Create());

        var bad = await controller.UpdateStatus(1, new StatusUpdateRequest("Pending"), Ct);
        Assert.Contains("Unknown status 'Pending'", Assert.IsType<BadRequestObjectResult>(bad.Result).Value as string);
        Assert.IsType<BadRequestObjectResult>((await controller.UpdateStatus(1, new StatusUpdateRequest("  "), Ct)).Result);
        Assert.IsType<NotFoundResult>((await controller.UpdateStatus(9999, new StatusUpdateRequest("With Smith"), Ct)).Result);
    }

    private static ItemCreateRequest NewItem() => new("T-1", "Test Item", 2, 1, "Gold", "22K", 5, 5, 0, "percent", 8, null, null);
}
