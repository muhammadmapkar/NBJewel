// Role: Saves an ERP item's uploaded source and runs only the existing cutout-jewelry.py
// script. Stores durable processing evidence; never publishes or approves preview quality.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Jewelry;

public sealed class ItemCutoutPipeline(IItemRepository items, IItemImageAssetRepository assets, ItemCutoutOptions options)
{
    public const int MaxCameraTrimPercent = 49;
    private const string CameraTrimFile = "camera-trim.json";
    private readonly SemaphoreSlim gate = new(1, 1);
    private sealed record Report(int Width, int Height, bool HasTransparency, bool HasForeground);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ItemImageAsset?> ProcessAsync(int itemId, byte[] content, CancellationToken ct)
    {
        var item = await items.GetItemAsync(itemId, ct);
        if (item is null) return null;
        var category = (await items.GetCategoriesAsync(ct)).FirstOrDefault(x => x.Id == item.CategoryId)?.Name;
        if (category is not ("Necklaces" or "Earrings"))
            throw new DomainValidationException("Image previews currently support necklaces and earrings only.");
        if (content.Length == 0 || content.Length > UploadValidator.MaxBytes || UploadValidator.DetectExtension(content) is not { } extension)
            throw new DomainValidationException("Choose a PNG or JPG image of at most 5 MB.");
        await gate.WaitAsync(ct);
        try
        {
            var revision = Guid.NewGuid().ToString("N");
            var folder = options.RevisionFolder(revision); Directory.CreateDirectory(folder);
            var source = Path.Combine(folder, "source" + extension);
            var preview = Path.Combine(folder, "preview.png");
            var reportPath = Path.Combine(folder, "report.json");
            await File.WriteAllBytesAsync(source, content, ct);
            var pending = new ItemImageAsset(itemId, revision, Convert.ToHexString(SHA256.HashData(content)),
                null, "processing", "pending", "open", 0, 0, DateTimeOffset.UtcNow);
            await assets.SaveAsync(pending, ct); // A crash cannot leave a prior revision marked as this new upload.
            ItemImageAsset result;
            try
            {
                var info = new ProcessStartInfo(options.PythonPath)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true, WorkingDirectory = folder
                };
                foreach (var arg in new[] { options.ScriptPath, source, preview, "--metadata", reportPath }) info.ArgumentList.Add(arg);
                using var process = Process.Start(info) ?? throw new InvalidOperationException("Cutout process did not start.");
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(options.Timeout);
                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                    ct.ThrowIfCancellationRequested();
                    throw new TimeoutException("Cutout processing timed out.");
                }
                await Task.WhenAll(stdout, stderr); // Process details stay server-side, never in the API result.
                if (process.ExitCode != 0) throw new InvalidDataException("Cutout processing failed.");
                var report = JsonSerializer.Deserialize<Report>(await File.ReadAllTextAsync(reportPath, ct), Json);
                if (report is null || report.Width < 1 || report.Height < 1 || (long)report.Width * report.Height > 16_000_000 ||
                    !report.HasTransparency || !report.HasForeground)
                    throw new InvalidDataException("A visible transparent preview was not generated.");
                var bytes = await File.ReadAllBytesAsync(preview, ct);
                if (UploadValidator.DetectExtension(bytes) != ".png") throw new InvalidDataException("Invalid preview.");
                result = pending with { State = "ready", Width = report.Width, Height = report.Height,
                    PreviewSha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                result = pending with { State = "failed", FailureCode = "preview_processing_failed" };
            }
            await assets.SaveAsync(result, ct);
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<byte[]?> PreviewAsync(int itemId, CancellationToken ct, string? revisionId = null)
    {
        if (await items.GetItemAsync(itemId, ct) is null) return null;
        var asset = await assets.GetAsync(itemId, ct);
        if (asset is not { State: "ready", PreviewSha256: not null } || (revisionId is not null && revisionId != asset.RevisionId)) return null;
        return await ReadPreviewAsync(asset, ct);
    }

    private async Task<byte[]?> ReadPreviewAsync(ItemImageAsset asset, CancellationToken ct)
    {
        var path = Path.Combine(options.RevisionFolder(asset.RevisionId), "preview.png");
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return Convert.ToHexString(SHA256.HashData(bytes)) == asset.PreviewSha256 ? bytes : null;
    }

    public async Task<int?> ReadCameraTrimAsync(ItemImageAsset asset, CancellationToken ct)
    {
        var path = Path.Combine(options.RevisionFolder(asset.RevisionId), CameraTrimFile);
        if (!File.Exists(path)) return null;
        var value = JsonSerializer.Deserialize<int>(await File.ReadAllTextAsync(path, ct));
        if (value is < 0 or > MaxCameraTrimPercent) throw new InvalidDataException("Invalid camera trim.");
        return value;
    }

    public async Task<ItemImageAsset?> SaveCameraTrimAsync(int itemId, string expectedRevision, int trimPercent, CancellationToken ct)
    {
        var item = await items.GetItemAsync(itemId, ct);
        if (item is null) return null;
        var category = (await items.GetCategoriesAsync(ct)).FirstOrDefault(x => x.Id == item.CategoryId)?.Name;
        if (category != "Necklaces" || trimPercent is < 0 or > MaxCameraTrimPercent)
            throw new DomainValidationException("Choose a necklace trim between 0 and 49 percent.");

        var current = await assets.GetAsync(itemId, ct);
        if (current is not { State: "ready", PreviewSha256: not null } || current.RevisionId != expectedRevision ||
            await ReadPreviewAsync(current, ct) is not { } preview) throw new ItemAssetConflictException();

        var next = current with { RevisionId = Guid.NewGuid().ToString("N"), QualityReview = "pending", CreatedAt = DateTimeOffset.UtcNow };
        var folder = options.RevisionFolder(next.RevisionId);
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, "preview.png"), preview, ct);
        await File.WriteAllTextAsync(Path.Combine(folder, CameraTrimFile), JsonSerializer.Serialize(trimPercent), ct);
        foreach (var name in new[] { "source.png", "source.jpg" })
        {
            var source = Path.Combine(options.RevisionFolder(current.RevisionId), name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(folder, name));
        }
        await assets.ReplaceAsync(next, expectedRevision, ct);
        return next;
    }
}
