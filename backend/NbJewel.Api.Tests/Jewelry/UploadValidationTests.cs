// Role: Tests the upload checks for try-on jewelry: the type is decided by content (a fake
// extension does not help), the 5 MB limit holds, empty files and bad names or types fail,
// and the controller returns 400 for bad uploads and never uses the client file name.

using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Jewelry;

namespace NbJewel.Api.Tests.Jewelry;

public sealed class UploadValidationTests
{
    [Fact]
    public void Png_IsDetectedFromContent() => Assert.Equal(".png", UploadValidator.DetectExtension(JewelryTestKit.Png()));

    [Fact]
    public void Jpeg_IsDetectedFromContent() => Assert.Equal(".jpg", UploadValidator.DetectExtension(JewelryTestKit.Jpeg()));

    [Fact]
    public void BadType_TextFileIsRejected()
    {
        var check = UploadValidator.Check("Stud", "earring", 20, "hello, not an image"u8);
        Assert.False(check.Ok);
        Assert.Equal("Only PNG or JPG images are allowed.", check.Error);
    }

    [Fact]
    public void BadType_GifIsRejected() => Assert.False(UploadValidator.Check("Stud", "earring", 6, "GIF89a"u8).Ok);

    [Fact]
    public void TooLarge_ContentOverFiveMegabytesIsRejected()
    {
        var check = UploadValidator.Check("Stud", "earring", UploadValidator.MaxBytes + 1, JewelryTestKit.Png((int)UploadValidator.MaxBytes + 1));
        Assert.False(check.Ok);
        Assert.Equal("The image is larger than 5 MB.", check.Error);
    }

    [Fact]
    public void TooLarge_DeclaredLengthOverLimitIsRejected() =>
        Assert.False(UploadValidator.Check("Stud", "earring", UploadValidator.MaxBytes + 1, JewelryTestKit.Png()).Ok);

    [Fact]
    public void ExactlyFiveMegabytes_IsAccepted() =>
        Assert.True(UploadValidator.Check("Stud", "earring", UploadValidator.MaxBytes, JewelryTestKit.Png((int)UploadValidator.MaxBytes)).Ok);

    [Fact]
    public void EmptyFile_IsRejected() => Assert.Equal("Choose an image file.", UploadValidator.Check("Stud", "ring", 0, []).Error);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\u0007bell")]
    public void BadName_IsRejected(string name) => Assert.False(UploadValidator.Check(name, "ring", 64, JewelryTestKit.Png()).Ok);

    [Fact]
    public void LongName_IsRejected() => Assert.False(UploadValidator.Check(new string('x', 61), "ring", 64, JewelryTestKit.Png()).Ok);

    [Theory]
    [InlineData("bracelet")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownJewelryType_IsRejected(string? type) => Assert.False(UploadValidator.Check("Band", type, 64, JewelryTestKit.Png()).Ok);

    [Fact]
    public void TypeAndName_AreCleaned()
    {
        var check = UploadValidator.Check("  Gold Band ", " RING ", 64, JewelryTestKit.Jpeg());
        Assert.True(check.Ok);
        Assert.Equal(("Gold Band", "ring", ".jpg"), (check.Name, check.Type, check.Extension));
    }

    [Fact]
    public async Task Controller_FakeExtension_TextNamedPngIsRejected()
    {
        var controller = JewelryTestKit.Controller(new FileJewelryRepository(JewelryTestKit.TempOptions()));
        var file = JewelryTestKit.File("<script>alert(1)</script>"u8.ToArray(), "evil.png");
        var result = await controller.Upload("4321", "Evil", "earring", file, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_RealPngWithWrongExtension_IsStoredAsPngUnderGeneratedName()
    {
        var repository = new FileJewelryRepository(JewelryTestKit.TempOptions());
        var controller = JewelryTestKit.Controller(repository);
        var file = JewelryTestKit.File(JewelryTestKit.Png(), "../../etc/passwd.exe");
        var result = await controller.Upload("4321", "Stud", "earring", file, CancellationToken.None);
        var item = Assert.IsType<JewelryItem>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal($"/jewelry-images/{item.Id}.png", item.ImageUrl);
        Assert.DoesNotContain("passwd", item.ImageUrl);
    }

    [Fact]
    public async Task Controller_TooLargeFile_Returns400()
    {
        var controller = JewelryTestKit.Controller(new FileJewelryRepository(JewelryTestKit.TempOptions()));
        var file = JewelryTestKit.File(JewelryTestKit.Png((int)UploadValidator.MaxBytes + 10), "big.png");
        var result = await controller.Upload("4321", "Big", "necklace", file, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_MissingFile_Returns400()
    {
        var controller = JewelryTestKit.Controller(new FileJewelryRepository(JewelryTestKit.TempOptions()));
        var result = await controller.Upload("4321", "None", "ring", null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
