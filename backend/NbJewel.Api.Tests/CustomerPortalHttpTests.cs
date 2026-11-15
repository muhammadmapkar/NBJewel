// Role: Exercises real customer sessions, CSRF, ownership, availability and idempotent reservation requests against isolated sample data.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NbJewel.Api.Storefront;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Knowledge;
namespace NbJewel.Api.Tests;
public class CustomerPortalHttpTests
{
    internal static async Task Token(HttpClient client) {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
    internal static async Task Login(HttpClient client) { await Token(client); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "PasswordExample123!" })).StatusCode); await Token(client); }
    [Fact] public async Task Only_customer_with_csrf_can_write_own_data() {
        await using var factory = new KnowledgeHttpFactory(); using var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/customer/profile")).StatusCode);
        await factory.Seed("erp", true, ["items.write"]); using var staff = factory.CreateClient(); await Login(staff);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/customer/profile")).StatusCode);
        await using var customerFactory = new KnowledgeHttpFactory(); await customerFactory.Seed("customer", false, []); using var customer = customerFactory.CreateClient(); await Login(customer);
        customer.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); Assert.Equal(HttpStatusCode.BadRequest, (await customer.PutAsJsonAsync("/api/customer/profile", new { name = "Sample", phone = "12345678" })).StatusCode);
    }
    [Fact] public async Task Bag_and_reservation_are_validated_saved_and_idempotent() {
        await using var factory = new KnowledgeHttpFactory(); await factory.Seed("customer", false, []); using var client = factory.CreateClient(); await Login(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/customer/profile", new { name = "Sample customer", phone = "12345678" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/customer/bag", new { id = "made-up", source = "demo" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/customer/bag", new { id = "temple-jhumka-necklace", source = "demo" })).StatusCode);
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeDatabase>();
            foreach (var itemId in new[] { 7, 9 }) {
                var asset = await PreparedStorefrontAsset.Create(factory, itemId);
                db.Products.Add(new ProductPublication { ItemId = itemId, Published = true, AssetRevisionId = asset.RevisionId });
            }
            await db.SaveChangesAsync();
        }
        var add = new { id = "7", source = "erp" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/customer/bag", add)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/customer/bag", add)).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<CustomerState>("/api/customer/profile"))!.Bag);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/customer/bag", new { id = "9", source = "erp" })).StatusCode);
        var request = new { requestId = Guid.NewGuid().ToString(), storeId = "1", source = "erp", name = "Sample customer", phone = "12345678", items = new[] { "7" }, date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/customer/reservations", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/customer/reservations", request)).StatusCode);
        var state = (await client.GetFromJsonAsync<CustomerState>("/api/customer/profile"))!; Assert.Single(state.Bag); Assert.Equal("9", state.Bag[0].Id); Assert.Single(state.Reservations); Assert.Equal("requested", state.Reservations[0].Status);
        using var second = factory.CreateClient(); await Token(second);
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsJsonAsync("/api/auth/register", new { email = "second@example.test", password = "PasswordExample123!" })).StatusCode);
        Assert.Empty((await second.GetFromJsonAsync<CustomerState>("/api/customer/profile"))!.Reservations);
        Assert.Equal("", (await second.GetFromJsonAsync<CustomerState>("/api/customer/profile"))!.Name);
    }
    [Fact] public async Task Public_stores_and_market_fallback_need_no_login() {
        await using var factory = new KnowledgeHttpFactory(); using var client = factory.CreateClient();
        var stores = (await client.GetFromJsonAsync<PublicStore[]>("/api/storefront/stores"))!;
        Assert.Equal(3, stores.Length); Assert.All(stores, s => { Assert.True(s.Location.IsSample); Assert.False(s.LiveSynced); });
        Assert.Equal("unconfigured", (await client.GetFromJsonAsync<SpotSnapshot>("/api/storefront/spot-rates"))!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/storefront/products?storeId=999")).StatusCode);
    }
    [Fact] public void Future_or_old_syncs_are_not_live() {
        var now = DateTimeOffset.UtcNow;
        Assert.False(StoreDirectory.Fresh(now.AddHours(1), now)); Assert.False(StoreDirectory.Fresh(now.AddMinutes(-16), now)); Assert.False(StoreDirectory.Fresh(null, now)); Assert.True(StoreDirectory.Fresh(now.AddMinutes(-2), now));
    }
}
