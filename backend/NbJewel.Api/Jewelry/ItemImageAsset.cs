// Role: Defines durable ERP-item preview evidence, separate from legacy standalone jewelry.
// Ready means technically generated. Quality review stays pending and licensing stays open.
namespace NbJewel.Api.Jewelry;

public sealed record ItemImageAsset(int ItemId, string RevisionId, string SourceSha256,
    string? PreviewSha256, string State, string QualityReview, string LicenseStatus,
    int Width, int Height, DateTimeOffset CreatedAt, string? FailureCode = null);

public sealed record ItemCutoutOptions(string StoragePath, string PythonPath, string ScriptPath)
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public string RevisionFolder(string revision) => Path.Combine(StoragePath, "revisions", revision);
}
