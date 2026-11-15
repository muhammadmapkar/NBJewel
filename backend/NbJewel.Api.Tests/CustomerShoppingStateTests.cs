// Role: Proves SQL-safe in-row JSON migration, optimistic concurrency and independent customer shop state.
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Storefront;
namespace NbJewel.Api.Tests;
public sealed class CustomerShoppingStateTests
{
    [Fact]
    public async Task LegacyRowUpgradesInOneSaveKeepingProfileAndErpHistoryOnly()
    {
        await using var factory = new IdentityTestFactory(); var id = await factory.Seed("customer", false, []);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        var legacy = new CustomerState("Assumed customer", "12345678", [new("7", "erp"), new("old-demo", "demo")], [Reservation("erp"), Reservation("demo")]);
        db.CustomerPortals.Add(new() { Id = id, Revision = 4, Json = JsonSerializer.Serialize(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web)) }); await db.SaveChangesAsync();
        var portal = scope.ServiceProvider.GetRequiredService<CustomerPortal>();
        var first = await portal.ReadAsync(id, default);
        Assert.Equal("Assumed customer", first.State.Name); Assert.Equal("7", Assert.Single(first.State.Bag).Id); Assert.Equal("erp", Assert.Single(first.State.Reservations).Source);
        Assert.Equal(5, first.Row!.Revision);
        var json = JsonDocument.Parse(first.Row.Json); Assert.Equal(2, json.RootElement.GetProperty("version").GetInt32());
        Assert.True(json.RootElement.GetProperty("shops").TryGetProperty("nb-jewel-demo", out _));
        Assert.DoesNotContain("old-demo", first.Row.Json);
        var stored = first.Row.Json; var second = await portal.ReadAsync(id, default);
        Assert.Equal(5, second.Row!.Revision); Assert.Equal(stored, second.Row.Json);
    }
    [Fact]
    public async Task ExistingRevisionRejectsAConcurrentWriteWithoutLosingTheFirstSave()
    {
        await using var factory = new IdentityTestFactory(); var id = await factory.Seed("customer", false, []);
        using (var seed = factory.Services.CreateScope()) await seed.ServiceProvider.GetRequiredService<CustomerPortal>().SaveAsync(id, null, CustomerState.Empty, default);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var first = a.ServiceProvider.GetRequiredService<CustomerPortal>(); var second = b.ServiceProvider.GetRequiredService<CustomerPortal>();
        var left = await first.ReadAsync(id, default); var right = await second.ReadAsync(id, default);
        await first.SaveAsync(id, left.Row, left.State with { Name = "First writer" }, default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveAsync(id, right.Row, right.State with { Name = "Stale writer" }, default));
        using var fresh = factory.Services.CreateScope(); Assert.Equal("First writer", (await fresh.ServiceProvider.GetRequiredService<CustomerPortal>().ReadAsync(id, default)).State.Name);
    }
    [Fact]
    public async Task ConcurrentFirstWritesReturnARevisionConflictInsteadOfLosingState()
    {
        await using var factory = new IdentityTestFactory(); var id = await factory.Seed("customer", false, []);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var first = a.ServiceProvider.GetRequiredService<CustomerPortal>(); var second = b.ServiceProvider.GetRequiredService<CustomerPortal>();
        var left = await first.ReadAsync(id, default); var right = await second.ReadAsync(id, default);
        Assert.Null(left.Row); Assert.Null(right.Row);
        await first.SaveAsync(id, left.Row, left.State with { Name = "First writer" }, default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveAsync(id, right.Row, right.State with { Name = "Stale first write" }, default));
        using var fresh = factory.Services.CreateScope(); Assert.Equal("First writer", (await fresh.ServiceProvider.GetRequiredService<CustomerPortal>().ReadAsync(id, default)).State.Name);
    }
    private static CustomerReservation Reservation(string source) => new("NBJ-ASSUMED", Guid.NewGuid().ToString(), "Assumed customer", "12345678", "Assumed branch", "1", source, "2026-11-16", ["7"], DateTimeOffset.UnixEpoch);
    private static async Task Publish(KnowledgeHttpFactory factory, string slug)
    {
        var registry = factory.Services.GetRequiredService<BusinessRegistry>(); var partition = factory.Services.GetRequiredService<BusinessStorage>().For(registry.Find(slug)!);
        var revision = Guid.NewGuid().ToString("N"); var path = partition.Cutout.RevisionFolder(revision); Directory.CreateDirectory(path);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(SampleRepositoryFactory.FindRoot(), "sample-data", "jewelry", "necklace-gold-pendant.png"));
        await File.WriteAllBytesAsync(Path.Combine(path, "preview.png"), bytes); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        await partition.Assets.SaveAsync(new(7, revision, hash, hash, "ready", "approved", "open", 64, 64, DateTimeOffset.UtcNow), default);
        await partition.Publications.SaveAsync(new() { ItemId = 7, Published = true, AssetRevisionId = revision }, "test", default);
        var portals = factory.Services.GetRequiredService<PortalStore>();
        await portals.ChangeAsync(registry.Find(slug)!, doc => {
            var enabled = doc.Draft with { Revision = doc.Draft.Revision + 1, Settings = doc.Draft.Settings with { Enquiries = false, Bag = true, Reservations = true } };
            return Task.FromResult(doc with { Draft = enabled, Publication = enabled, Online = true });
        }, default);
    }
    [Fact]
    public async Task OneCustomerHasSeparateBagsAndForeignStoreReservationsAreRejected()
    {
        await using var factory = new KnowledgeHttpFactory { UseFilePublications = true }; await factory.Seed("customer", false, []);
        await Publish(factory, "nb-jewel-demo"); await Publish(factory, "sample-wholesale");
        using var client = factory.CreateClient(); await CustomerPortalHttpTests.Login(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/customer/profile", new { name = "Assumed global customer", phone = "12345678" })).StatusCode);
        Assert.Equal("Assumed global customer", (await client.GetFromJsonAsync<CustomerState>("/api/customer/shops/sample-wholesale/state"))!.Name);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/customer/shops/nb-jewel-demo/bag", new { id = "7", source = "erp" })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CustomerState>("/api/customer/shops/sample-wholesale/state"))!.Bag);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/customer/shops/sample-wholesale/bag", new { id = "7", source = "erp" })).StatusCode);
        var reservation = new { requestId = Guid.NewGuid().ToString(), storeId = "2", source = "erp", name = "Assumed customer", phone = "12345678", date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), items = new[] { "7" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/customer/shops/sample-wholesale/reservations", reservation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/customer/shops/sample-wholesale/bag/7")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CustomerState>("/api/customer/shops/sample-wholesale/state"))!.Bag);
        Assert.Single((await client.GetFromJsonAsync<CustomerState>("/api/customer/shops/nb-jewel-demo/state"))!.Bag);
        Assert.Single((await client.GetFromJsonAsync<CustomerState>("/api/customer/profile"))!.Bag);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/customer/shops/unknown-shop/state")).StatusCode);
    }
    [Fact]
    public async Task StaffCannotReadOrWriteCustomerShopState()
    {
        await using var factory = new KnowledgeHttpFactory(); await factory.Seed("erp", true, []); using var client = factory.CreateClient(); await CustomerPortalHttpTests.Login(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/customer/shops/nb-jewel-demo/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/customer/shops/sample-wholesale/bag", new { id = "7", source = "erp" })).StatusCode);
    }
}
