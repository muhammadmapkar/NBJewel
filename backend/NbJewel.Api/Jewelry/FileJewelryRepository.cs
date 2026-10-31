// Role: Stores try-on jewelry on disk. Metadata lives in one JSON file (jewelry.json) and the
// images in an "images" folder, both under JEWELRY_STORAGE_PATH. On first start (no metadata
// file yet) it copies the 6 sample items from sample-data/jewelry. File names are generated
// (id plus extension); the client file name is never used. One lock guards all writes.
// On hosts with a temporary disk, uploads are lost on restart (known limit, see docs).

using System.Text.Json;
using System.Text.RegularExpressions;

namespace NbJewel.Api.Jewelry;

public sealed partial class FileJewelryRepository : IJewelryRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly JewelryStorageOptions options;
    private readonly SemaphoreSlim gate = new(1, 1);
    private List<JewelryRecord> records;

    public FileJewelryRepository(JewelryStorageOptions options)
    {
        this.options = options;
        Directory.CreateDirectory(options.ImagesFolder);
        if (!File.Exists(options.MetadataFile)) Seed();
        records = JsonSerializer.Deserialize<List<JewelryRecord>>(File.ReadAllText(options.MetadataFile), Json) ?? [];
    }

    // Ids are 32 lower case hex characters. Anything else is unknown, which also blocks path tricks.
    [GeneratedRegex("^[a-f0-9]{32}$")]
    private static partial Regex IdPattern();
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    public async Task<IReadOnlyList<JewelryRecord>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { return records.Where(x => !activeOnly || x.Active).OrderBy(x => x.Type).ThenBy(x => x.CreatedAt).ToList(); }
        finally { gate.Release(); }
    }

    public async Task<JewelryRecord?> GetAsync(string id, CancellationToken cancellationToken)
    {
        if (!IsValidId(id)) return null;
        await gate.WaitAsync(cancellationToken);
        try { return records.FirstOrDefault(x => x.Id == id); }
        finally { gate.Release(); }
    }

    public async Task<JewelryRecord> AddAsync(string name, string type, byte[] content, string extension, CancellationToken cancellationToken)
    {
        if (extension is not (".png" or ".jpg")) throw new ArgumentException("Unsupported extension.", nameof(extension));
        if (JewelryTypes.Resolve(type) is not { } cleanType) throw new ArgumentException("Unknown type.", nameof(type));
        var id = Guid.NewGuid().ToString("N");
        var record = new JewelryRecord(id, name, cleanType, id + extension, true, DateTimeOffset.UtcNow);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(options.ImagesFolder, record.File), content, cancellationToken);
            records.Add(record);
            await SaveAsync(cancellationToken);
            return record;
        }
        finally { gate.Release(); }
    }

    public async Task<JewelryRecord?> SetActiveAsync(string id, bool active, CancellationToken cancellationToken)
    {
        if (!IsValidId(id)) return null;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var index = records.FindIndex(x => x.Id == id);
            if (index < 0) return null;
            records[index] = records[index] with { Active = active };
            await SaveAsync(cancellationToken);
            return records[index];
        }
        finally { gate.Release(); }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (!IsValidId(id)) return false;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var record = records.FirstOrDefault(x => x.Id == id);
            if (record is null) return false;
            records.Remove(record);
            await SaveAsync(cancellationToken);
            var path = Path.Combine(options.ImagesFolder, Path.GetFileName(record.File));
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        finally { gate.Release(); }
    }

    // Writes to a temp file first, then replaces, so a crash never leaves half a JSON file.
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var temp = options.MetadataFile + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(records, Json), cancellationToken);
        File.Move(temp, options.MetadataFile, overwrite: true);
    }

    private sealed record SeedEntry(string Id, string Name, string Type, string File, bool Active);

    private void Seed()
    {
        var seeded = new List<JewelryRecord>();
        var seedFile = Path.Combine(options.SeedFolder, "seed.json");
        if (File.Exists(seedFile))
        {
            var entries = JsonSerializer.Deserialize<List<SeedEntry>>(File.ReadAllText(seedFile), Json) ?? [];
            var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            foreach (var (entry, index) in entries.Select((e, i) => (e, i)))
            {
                var source = Path.Combine(options.SeedFolder, Path.GetFileName(entry.File));
                if (!IsValidId(entry.Id) || JewelryTypes.Resolve(entry.Type) is not { } type || !File.Exists(source)) continue;
                var bytes = File.ReadAllBytes(source);
                if (UploadValidator.DetectExtension(bytes) is not { } extension) continue;
                var file = entry.Id + extension;
                File.WriteAllBytes(Path.Combine(options.ImagesFolder, file), bytes);
                seeded.Add(new JewelryRecord(entry.Id, entry.Name, type, file, entry.Active, created.AddMinutes(index)));
            }
        }
        File.WriteAllText(options.MetadataFile, JsonSerializer.Serialize(seeded, Json));
    }
}
