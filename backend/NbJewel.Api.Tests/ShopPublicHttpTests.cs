// Role: Proves assumed scoped public catalog routes choose the requested business and its live policy despite overlapping IDs.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
using NbJewel.Api.Knowledge;
namespace NbJewel.Api.Tests;
public sealed class ShopPublicHttpTests
{
    [Fact]
    public async Task ScopedCatalogAndPreviewUrlsRemainIndependentFromLegacyDemoAliases()
    {
        await using var factory = new KnowledgeHttpFactory { UseFilePublications = true };
        using var admin = await PortalLifecycleHttpTests.Admin(factory);
        var asset = await PreparedStorefrontAsset.Create(factory, 7);
        var registry = factory.Services.GetRequiredService<BusinessRegistry>(); var storage = factory.Services.GetRequiredService<BusinessStorage>();
        var demo = storage.For(registry.Find("nb-jewel-demo")!); await demo.Publications.SaveAsync(new() { ItemId = 7, Published = true, AssetRevisionId = asset.RevisionId }, "test", default);
        var wholesale = storage.For(registry.Find("sample-wholesale")!); var revision = Guid.NewGuid().ToString("N"); var folder = wholesale.Cutout.RevisionFolder(revision); Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, "preview.png"), await File.ReadAllBytesAsync(Path.Combine(demo.Cutout.RevisionFolder(asset.RevisionId), "preview.png")));
        await wholesale.Assets.SaveAsync(asset with { RevisionId = revision }, default); await wholesale.Publications.SaveAsync(new() { ItemId = 7, Published = true, AssetRevisionId = revision }, "test", default);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/shops/sample-wholesale/products")).StatusCode);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>(); (await db.Set<BusinessMembership>().SingleAsync()).BusinessId = "sample-wholesale"; await db.SaveChangesAsync(); }
        var root = "/api/businesses/sample-wholesale/portal"; var draft = await PortalLifecycleHttpTests.Draft(admin, root); draft["settings"]!["enquiries"] = false;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(root + "/draft", draft)).StatusCode); draft = await PortalLifecycleHttpTests.Draft(admin, root);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(root + "/publish", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
        var product = await admin.GetFromJsonAsync<JsonElement>("/api/shops/sample-wholesale/products/7?businessId=nb-jewel-demo");
        Assert.Equal("WS-ONLY", product.GetProperty("code").GetString()); Assert.Equal(JsonValueKind.Null, product.GetProperty("price").ValueKind);
        var url = product.GetProperty("previewUrl").GetString()!; Assert.StartsWith("/api/shops/sample-wholesale/products/7/preview", url); Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal("NKL-0007", (await admin.GetFromJsonAsync<JsonElement>("/api/storefront/products/7?slug=sample-wholesale")).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/shops/sample-wholesale/products/1")).StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<JsonElement>("/api/shops/sample-wholesale/stores")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(root + "/offline", new { revision = draft["revision"]!.GetValue<long>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/storefront/products/7")).StatusCode);
    }
}
