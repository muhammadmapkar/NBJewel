// Role: Tests uploaded legacy jewelry persistence and public visibility.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Jewelry;

namespace NbJewel.Api.Tests.Jewelry;

public sealed class JewelryRepositoryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task OldBuiltInDrawingsAreRetiredWithoutRemovingUploads()
    {
        var options = JewelryTestKit.TempOptions();
        var repository = new FileJewelryRepository(options);
        var upload = await repository.AddAsync("Uploaded necklace", "necklace", JewelryTestKit.Png(), ".png", Ct);
        var old = new JewelryRecord("a1000000000000000000000000000003", "Old drawing", "necklace",
            "a1000000000000000000000000000003.png", true, DateTimeOffset.UnixEpoch);
        await File.WriteAllTextAsync(options.MetadataFile, System.Text.Json.JsonSerializer.Serialize(
            new[] { upload, old }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        var reopened = new FileJewelryRepository(options);
        Assert.Equal(upload.Id, Assert.Single(await reopened.ListAsync(activeOnly: true, Ct)).Id);
        Assert.NotNull(await reopened.GetAsync(old.Id, Ct));
    }

    [Fact]
    public async Task FirstStartHasNoBuiltInDrawings()
    {
        var options = JewelryTestKit.TempOptions();
        Assert.Empty(await new FileJewelryRepository(options).ListAsync(activeOnly: false, Ct));
        Assert.Empty(Directory.EnumerateFiles(options.ImagesFolder));
    }

    [Fact]
    public async Task Add_PersistsAcrossRestart()
    {
        var options = JewelryTestKit.TempOptions();
        var added = await new FileJewelryRepository(options).AddAsync("Test Stud", "earring", JewelryTestKit.Png(), ".png", Ct);
        var reloaded = await new FileJewelryRepository(options).GetAsync(added.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal("Test Stud", reloaded.Name);
        Assert.Single(await new FileJewelryRepository(options).ListAsync(false, Ct));
        Assert.Equal(JewelryTestKit.Png(), await File.ReadAllBytesAsync(Path.Combine(options.ImagesFolder, reloaded.File)));
    }

    [Fact]
    public async Task SetActive_False_HidesFromActiveList()
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        var first = await repository.AddAsync("Necklace", "necklace", JewelryTestKit.Png(), ".png", Ct);
        Assert.NotNull(await repository.SetActiveAsync(first.Id, false, Ct));
        Assert.DoesNotContain(await repository.ListAsync(true, Ct), x => x.Id == first.Id);
        Assert.Contains(await repository.ListAsync(false, Ct), x => x.Id == first.Id);
    }

    [Fact]
    public async Task Delete_RemovesMetadataAndFile()
    {
        var options = JewelryTestKit.TempOptions();
        var repository = new FileJewelryRepository(options);
        var added = await repository.AddAsync("Gone", "ring", JewelryTestKit.Jpeg(), ".jpg", Ct);
        Assert.True(await repository.DeleteAsync(added.Id, Ct));
        Assert.Null(await repository.GetAsync(added.Id, Ct));
        Assert.False(File.Exists(Path.Combine(options.ImagesFolder, added.File)));
        Assert.False(await repository.DeleteAsync(added.Id, Ct));
    }

    [Theory]
    [InlineData("../jewelry")]
    [InlineData("A1000000000000000000000000000001")]
    [InlineData("nope")]
    public async Task BadIds_AreUnknown(string id)
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        Assert.Null(await repository.GetAsync(id, Ct));
        Assert.Null(await repository.SetActiveAsync(id, false, Ct));
        Assert.False(await repository.DeleteAsync(id, Ct));
    }

    [Fact]
    public async Task PublicList_ReturnsActiveOnlyWithImageUrls()
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        var first = await repository.AddAsync("Necklace", "necklace", JewelryTestKit.Png(), ".png", Ct);
        var visible = await repository.AddAsync("Earrings", "earring", JewelryTestKit.Png(), ".png", Ct);
        await repository.SetActiveAsync(first.Id, false, Ct);
        var result = await JewelryTestKit.Controller(repository).GetActive(Ct);
        var items = Assert.IsAssignableFrom<IReadOnlyList<JewelryItem>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal($"/jewelry-images/{visible.File}", Assert.Single(items).ImageUrl);
    }

    [Fact]
    public async Task OwnerOperations_ReturnExpectedBusinessResults()
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        var controller = JewelryTestKit.Controller(repository);
        var id = (await repository.AddAsync("Necklace", "necklace", JewelryTestKit.Png(), ".png", Ct)).Id;
        Assert.IsType<OkObjectResult>((await controller.GetAll(Ct)).Result);
        Assert.IsType<OkObjectResult>((await controller.SetActive(id, new JewelryActiveRequest(false), Ct)).Result);
        Assert.IsType<NoContentResult>(await controller.Delete(id, Ct));
        Assert.IsType<NotFoundResult>(await controller.Delete(id, Ct));
    }
}
