// Role: Persists sample publication decisions and hashed audit identity as atomic local JSON.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Knowledge;
public sealed class FileProductPublicationStore(ItemCutoutOptions options) : IProductPublicationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private string Folder => Path.Combine(options.StoragePath, "publications");
    private sealed record Stored(ProductPublication Decision, string ActorHash, DateTimeOffset UpdatedAt);
    public async Task<IReadOnlyList<ProductPublication>> ListAsync(CancellationToken ct)
    {
        if (!Directory.Exists(Folder)) return [];
        var rows = new List<ProductPublication>();
        foreach (var path in Directory.GetFiles(Folder, "item-*.json"))
        {
            var stored = JsonSerializer.Deserialize<Stored>(await File.ReadAllTextAsync(path, ct), Json);
            if (stored?.Decision is not { ItemId: > 0 } row || (row.Published && !Guid.TryParseExact(row.AssetRevisionId, "N", out _)))
                throw new InvalidDataException("Invalid publication decision.");
            rows.Add(row);
        }
        return rows;
    }
    public async Task SaveAsync(ProductPublication decision, string? actor, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"item-{decision.ItemId}.json");
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var stored = new Stored(decision, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actor ?? "system"))), DateTimeOffset.UtcNow);
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(stored, Json), ct);
            File.Move(temp, path, overwrite: true);
        }
        finally { gate.Release(); }
    }
}
