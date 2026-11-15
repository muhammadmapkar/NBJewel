// Role: Proves current published portal policies hide structured prices and reject stale customer actions and conversation history.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NbJewel.Api.Businesses;
using NbJewel.Api.Chat;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Platform;
namespace NbJewel.Api.Tests;
public sealed class PriceHidingHttpTests
{
    private static async Task<HttpClient> Admin(KnowledgeHttpFactory factory)
    {
        await factory.Seed("erp", true, ["portal.manage", "portal.publish", "items.read", "prices.read", "status.read"]);
        var client = factory.CreateClient(); await CustomerPortalHttpTests.Login(client); return client;
    }
    private static async Task PublishProduct(KnowledgeHttpFactory factory)
    {
        var asset = await PreparedStorefrontAsset.Create(factory, 7); using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<KnowledgeService>().SetProductAsync(7, true, default, "test", asset.RevisionId);
    }
    private static async Task Policy(HttpClient client, Action<JsonNode> change)
    {
        var draft = await PortalLifecycleHttpTests.Draft(client); change(draft["settings"]!);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(PortalLifecycleHttpTests.Root + "/draft", draft)).StatusCode);
        draft = await PortalLifecycleHttpTests.Draft(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(PortalLifecycleHttpTests.Root + "/publish", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
    }
    [Fact]
    public async Task HiddenModeRemovesPricesFromBothCatalogAliasesRatesAndWebsiteToolsButKeepsWeightsAndErpPrices()
    {
        await using var factory = new KnowledgeHttpFactory { UseFilePublications = true }; using var client = await Admin(factory); await PublishProduct(factory);
        await Policy(client, settings => settings["showPrices"] = false);
        foreach (var prefix in new[] { "/api/shops/nb-jewel-demo", "/api/storefront" })
        {
            var product = await client.GetFromJsonAsync<JsonElement>(prefix + "/products/7"); Assert.Equal(JsonValueKind.Null, product.GetProperty("price").ValueKind); Assert.True(product.GetProperty("netWeight").GetDecimal() > 0);
            var body = await client.GetStringAsync(prefix + "/products"); foreach (var field in new[] { "totalPrice", "ratePerGram", "makingCharge", "stoneValue", "vatAmount" }) Assert.DoesNotContain("\"" + field + "\"", body);
            var rates = await client.GetFromJsonAsync<JsonElement>(prefix + "/metal-rates"); Assert.True(rates.GetProperty("pricesHidden").GetBoolean()); Assert.Empty(rates.GetProperty("rates").EnumerateArray());
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/items/7/price")).StatusCode);
        using var scope = factory.Services.CreateScope(); var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(); var previous = accessor.HttpContext;
        accessor.HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider }; scope.ServiceProvider.GetRequiredService<BusinessContext>().ResolvePublic("nb-jewel-demo");
        try { var result = await scope.ServiceProvider.GetRequiredService<IAssistantTools>().RunAsync(new("website", "visitor:test", "visitor", [], [], false), "get_public_product", JsonNode.Parse("{\"itemId\":7}")!.AsObject(), default); Assert.Equal("ok", result["status"]!.GetValue<string>()); Assert.Null(result["data"]!["price"]); }
        finally { accessor.HttpContext = previous; }
    }
    [Fact]
    public async Task OfflineAndUnknownCatalogsAreIdenticalAndPreviewQueryDoesNotExposeTheDraft()
    {
        await using var factory = new KnowledgeHttpFactory(); using var client = await Admin(factory);
        var draft = await PortalLifecycleHttpTests.Draft(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(PortalLifecycleHttpTests.Root + "/offline", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
        foreach (var suffix in new[] { "", "/products", "/products/7", "/metal-rates", "/stores", "/products/7/preview" })
        {
            var offline = await client.GetAsync("/api/shops/nb-jewel-demo" + suffix + "?preview=true"); var missing = await client.GetAsync("/api/shops/unknown-shop" + suffix);
            Assert.Equal(HttpStatusCode.NotFound, offline.StatusCode); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); Assert.Equal(await missing.Content.ReadAsStringAsync(), await offline.Content.ReadAsStringAsync());
        }
    }
    [Fact]
    public async Task AnOldCustomerTabCannotBypassDisabledBagOrReservationActions()
    {
        await using var factory = new KnowledgeHttpFactory { UseFilePublications = true }; using var staff = await Admin(factory); await PublishProduct(factory);
        using var customer = factory.CreateClient(); await CustomerPortalHttpTests.Token(customer);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsJsonAsync("/api/auth/register", new { email = "customer@example.test", password = "PasswordExample123!" })).StatusCode); await CustomerPortalHttpTests.Token(customer);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsJsonAsync("/api/customer/shops/nb-jewel-demo/bag", new { id = "7", source = "erp" })).StatusCode);
        await Policy(staff, settings => { settings["bag"] = false; settings["reservations"] = false; });
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync("/api/customer/bag", new { id = "7", source = "erp" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.DeleteAsync("/api/customer/shops/nb-jewel-demo/bag/7")).StatusCode);
        var request = new { requestId = Guid.NewGuid().ToString(), storeId = "1", source = "erp", name = "Assumed customer", phone = "12345678", date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), items = new[] { "7" } };
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync("/api/customer/shops/nb-jewel-demo/reservations", request)).StatusCode);
    }
    private sealed class WebsiteModel : ILlmClient
    {
        public Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken ct) => Task.FromResult(messages.Any(x => x.Role == "tool")
            ? new LlmResponse("{\"answer\":\"Product details.\",\"sources\":[{\"tool\":\"get_public_product\",\"recordId\":\"7\"}]}", [])
            : new LlmResponse(null, [new("get_public_product", JsonNode.Parse("{\"itemId\":7}")!.AsObject())]));
    }
    [Fact]
    public async Task WebsiteHistoryCreatedUnderAnEarlierPricePolicyCannotBeReadAfterPricesAreHidden()
    {
        await using var original = new KnowledgeHttpFactory { UseFilePublications = true, TestConfiguration = new() { ["GEMINI_MODEL"] = "test" } };
        // Configure the same real HTTP host with only the external model substituted.
        await using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => { services.RemoveAll<ILlmClient>(); services.AddSingleton<ILlmClient, WebsiteModel>(); }));
        using var admin = await Admin(original); // Use the original host for its isolated catalog and portal; derived host has the same durable partition roots.
        await PublishProduct(original);
        using var guest = factory.CreateClient(); await CustomerPortalHttpTests.Token(guest);
        var create = await guest.PostAsJsonAsync("/api/ai/website/conversations", new { }); Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var answer = await guest.PostAsJsonAsync($"/api/ai/website/conversations/{id}/messages", new { text = "Show product 7", clientRequestId = Guid.NewGuid().ToString() }); Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Contains("AED", (await answer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("answer").GetString());
        await Policy(admin, settings => settings["showPrices"] = false);
        var history = await guest.GetAsync($"/api/ai/website/conversations/{id}/messages"); Assert.Equal(HttpStatusCode.Unauthorized, history.StatusCode);
        var next = await guest.PostAsJsonAsync("/api/ai/website/conversations", new { }); Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        var nextId = (await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var hidden = await guest.PostAsJsonAsync($"/api/ai/website/conversations/{nextId}/messages", new { text = "Show product 7", clientRequestId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode); var hiddenText = (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("answer").GetString()!;
        Assert.DoesNotContain("Price:", hiddenText); Assert.DoesNotContain("AED", hiddenText);
        Assert.Equal(HttpStatusCode.NoContent, (await guest.DeleteAsync($"/api/ai/website/conversations/{id}")).StatusCode);
    }
}
