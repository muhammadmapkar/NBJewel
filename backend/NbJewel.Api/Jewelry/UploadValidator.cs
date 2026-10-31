// Role: Checks a jewelry upload before anything is saved. The file type is decided by its
// first bytes (PNG or JPEG signature), never by the client file name, extension or content
// type header. Enforces the 5 MB limit, a non-empty file, a known type and a sensible name.

namespace NbJewel.Api.Jewelry;

public sealed record UploadCheck(bool Ok, string? Error, string? Extension, string? Type, string? Name)
{
    public static UploadCheck Fail(string error) => new(false, error, null, null, null);
}

public static class UploadValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxNameLength = 60;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    // Returns ".png", ".jpg" or null, from the content only.
    public static string? DetectExtension(ReadOnlySpan<byte> content)
    {
        if (content.Length >= PngSignature.Length && content[..PngSignature.Length].SequenceEqual(PngSignature)) return ".png";
        if (content.Length >= JpegSignature.Length && content[..JpegSignature.Length].SequenceEqual(JpegSignature)) return ".jpg";
        return null;
    }

    // declaredLength is the size the client claimed (IFormFile.Length), checked before reading.
    public static UploadCheck Check(string? name, string? type, long declaredLength, ReadOnlySpan<byte> content)
    {
        var cleanName = name?.Trim() ?? "";
        if (cleanName.Length == 0) return UploadCheck.Fail("Enter a name.");
        if (cleanName.Length > MaxNameLength) return UploadCheck.Fail($"The name can have at most {MaxNameLength} characters.");
        if (cleanName.Any(char.IsControl)) return UploadCheck.Fail("The name has characters that are not allowed.");
        var cleanType = JewelryTypes.Resolve(type);
        if (cleanType is null) return UploadCheck.Fail("Choose a type: earring, necklace or ring.");
        if (declaredLength > MaxBytes || content.Length > MaxBytes) return UploadCheck.Fail("The image is larger than 5 MB.");
        if (content.Length == 0) return UploadCheck.Fail("Choose an image file.");
        var extension = DetectExtension(content);
        if (extension is null) return UploadCheck.Fail("Only PNG or JPG images are allowed.");
        return new UploadCheck(true, null, extension, cleanType, cleanName);
    }
}
