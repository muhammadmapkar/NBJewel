// Role: Reserves sample item IDs on the asset filesystem so restart cannot attach persisted
// image approval/publication to an unrelated new item. Item records themselves stay in memory.
using System.Globalization;
using System.Text.Json;
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Repositories;

internal sealed class SampleItemIdAllocator(ItemCutoutOptions options)
{
    public int ReserveAfter(int minimum)
    {
        Directory.CreateDirectory(options.StoragePath);
        // Exclusive OS file sharing also protects simultaneous hosts using this sample storage.
        // Contention or invalid metadata fails closed rather than guessing an available ID.
        using var gate = new FileStream(Path.Combine(options.StoragePath, "sample-item-id.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var path = Path.Combine(options.StoragePath, "sample-item-last-id.json");
        var last = File.Exists(path) ? JsonSerializer.Deserialize<int>(File.ReadAllText(path)) : 0;
        if (last < 0) throw new InvalidDataException("Invalid sample item ID reservation.");
        last = Math.Max(last, minimum);
        // Preserve old asset/publication IDs even when upgrading storage without a watermark.
        foreach (var name in new[] { "links", "publications" })
        {
            var folder = Path.Combine(options.StoragePath, name);
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "item-*.json"))
            {
                var value = Path.GetFileNameWithoutExtension(file)[5..];
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1)
                    throw new InvalidDataException("Invalid persisted sample item ID.");
                last = Math.Max(last, id);
            }
        }
        var next = checked(last + 1);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(output, next);
            output.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
        return next;
    }
}
