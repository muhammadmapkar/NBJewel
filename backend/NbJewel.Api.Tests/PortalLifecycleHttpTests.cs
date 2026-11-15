// Role: Proves assumed private drafts, revision-safe atomic publication, asset boundaries and portal permission checks.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Identity;
using NbJewel.Api.Businesses;
namespace NbJewel.Api.Tests;
public sealed class PortalLifecycleHttpTests
{
    internal const string Root = "/api/businesses/nb-jewel-demo/portal";
    internal static async Task<HttpClient> Admin(KnowledgeHttpFactory factory, bool publish = true)
    {
        await factory.Seed("erp", true, publish ? ["portal.manage", "portal.publish"] : ["portal.manage"]);
        var client = factory.CreateClient(); await CustomerPortalHttpTests.Login(client); return client;
    }
    internal static async Task<JsonObject> Draft(HttpClient client, string root = Root) => (await client.GetFromJsonAsync<JsonObject>(root + "/draft"))!;
    [Fact]
    public async Task StaleDraftAndPublishRevisionsCannotOverwriteAnotherEditorsWork()
    {
        await using var factory = new KnowledgeHttpFactory(); using var client = await Admin(factory);
        var original = await Draft(client); var revision = original["revision"]!.GetValue<long>();
        original["settings"]!["headline"] = "Assumed new headline";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + "/draft", original)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(Root + "/draft", original)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Root + "/publish", new { revision })).StatusCode);
        Assert.DoesNotContain("Assumed new headline", await client.GetStringAsync("/api/shops/nb-jewel-demo?preview=true"));
        var latest = await Draft(client); Assert.Equal("Assumed new headline", latest["settings"]!["headline"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Root + "/publish", new { revision = latest["revision"]!.GetValue<long>() })).StatusCode);
        var before = await client.GetStringAsync("/api/shops/nb-jewel-demo");
        latest = await Draft(client); latest["settings"]!["enquiries"] = true; latest["settings"]!["whatsApp"] = "";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + "/draft", latest)).StatusCode);
        latest = await Draft(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Root + "/publish", new { revision = latest["revision"]!.GetValue<long>() })).StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/api/shops/nb-jewel-demo"));
    }
    [Theory]
    [InlineData("featuredItemIds")]
    [InlineData("visibleBranchIds")]
    [InlineData("logoAssetId")]
    public async Task ForeignOrUnavailableReferencesCannotBePublished(string field)
    {
        await using var factory = new KnowledgeHttpFactory(); using var client = await Admin(factory);
        var before = await client.GetStringAsync("/api/shops/nb-jewel-demo"); var draft = await Draft(client);
        draft["settings"]![field] = field == "logoAssetId" ? JsonValue.Create(new string('a', 32)) : new JsonArray(999999);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + "/draft", draft)).StatusCode); draft = await Draft(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Root + "/publish", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/api/shops/nb-jewel-demo"));
    }
    [Fact]
    public async Task DraftAssetsStayPrivateUntilReferencedByTheLivePublicationAndOfflineIsIndistinguishable()
    {
        await using var factory = new KnowledgeHttpFactory(); using var client = await Admin(factory); using var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(Root + "/preview")).StatusCode);
        using var form = new MultipartFormDataContent(); var file = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAlklEQVR4nO3awQmEUAAD0XGwDyuwMCuyMCuwkm8NIosM67sHArlmGmNQJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3ESJ3Hz3cCxL/zSup3/tYDESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESdz0/YVeJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3G+XeCpCyqgCXui4j6ZAAAAAElFTkSuQmCC"));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(file, "file", "sample.png");
        var uploaded = await client.PostAsync(Root + "/assets", form); Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var id = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Root + "/assets/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/shops/nb-jewel-demo/assets/" + id + "?preview=true")).StatusCode);
        var draft = await Draft(client); draft["settings"]!["logoAssetId"] = id;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + "/draft", draft)).StatusCode); draft = await Draft(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Root + "/publish", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/shops/nb-jewel-demo/assets/" + id)).StatusCode);
        var preview = await client.GetAsync(Root + "/preview"); Assert.True(preview.Headers.CacheControl?.NoStore); Assert.Contains(preview.Headers.GetValues("X-Robots-Tag"), value => value.Contains("noindex", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Root + "/offline", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
        var offline = await guest.GetAsync("/api/shops/nb-jewel-demo"); var missing = await guest.GetAsync("/api/shops/unknown-shop");
        Assert.Equal(HttpStatusCode.NotFound, offline.StatusCode); Assert.Equal(await missing.Content.ReadAsStringAsync(), await offline.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/shops/nb-jewel-demo/assets/" + id)).StatusCode);
        var state = await Draft(client); Assert.Equal(3, state["audit"]!.AsArray().Count); // save, publish and offline; initial seed is not audited.
    }
    [Fact]
    public async Task RevokedFeaturedProductsDisappearFromAuthenticatedPreviewWithoutRepublishingThePortal()
    {
        await using var factory = new KnowledgeHttpFactory { UseFilePublications = true }; using var client = await Admin(factory);
        var asset = await PreparedStorefrontAsset.Create(factory, 7);
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<NbJewel.Api.Knowledge.KnowledgeService>().SetProductAsync(7, true, default, "test", asset.RevisionId);
        var draft = await Draft(client); draft["settings"]!["featuredItemIds"] = new JsonArray(7);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + "/draft", draft)).StatusCode); draft = await Draft(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Root + "/publish", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
        var preview = await client.GetFromJsonAsync<JsonElement>(Root + "/preview"); Assert.Single(preview.GetProperty("products").GetProperty("items").EnumerateArray());
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<NbJewel.Api.Knowledge.KnowledgeService>().SetProductAsync(7, false, default, "test");
        preview = await client.GetFromJsonAsync<JsonElement>(Root + "/preview"); Assert.Empty(preview.GetProperty("products").GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Root + "/products/7/preview?v=" + asset.RevisionId)).StatusCode);
    }
    [Fact]
    public async Task PortalPermissionsCsrfAndMembershipAreCheckedExplicitly()
    {
        await using var factory = new KnowledgeHttpFactory(); using var client = await Admin(factory, false);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Root + "/publish", new { revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/businesses/sample-wholesale/portal/draft")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); var draft = await Draft(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Root + "/draft", draft)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>(); var member = await db.Set<BusinessMembership>().SingleAsync(); member.BusinessId = "sample-wholesale"; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Root + "/draft")).StatusCode);
    }
}
