// Role: Keeps the HTTP ERP importer's read, create, image, stock and status route contract working for a demo member.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NbJewel.Api.Models;
namespace NbJewel.Api.Tests;

public sealed class ImporterCompatibilityTests
{
    [Fact]
    public async Task DemoMemberCanUseEveryImporterRouteWithoutPublishing()
    {
        await using var factory = new KnowledgeHttpFactory { UseFilePublications = true };
        await factory.Seed("erp", true, ["items.read", "items.write", "prices.read", "status.read", "status.write", "reference.read", "stock.read", "stock.write"]);
        using var client = factory.CreateClient();
        await BusinessMembershipHttpTests.Token(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "PasswordExample123!" })).StatusCode);
        await BusinessMembershipHttpTests.Token(client);
        var storage = await client.GetFromJsonAsync<JsonElement>("/api/items/storage");
        Assert.Equal("Sample", storage.GetProperty("provider").GetString());
        foreach (var path in new[] { "/api/items", "/api/categories", "/api/designs", "/api/stores" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        var input = new ItemCreateRequest("IMPORT-" + Guid.NewGuid().ToString("N")[..12], "Assumed importer fixture", 3, 1, "Gold", "22K", 10, 10, 0, "PERCENT", 8, null, null);
        var created = await client.PostAsJsonAsync("/api/items", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<Item>())!;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/items/{item.Id}/stock", new StockAddRequest(null, 1, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/items/{item.Id}/status", new { status = "Complete" })).StatusCode);
        using var form = new MultipartFormDataContent();
        var photo = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAlklEQVR4nO3awQmEUAAD0XGwDyuwMCuyMCuwkm8NIosM67sHArlmGmNQJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3ESJ3Hz3cCxL/zSup3/tYDESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESdz0/YVeJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3G+XeCpCyqgCXui4j6ZAAAAAElFTkSuQmCC"));
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(photo, "file", "sample.png");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/items/{item.Id}/image", form)).StatusCode);
        var publicList = await client.GetFromJsonAsync<JsonElement>("/api/storefront/products");
        Assert.Equal(0, publicList.GetProperty("totalCount").GetInt32());
    }
}
