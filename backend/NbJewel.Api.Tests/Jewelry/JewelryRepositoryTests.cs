// Role: Tests FileJewelryRepository and the PIN rules of JewelryController: first start seeds
// 6 sample items (2 per type), uploads persist across restarts, the Active toggle hides items
// from the public list, delete removes the image file, bad ids are unknown, and the owner
// endpoints need the right PIN (401 when wrong, 503 when OWNER_PIN is not set).

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Jewelry;

namespace NbJewel.Api.Tests.Jewelry;

public sealed class JewelryRepositoryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task FirstStart_SeedsSixItemsTwoPerType()
    {
        var options = JewelryTestKit.TempOptions();
        var items = await new FileJewelryRepository(options).ListAsync(activeOnly: false, Ct);
        Assert.Equal(6, items.Count);
        foreach (var type in JewelryTypes.All) Assert.Equal(2, items.Count(x => x.Type == type));
        Assert.All(items, x => Assert.True(File.Exists(Path.Combine(options.ImagesFolder, x.File))));
    }

    [Fact]
    public async Task SeedImages_AreTransparentPngs()
    {
        var options = JewelryTestKit.TempOptions();
        foreach (var item in await new FileJewelryRepository(options).ListAsync(false, Ct))
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(options.ImagesFolder, item.File));
            Assert.Equal(".png", UploadValidator.DetectExtension(bytes));
            Assert.Equal(6, bytes[25]); // IHDR color type 6 means RGBA, so the PNG has an alpha channel.
        }
    }

    [Fact]
    public async Task Add_PersistsAcrossRestart()
    {
        var options = JewelryTestKit.TempOptions();
        var added = await new FileJewelryRepository(options).AddAsync("Test Stud", "earring", JewelryTestKit.Png(), ".png", Ct);
        var reloaded = await new FileJewelryRepository(options).GetAsync(added.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal("Test Stud", reloaded.Name);
        Assert.Equal(7, (await new FileJewelryRepository(options).ListAsync(false, Ct)).Count);
    }

    [Fact]
    public async Task SetActive_False_HidesFromActiveList()
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        var first = (await repository.ListAsync(true, Ct))[0];
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
        var first = (await repository.ListAsync(true, Ct))[0];
        await repository.SetActiveAsync(first.Id, false, Ct);
        var result = await JewelryTestKit.Controller(repository).GetActive(Ct);
        var items = Assert.IsAssignableFrom<IReadOnlyList<JewelryItem>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(5, items.Count);
        Assert.All(items, x => Assert.StartsWith("/jewelry-images/", x.ImageUrl));
    }

    [Fact]
    public async Task OwnerEndpoints_WrongPin_Return401()
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        var controller = JewelryTestKit.Controller(repository, "4321");
        var id = (await repository.ListAsync(false, Ct))[0].Id;
        Assert.Equal(401, JewelryTestKit.Status((await controller.GetAll("1111", Ct)).Result!));
        Assert.Equal(401, JewelryTestKit.Status((await controller.GetAll(null, Ct)).Result!));
        Assert.Equal(401, JewelryTestKit.Status((await controller.SetActive(id, new JewelryActiveRequest(false), "0000", Ct)).Result!));
        Assert.Equal(401, JewelryTestKit.Status(await controller.Delete(id, "0000", Ct)));
        Assert.Equal(401, JewelryTestKit.Status((await controller.Upload("0000", "x", "ring", JewelryTestKit.File(JewelryTestKit.Png(), "a.png"), Ct)).Result!));
        Assert.Equal(6, (await repository.ListAsync(true, Ct)).Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task OwnerEndpoints_PinNotConfigured_Return503(string? configured)
    {
        var controller = JewelryTestKit.Controller(new FileJewelryRepository(JewelryTestKit.TempOptions()), configured);
        Assert.Equal(503, JewelryTestKit.Status((await controller.GetAll("", Ct)).Result!));
    }

    [Fact]
    public async Task OwnerEndpoints_RightPin_Work()
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        var controller = JewelryTestKit.Controller(repository, "4321");
        var id = (await repository.ListAsync(false, Ct))[0].Id;
        Assert.IsType<OkObjectResult>((await controller.GetAll("4321", Ct)).Result);
        Assert.IsType<OkObjectResult>((await controller.SetActive(id, new JewelryActiveRequest(false), "4321", Ct)).Result);
        Assert.IsType<NoContentResult>(await controller.Delete(id, "4321", Ct));
        Assert.IsType<NotFoundResult>(await controller.Delete(id, "4321", Ct));
    }
}
