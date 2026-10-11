// Role: Tests the forward-only status workflow (Draft, With Smith, Received,
// Complete) at the rule level and through the sample repository and controller,
// including the 409 response for a bad move. The workflow is assumed.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class ItemStatusRulesTests
{
    [Theory]
    [InlineData("Draft", "With Smith")]
    [InlineData("With Smith", "Received")]
    [InlineData("Received", "Complete")]
    public void EnsureMove_AllowsTheNextStatus(string current, string next) =>
        Assert.Equal(next, ItemStatusRules.EnsureMove(current, next));

    [Fact]
    public void EnsureMove_IsCaseInsensitiveAndReturnsCanonicalName() =>
        Assert.Equal("With Smith", ItemStatusRules.EnsureMove("draft", "  with smith "));

    [Theory]
    [InlineData("With Smith", "Draft")]     // backward
    [InlineData("Received", "With Smith")]  // backward
    [InlineData("Complete", "Received")]    // backward
    [InlineData("Draft", "Received")]       // skips a step
    [InlineData("Draft", "Complete")]       // skips two steps
    [InlineData("With Smith", "Complete")]  // skips a step
    [InlineData("Draft", "Draft")]          // same status
    [InlineData("Complete", "Complete")]    // same status, last step
    public void EnsureMove_RejectsEveryOtherMove(string current, string requested)
    {
        var ex = Assert.Throws<InvalidStatusMoveException>(() => ItemStatusRules.EnsureMove(current, requested));
        Assert.Contains("Cannot move from", ex.Message);
    }

    [Fact]
    public void EnsureMove_ErrorNamesTheNextAllowedStatus()
    {
        var ex = Assert.Throws<InvalidStatusMoveException>(() => ItemStatusRules.EnsureMove("Draft", "Complete"));
        Assert.Equal("Cannot move from Draft to Complete. Next allowed status is With Smith.", ex.Message);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Active")]
    [InlineData("")]
    public void EnsureMove_RejectsUnknownRequestedStatusAsValidationError(string requested) =>
        Assert.Throws<DomainValidationException>(() => ItemStatusRules.EnsureMove("Draft", requested));

    [Fact]
    public void EnsureMove_RejectsMovingFromAStatusOutsideTheWorkflow() =>
        Assert.Throws<InvalidStatusMoveException>(() => ItemStatusRules.EnsureMove("Active", "Draft"));

    [Fact]
    public async Task Repository_WalksTheWholeWorkflowAndThenRefusesToGoBack()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(NewItem(), CancellationToken.None);
        Assert.Equal("Draft", item.Status);

        foreach (var next in new[] { "With Smith", "Received", "Complete" })
            Assert.Equal(next, (await repository.UpdateStatusAsync(item.Id, next, CancellationToken.None))!.Status);

        await Assert.ThrowsAsync<InvalidStatusMoveException>(() => repository.UpdateStatusAsync(item.Id, "Received", CancellationToken.None));
        Assert.Equal("Complete", (await repository.GetItemAsync(item.Id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task Repository_UpdateDoesNotChangeStatus()
    {
        var repository = SampleRepositoryFactory.Create();
        var item = await repository.CreateAsync(NewItem(), CancellationToken.None);
        await repository.UpdateStatusAsync(item.Id, "With Smith", CancellationToken.None);

        var updated = await repository.UpdateAsync(item.Id, new ItemUpdateRequest("T-1", "Renamed", 2, 1, "Gold", "22K", 5, 5, 0, "percent", 8, null, null), CancellationToken.None);

        Assert.Equal("With Smith", updated!.Status);
        Assert.Equal("Renamed", updated.Name);
    }

    [Fact]
    public async Task Controller_ReturnsConflictForABackwardMove()
    {
        var repository = SampleRepositoryFactory.Create();
        var controller = new ItemsController(repository);
        var item = await repository.CreateAsync(NewItem(), CancellationToken.None);
        await repository.UpdateStatusAsync(item.Id, "With Smith", CancellationToken.None);

        var result = await controller.UpdateStatus(item.Id, new StatusUpdateRequest("Draft"), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("Cannot move from With Smith to Draft. Next allowed status is Received.", conflict.Value);
    }

    [Fact]
    public async Task Controller_ReturnsBadRequestForAnUnknownStatusAndNotFoundForAMissingItem()
    {
        var repository = SampleRepositoryFactory.Create();
        var controller = new ItemsController(repository);

        Assert.IsType<BadRequestObjectResult>((await controller.UpdateStatus(1, new StatusUpdateRequest("Pending"), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.UpdateStatus(9999, new StatusUpdateRequest("With Smith"), CancellationToken.None)).Result);
    }

    private static ItemCreateRequest NewItem() => new("T-1", "Test Item", 2, 1, "Gold", "22K", 5, 5, 0, "percent", 8, null, null);
}
