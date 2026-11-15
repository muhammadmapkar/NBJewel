// Role: Stores global profile and assumed versioned per-shop shopping state in the existing Identity JSON row and Revision token.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
namespace NbJewel.Api.Storefront;
public sealed record BagEntry(string Id, string Source);
public sealed record CustomerReservation(string Reference, string RequestId, string Name, string Phone, string Branch, string StoreId, string Source, string Date, string[] Items, DateTimeOffset CreatedAt, string Status = "requested");
public sealed record CustomerState(string Name, string Phone, List<BagEntry> Bag, List<CustomerReservation> Reservations)
{
    public static CustomerState Empty => new("", "", [], []);
}
public sealed class CustomerPortal(IdentityDatabase db)
{
    // Assumed v2 JSON shape. No CustomerShoppingState table or SQL backfill is needed.
    private sealed record Profile(string Name, string Phone);
    private sealed record Shop(List<BagEntry> Bag, List<CustomerReservation> Reservations);
    private sealed record Document(int Version, Profile Profile, Dictionary<string, Shop> Shops);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static Document Empty => new(2, new("", ""), new(StringComparer.Ordinal));
    private static Document Decode(string raw, out bool upgraded)
    {
        using var json = JsonDocument.Parse(raw);
        upgraded = !json.RootElement.TryGetProperty("version", out var version);
        if (!upgraded)
        {
            if (!version.TryGetInt32(out var number) || number != 2) throw new InvalidDataException("Unsupported customer data version.");
            var current = JsonSerializer.Deserialize<Document>(raw, Json);
            if (current?.Profile is null || current.Profile.Name is null || current.Profile.Phone is null || current.Shops is null || current.Shops.Any(x => !BusinessRegistry.ValidSlug(x.Key) || x.Value is null || x.Value.Bag is null || x.Value.Reservations is null)) throw new InvalidDataException("Invalid customer data.");
            return current;
        }
        var legacy = JsonSerializer.Deserialize<CustomerState>(raw, Json) ?? CustomerState.Empty;
        return new(2, new(legacy.Name ?? "", legacy.Phone ?? ""), new(StringComparer.Ordinal) {
            [BusinessRegistry.DemoSlug] = new((legacy.Bag ?? []).Where(x => x is not null && x.Source == "erp").ToList(),
                (legacy.Reservations ?? []).Where(x => x is not null && x.Source == "erp").ToList()) });
    }
    public async Task<(CustomerPortalRecord? Row, CustomerState State)> ReadAsync(string id, CancellationToken ct, string slug = BusinessRegistry.DemoSlug)
    {
        if (!BusinessRegistry.ValidSlug(slug)) throw new ArgumentException("Invalid shop.");
        var row = await db.CustomerPortals.FindAsync([id], ct);
        if (row is null) return (null, CustomerState.Empty);
        var document = Decode(row.Json, out var upgraded);
        if (upgraded)
        {
            // Profile and all retained legacy ERP entries move together under the existing concurrency token.
            row.Json = JsonSerializer.Serialize(document, Json); row.Revision++;
            await db.SaveChangesAsync(ct);
        }
        var shop = document.Shops.GetValueOrDefault(slug) ?? new([], []);
        return (row, new(document.Profile.Name, document.Profile.Phone, shop.Bag, shop.Reservations));
    }
    public async Task SaveAsync(string id, CustomerPortalRecord? row, CustomerState state, CancellationToken ct, string slug = BusinessRegistry.DemoSlug)
    {
        if (!BusinessRegistry.ValidSlug(slug) || (row is not null && row.Id != id)) throw new ArgumentException("Invalid customer state owner or shop.");
        var document = row is null ? Empty : Decode(row.Json, out _);
        document = document with { Profile = new(state.Name, state.Phone) };
        document.Shops[slug] = new(state.Bag, state.Reservations);
        var inserting = row is null;
        if (row is null) { row = new() { Id = id }; db.CustomerPortals.Add(row); }
        row.Json = JsonSerializer.Serialize(document, Json); row.Revision++;
        try { await db.SaveChangesAsync(ct); }
        catch (ArgumentException error) when (inserting && db.Database.IsInMemory())
        { throw new DbUpdateConcurrencyException("Customer state was created concurrently.", error); }
        catch (DbUpdateException error) when (inserting && error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new DbUpdateConcurrencyException("Customer state was created concurrently.", error); }
    }
}
