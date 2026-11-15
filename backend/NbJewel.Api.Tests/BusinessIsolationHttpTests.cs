// Role: Exercises overlapping Sample IDs through real ERP, publication, knowledge and assistant HTTP boundaries.
using System.Net;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NbJewel.Api.Chat;
using NbJewel.Api.Platform;
using NbJewel.Api.Storefront;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Tests;

public sealed class BusinessIsolationHttpTests
{
    private static KnowledgeHttpFactory Factory() => new() { UseFilePublications = true, TestConfiguration = new() { ["GEMINI_MODEL"] = "test" } };
    private static async Task<HttpClient> Staff(KnowledgeHttpFactory factory)
    {
        await factory.Seed("erp", true, ["items.read", "items.write", "prices.read", "stock.read", "stock.write", "status.read", "status.write", "reference.read", "knowledge.manage", "chat.read", "jewelry.manage"]);
        var client = factory.CreateClient(); await BusinessMembershipHttpTests.Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "PasswordExample123!" })).StatusCode);
        await BusinessMembershipHttpTests.Token(client); return client;
    }
    private static async Task Move(KnowledgeHttpFactory factory, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        var member = await db.Set<BusinessMembership>().SingleAsync();
        member.BusinessId = slug; await db.SaveChangesAsync();
    }
    [Fact]
    public async Task DirectorySkipsAStoreThatDoesNotBelongToItsBusiness()
    {
        await using var factory = Factory(); using var client = await Staff(factory);
        await Move(factory, "sample-wholesale");
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@example.test");
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user!.Id)], "test")) };
        Assert.True(await scope.ServiceProvider.GetRequiredService<BusinessContext>().ResolveErpAsync(http, default));
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(); var previous = accessor.HttpContext;
        accessor.HttpContext = http;
        try { var rows = await scope.ServiceProvider.GetRequiredService<StoreDirectory>().ReadAsync(default); Assert.Equal("1", Assert.Single(rows).Location.Id); Assert.Contains("wholesale", rows[0].Location.Name); }
        finally { accessor.HttpContext = previous; }
    }
    private sealed class GroundedModel : ILlmClient
    {
        public string Evidence { get; private set; } = "";
        public Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken ct) { Evidence = string.Join("\n", messages.Where(m => m.Role == "tool").Select(m => m.ToolResult?.ToJsonString())); return Task.FromResult(messages.Any(m => m.Role == "tool")
            ? new LlmResponse("{\"answer\":\"Assumed wholesale necklace.\",\"sources\":[{\"tool\":\"get_item_details\",\"recordId\":\"7\"}]}", [])
            : new LlmResponse(null, [new("get_item_details", JsonNode.Parse("{\"itemId\":7}")!.AsObject())])); }
    }
    [Fact]
    public async Task ErpChatUsesOnlyTheBusinessRepositoryAndRevokedMembershipIsDenied()
    {
        var model = new GroundedModel();
        await using var original = Factory();
        await using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => { services.RemoveAll<ILlmClient>(); services.AddSingleton<ILlmClient>(model); }));
        // The derived host shares the factory configuration, not the original host's in-memory identity.
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Email = "admin@example.test", UserName = "admin@example.test", AccountType = "erp", AllStores = true };
        Assert.True((await users.CreateAsync(user, "PasswordExample123!")).Succeeded);
        await scope.ServiceProvider.GetRequiredService<BusinessMemberships>().AssignAsync(user.Id, "sample-wholesale", "owner", default);
        using var client = factory.CreateClient(); await BusinessMembershipHttpTests.Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "PasswordExample123!" })).StatusCode);
        await BusinessMembershipHttpTests.Token(client);
        var response = await client.PostAsJsonAsync("/api/chat", new { question = "Show item 7" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("WS-ONLY", model.Evidence);
        var body = await response.Content.ReadAsStringAsync(); Assert.Contains("Assumed wholesale necklace", body); Assert.DoesNotContain("NKL-0007", body);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>(); db.Remove(await db.Set<BusinessMembership>().SingleAsync()); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/ai/erp/conversations", new { })).StatusCode);
    }
    [Fact]
    public async Task AProvisionedBusinessWithoutCatalogFixturesStartsEmptyWithoutDemoFallback()
    {
        await using var factory = Factory(); using var client = await Staff(factory);
        factory.Services.GetRequiredService<BusinessRegistry>().Add("empty-shop", "Assumed empty shop", "retailer");
        await Move(factory, "empty-shop");
        var response = await client.GetAsync("/api/items"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/7")).StatusCode);
    }
    [Theory]
    [InlineData("/api/items/7", "code", "WS-ONLY")]
    [InlineData("/api/items/7/price", "ratePerGram", "1")]
    public async Task OverlappingIdReadsOnlyTheMemberPartition(string path, string field, string expected)
    {
        await using var factory = Factory(); using var client = await Staff(factory);
        await Move(factory, "sample-wholesale");
        var body = await client.GetFromJsonAsync<JsonElement>(path + "?businessId=nb-jewel-demo&slug=nb-jewel-demo");
        Assert.Equal(expected, body.GetProperty(field).ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/1")).StatusCode);
    }
    [Fact]
    public async Task ReferencesStockAndWritesCannotEscapeTheMemberPartition()
    {
        await using var factory = Factory(); using var client = await Staff(factory);
        var original = await client.GetStringAsync("/api/items/7");
        var originalEight = await client.GetStringAsync("/api/items/8");
        var originalStock = await client.GetStringAsync("/api/items/7/stock");
        await Move(factory, "sample-wholesale");
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/items")).GetProperty("totalCount").GetInt32());
        foreach (var route in new[] { "categories", "designs", "stores", "metal-rates/latest" })
            Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/" + route)).EnumerateArray());
        var stores = await client.GetStringAsync("/api/stores"); Assert.Contains("Assumed wholesale branch", stores); Assert.DoesNotContain("Mumbai", stores);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/items/7/stock")).EnumerateArray());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/items/7/variants")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/stock/low")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/items/7/stock", new { storeId = 2, quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync("/api/items/1/status", new { status = "Draft" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/items/7/status", new { status = "Draft" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/items/7/stock", new { storeId = 1, quantity = 3 })).StatusCode);
        var edit = new { code = "WS-ONLY", name = "Assumed edited wholesale necklace", categoryId = 3, designId = 1, metalType = "Gold", purity = "22K", grossWt = 10, netWt = 10, stoneWt = 0, makingType = "PERCENT", makingValue = 8 };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/items/7", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/items", new { code = "WS-NEW", name = "Assumed created wholesale necklace", categoryId = 3, designId = 1, metalType = "Gold", purity = "22K", grossWt = 10, netWt = 10, stoneWt = 0, makingType = "PERCENT", makingValue = 8 })).StatusCode);
        await Move(factory, "nb-jewel-demo");
        Assert.Equal(originalStock, await client.GetStringAsync("/api/items/7/stock"));
        Assert.Equal(originalEight, await client.GetStringAsync("/api/items/8"));
        Assert.Equal(original, await client.GetStringAsync("/api/items/7"));
    }
    [Fact]
    public async Task WholesaleCannotManageTheLegacyDemoJewelryModule()
    {
        await using var factory = Factory(); using var client = await Staff(factory);
        await Move(factory, "sample-wholesale");
        foreach (var path in new[] { "/api/jewelry/all" }) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync("/api/jewelry/known/active", new { active = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/jewelry/known")).StatusCode);
        using var upload = new MultipartFormDataContent();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/jewelry", upload)).StatusCode);
    }
    [Fact]
    public async Task KnowledgeAndConversationOwnershipFollowTheBusinessAfterMembershipChanges()
    {
        await using var factory = Factory(); using var client = await Staff(factory);
        var document = await client.PostAsJsonAsync("/api/ai/knowledge/documents", new { title = "Demo-only knowledge", text = "Assumed demo care", collection = "public", audience = "public", confirm = true });
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        var docId = (await document.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var conversation = await client.PostAsJsonAsync("/api/ai/erp/conversations", new { });
        Assert.Equal(HttpStatusCode.Created, conversation.StatusCode);
        var id = (await conversation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        await Move(factory, "sample-wholesale");
        var docs = await client.GetStringAsync("/api/ai/knowledge/documents"); Assert.DoesNotContain(docId!, docs); Assert.DoesNotContain("Demo-only", docs);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{docId}/revoke", new { confirm = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/ai/knowledge/documents/{docId}", new { title = "Rejected edit", text = "Cannot edit demo", collection = "public", audience = "public", confirm = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{docId}/publish", new { confirm = true, versionId = "foreign-version" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/ai/erp/conversations/{id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/ai/erp/conversations/{id}/messages", new { text = "Show item 7", clientRequestId = Guid.NewGuid().ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/ai/erp/conversations/{id}")).StatusCode);
    }
    [Fact]
    public async Task UploadedRevisionAndPublicationNeverReachTheDemoPartitionOrLegacyPublicAliases()
    {
        await using var factory = Factory(); using var client = await Staff(factory);
        var demoAsset = await PreparedStorefrontAsset.Create(factory, 7);
        await Move(factory, "sample-wholesale");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/items/7/image/preview?revisionId={demoAsset.RevisionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/items/7/image/analyze", new { revisionId = demoAsset.RevisionId })).StatusCode);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAlklEQVR4nO3awQmEUAAD0XGwDyuwMCuyMCuwkm8NIosM67sHArlmGmNQJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3ESJ3Hz3cCxL/zSup3/tYDESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESdz0/YVeJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3G+XeCpCyqgCXui4j6ZAAAAAElFTkSuQmCC"));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(file, "file", "sample.png");
        var uploaded = await client.PostAsync("/api/items/7/image", form); Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var revision = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionId").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/items/7/image/review", new { revisionId = demoAsset.RevisionId, approve = true, confirm = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/items/7/image/review", new { revisionId = revision, approve = true, confirm = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/ai/knowledge/products/7/publish", new { assetRevisionId = revision, confirm = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/items/7/image/preview?revisionId=" + revision)).StatusCode);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/ai/knowledge/products/7"); Assert.True(status.GetProperty("published").GetBoolean());
        var legacy = await client.GetStringAsync("/api/storefront/products?slug=sample-wholesale&businessId=sample-wholesale"); Assert.DoesNotContain("WS-ONLY", legacy); Assert.DoesNotContain("Assumed wholesale", legacy);
        await Move(factory, "nb-jewel-demo");
        var asset = await client.GetFromJsonAsync<JsonElement>("/api/items/7/image"); Assert.Equal(demoAsset.RevisionId, asset.GetProperty("revisionId").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/7/image/preview?revisionId=" + revision)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/ai/knowledge/products/7")).GetProperty("published").GetBoolean());
    }
}
