// Role: Data shapes for the virtual try-on jewelry feature (separate from the ERP item catalog
// and from the chatbot). A JewelryItem is one uploaded 2D image with a name, a type
// (earring, necklace or ring) and an Active flag. ImageUrl is relative to the API address.

namespace NbJewel.Api.Jewelry;

public static class JewelryTypes
{
    public const string Earring = "earring";
    public const string Necklace = "necklace";
    public const string Ring = "ring";
    public static readonly IReadOnlyList<string> All = [Earring, Necklace, Ring];

    // Returns the canonical lower case type, or null when the value is not a known type.
    public static string? Resolve(string? value)
    {
        var trimmed = value?.Trim().ToLowerInvariant();
        return trimmed is not null && All.Contains(trimmed) ? trimmed : null;
    }
}

// Stored metadata. File is the server generated file name inside the images folder.
public sealed record JewelryRecord(string Id, string Name, string Type, string File, bool Active, DateTimeOffset CreatedAt);

// What the API returns to clients.
public sealed record JewelryItem(string Id, string Name, string Type, string ImageUrl, bool Active, DateTimeOffset CreatedAt);

public sealed record JewelryActiveRequest(bool Active);

// Settings for the file based repository. StoragePath comes from JEWELRY_STORAGE_PATH.
public sealed record JewelryStorageOptions(string StoragePath, string SeedFolder)
{
    public string ImagesFolder => Path.Combine(StoragePath, "images");
    public string MetadataFile => Path.Combine(StoragePath, "jewelry.json");
    // The public URL prefix where ImagesFolder is served as static files.
    public const string ImagesRequestPath = "/jewelry-images";
}
