// Role: Stores each customer's profile, bag and reservation requests in their Identity database row with optimistic concurrency.
using System.Text.Json;
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
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<(CustomerPortalRecord? Row, CustomerState State)> ReadAsync(string id, CancellationToken ct)
    {
        var row = await db.CustomerPortals.FindAsync([id], ct);
        return (row, row is null ? CustomerState.Empty : JsonSerializer.Deserialize<CustomerState>(row.Json, Json) ?? throw new InvalidOperationException("Invalid customer data."));
    }
    public async Task SaveAsync(string id, CustomerPortalRecord? row, CustomerState state, CancellationToken ct)
    {
        if (row is null) { row = new() { Id = id }; db.CustomerPortals.Add(row); }
        row.Json = JsonSerializer.Serialize(state, Json); row.Revision++;
        await db.SaveChangesAsync(ct);
    }
}
