// Role: Commits assumed portal draft, publication, online state and audit together using one atomic file per business in both providers.
using System.Collections.Concurrent;
using System.Text.Json;
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Businesses;
public sealed class PortalStore(BusinessStorage storage)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string PathFor(Business business) => Path.Combine(storage.Root(business), "portal.json");
    public async Task<PortalDocument> ReadAsync(Business business, CancellationToken ct)
    {
        var path = PathFor(business);
        if (File.Exists(path)) return await Load(path, ct);
        return await ChangeAsync(business, document => Task.FromResult(document), ct, initializeOnly: true);
    }
    private static async Task<PortalDocument> Load(string path, CancellationToken ct) =>
        JsonSerializer.Deserialize<PortalDocument>(await File.ReadAllTextAsync(path, ct), Json) ?? throw new InvalidDataException("Invalid portal state.");
    public async Task<PortalDocument> ChangeAsync(Business business, Func<PortalDocument, Task<PortalDocument>> change, CancellationToken ct, bool initializeOnly = false)
    {
        var gate = gates.GetOrAdd(business.Id, _ => new(1, 1)); await gate.WaitAsync(ct);
        try
        {
            var path = PathFor(business); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Persistent lock file is retained. FileShare.None coordinates writers on the same durable filesystem.
            await using var fileLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var exists = File.Exists(path);
            var current = exists ? await Load(path, ct) : await Seed(business, ct);
            if (initializeOnly && exists) return current;
            var next = await change(current);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(next, Json), ct);
            File.Move(temp, path, true);
            return next;
        }
        finally { gate.Release(); }
    }
    private async Task<PortalDocument> Seed(Business business, CancellationToken ct)
    {
        var branches = (await storage.For(business).Items.GetStoresAsync(ct)).Select(x => x.Id).ToArray();
        var draft = new PortalRevision(1, PortalSettings.Preset(business, branches), "assumed-seed", DateTimeOffset.UnixEpoch);
        return new(draft, business.Slug == BusinessRegistry.DemoSlug ? draft : null, business.Slug == BusinessRegistry.DemoSlug, []);
    }
    public async Task<string> UploadAsync(Business business, byte[] bytes, CancellationToken ct)
    {
        var extension = UploadValidator.DetectExtension(bytes);
        if (bytes.Length is 0 or > (int)UploadValidator.MaxBytes || extension is null) throw new PortalValidationException("Choose a PNG or JPG image of at most 5 MB.");
        var id = Guid.NewGuid().ToString("N"); var folder = Path.Combine(storage.Root(business), "portal-assets"); Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, id + extension), bytes, ct); return id;
    }
    public string? Asset(Business business, string? id)
    {
        if (id is null || !Guid.TryParseExact(id, "N", out _)) return null;
        foreach (var extension in new[] { ".png", ".jpg" }) { var path = Path.Combine(storage.Root(business), "portal-assets", id + extension); if (File.Exists(path)) return path; }
        return null;
    }
}
