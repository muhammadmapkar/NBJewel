// Role: Verifies anonymous storefront reads, publication/category boundaries, and live revocation
// through the real HTTP pipeline with sample items and an in-memory knowledge database only.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests;

public sealed class PublicCatalogHttpTests
{
    private static async Task Publish(KnowledgeHttpFactory factory, params int[] ids)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<KnowledgeDatabase>();
        foreach (var id in ids)
        {
            var asset = await PreparedStorefrontAsset.Create(factory, id);
            database.Products.Add(new ProductPublication { ItemId = id, Published = true, AssetRevisionId = asset.RevisionId });
        }
        await database.SaveChangesAsync(); // Includes intentionally excluded/draft legacy records for boundary checks.
    }

    [Fact]
    public async Task AnonymousListReturnsOnlyPublishedCompleteAllowedProducts()
    {
        await using var factory = new KnowledgeHttpFactory();
        await Publish(factory, 1, 7, 9, 21, 22, 28, 31);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/storefront/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { 7, 9, 21 }, body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));
        Assert.Equal(new[] { "necklace", "necklace", "earrings" }, body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("category").GetString()));
        Assert.True(response.Headers.CacheControl?.NoStore);
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/storefront/products?metal=silver&search=NKL&pageSize=1");
        Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
        Assert.Equal(9, Assert.Single(filtered.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
        var second = await client.GetFromJsonAsync<JsonElement>("/api/storefront/products?page=2&pageSize=2");
        Assert.Equal(21, Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData(1)] // Published ring remains excluded.
    [InlineData(8)] // Unpublished necklace remains hidden.
    [InlineData(22)] // Published draft remains hidden.
    [InlineData(28)] // Published coin remains excluded.
    [InlineData(9999)]
    public async Task AnonymousDetailDoesNotRevealHiddenItems(int id)
    {
        await using var factory = new KnowledgeHttpFactory();
        await Publish(factory, 1, 7, 22, 28);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/storefront/products/{id}")).StatusCode);
    }

    [Fact]
    public async Task DetailUsesAnExplicitPublicFieldContractAndRevocationTakesEffectOnNextRequest()
    {
        await using var factory = new KnowledgeHttpFactory();
        await Publish(factory, 7);
        using var client = factory.CreateClient();
        var product = await client.GetFromJsonAsync<JsonElement>("/api/storefront/products/7");
        Assert.Equal(new[] { "category", "code", "grossWeight", "id", "imageUrl", "metalType", "name", "netWeight", "previewAspect", "previewUrl", "price", "purity", "storefront" }, product.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(7, product.GetProperty("id").GetInt32());
        Assert.Equal("Gold", product.GetProperty("metalType").GetString());
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<KnowledgeService>().SetProductAsync(7, false, default);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/storefront/products/7")).StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/storefront/products");
        Assert.Equal(0, after.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task PublishedCompleteSamplePlatinumRetainsItsLabelAndMatchingRate()
    {
        await using var factory = new KnowledgeHttpFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IItemRepository>().UpdateStatusAsync(31, "Complete", default);
        await Publish(factory, 31);
        using var client = factory.CreateClient();
        var product = await client.GetFromJsonAsync<JsonElement>("/api/storefront/products/31");
        Assert.Equal("Platinum", product.GetProperty("metalType").GetString());
        Assert.Equal("950", product.GetProperty("purity").GetString());
        Assert.True(product.GetProperty("storefront").GetProperty("isSample").GetBoolean());
        Assert.Equal(110m, product.GetProperty("price").GetProperty("ratePerGram").GetDecimal());
    }

    [Fact]
    public async Task AnonymousRatesKeepActualEffectiveDatesAndReturnOnlyMatchingMetal()
    {
        await using var factory = new KnowledgeHttpFactory();
        using var client = factory.CreateClient();
        var rates = await client.GetFromJsonAsync<JsonElement>("/api/storefront/metal-rates");
        Assert.Equal(5, rates.GetArrayLength());
        Assert.All(rates.EnumerateArray(), rate => Assert.Equal("2026-09-30", rate.GetProperty("effectiveDate").GetString()));
        var platinum = await client.GetFromJsonAsync<JsonElement>("/api/storefront/metal-rates?metal=platinum");
        var row = Assert.Single(platinum.EnumerateArray());
        Assert.Equal("950", row.GetProperty("purity").GetString());
        Assert.Equal("AED", row.GetProperty("currency").GetString());
        Assert.Equal(110m, row.GetProperty("ratePerGram").GetDecimal());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=0")]
    public async Task InvalidPagingIsRejected(string query)
    {
        await using var factory = new KnowledgeHttpFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/storefront/products" + query)).StatusCode);
    }

    [Fact]
    public async Task PublicationStorageFailureReturnsUnavailableAndNeverFallsBackToAllItems()
    {
        await using var factory = new KnowledgeHttpFactory { DisableKnowledgeProvider = true };
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/storefront/products")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/storefront/products/7")).StatusCode);
    }

    [Fact]
    public async Task PublicRoutesDoNotProvideWritesOrOpenProtectedErpRoutes()
    {
        await using var factory = new KnowledgeHttpFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync("/api/storefront/products", new { name = "No write" })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PutAsJsonAsync("/api/storefront/products/7", new { name = "No write" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/items")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/metal-rates/latest")).StatusCode);
    }
}
