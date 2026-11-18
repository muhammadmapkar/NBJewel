// Role: Stores uploaded legacy try-on images and metadata under JEWELRY_STORAGE_PATH.
// Files use generated names and a single lock guards writes.
// On hosts with a temporary disk, uploads are lost on restart (known limit, see docs).

using System.Text.Json;
using System.Text.RegularExpressions;

namespace NbJewel.Api.Jewelry;

public sealed partial class FileJewelryRepository : IJewelryRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly HashSet<string> RetiredSampleIds = new(StringComparer.Ordinal)
    {
        "a1000000000000000000000000000001", "a1000000000000000000000000000002",
        "a1000000000000000000000000000003", "a1000000000000000000000000000004",
        "a1000000000000000000000000000005", "a1000000000000000000000000000006"
    };
    private readonly JewelryStorageOptions options;
    private readonly SemaphoreSlim gate = new(1, 1);
    private List<JewelryRecord> records;

    public FileJewelryRepository(JewelryStorageOptions options)
    {
        this.options = options;
        Directory.CreateDirectory(options.ImagesFolder);
        records = File.Exists(options.MetadataFile)
            ? JsonSerializer.Deserialize<List<JewelryRecord>>(File.ReadAllText(options.MetadataFile), Json) ?? []
            : [];
    }

    // Ids are 32 lower case hex characters. Anything else is unknown, which also blocks path tricks.
    [GeneratedRegex("^[a-f0-9]{32}$")]
    private static partial Regex IdPattern();
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    public async Task<IReadOnlyList<JewelryRecord>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return records.Where(x => !activeOnly || (x.Active && !RetiredSampleIds.Contains(x.Id)))
                .OrderBy(x => x.Type).ThenBy(x => x.CreatedAt).ToList();
        }
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
        var imagePath = Path.Combine(options.ImagesFolder, record.File);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllBytesAsync(imagePath, content, cancellationToken);
            try { await CommitAsync([.. records, record], cancellationToken); }
            catch { File.Delete(imagePath); throw; } // no orphan image when the metadata could not be saved
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
            var updated = records[index] with { Active = active };
            var next = records.ToList();
            next[index] = updated;
            await CommitAsync(next, cancellationToken);
            return updated;
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
            await CommitAsync(records.Where(x => x.Id != id).ToList(), cancellationToken);
            var path = Path.Combine(options.ImagesFolder, Path.GetFileName(record.File));
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        finally { gate.Release(); }
    }

    // Saves the new list to disk first and only then swaps it in, so memory and disk never disagree.
    // Writes to a temp file first, then replaces, so a crash never leaves half a JSON file.
    private async Task CommitAsync(List<JewelryRecord> next, CancellationToken cancellationToken)
    {
        var temp = options.MetadataFile + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(next, Json), cancellationToken);
        File.Move(temp, options.MetadataFile, overwrite: true);
        records = next;
    }
}
