// Role: Test helpers for the try-on jewelry feature: tiny valid PNG and JPEG byte arrays,
// a fresh temp storage folder per test,
// and a controller built with a chosen OWNER_PIN.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using NbJewel.Api.Controllers;
using NbJewel.Api.Jewelry;

namespace NbJewel.Api.Tests.Jewelry;

internal static class JewelryTestKit
{
    // The 8 byte PNG signature followed by filler. Validation only reads the signature.
    public static byte[] Png(int size = 64)
    {
        var bytes = new byte[size];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    // Decodable photo fixture for tests that need to process or serve a PNG.
    public static readonly byte[] CutoutPhoto = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAlklEQVR4nO3awQmEUAAD0XGwDyuwMCuyMCuwkm8NIosM67sHArlmGmNQJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3ESJ3Hz3cCxL/zSup3/tYDESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESdz0/YVeJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3G+XeCpCyqgCXui4j6ZAAAAAElFTkSuQmCC");

    public static byte[] Jpeg(int size = 64)
    {
        var bytes = new byte[size];
        new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.CopyTo(bytes, 0);
        return bytes;
    }

    public static JewelryStorageOptions TempOptions()
    {
        var folder = Path.Combine(Path.GetTempPath(), "nbjewel-jewelry-" + Guid.NewGuid().ToString("N"));
        return new JewelryStorageOptions(folder);
    }

    public static OwnerPin Pin(string? value) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OWNER_PIN"] = value }).Build());

    public static JewelryController Controller(IJewelryRepository repository) =>
        new(repository) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    public static IFormFile File(byte[] content, string fileName)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "image/png" };
    }

    public static int? Status(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;
}
