// Role: One parity test per chat tool. Each test calls the matching API controller endpoint on
// the same sample data and checks that the tool's data equals what the endpoint returned
// (serialized with the same JSON settings the API uses). If an endpoint changes, the tool
// must change with it, or one of these tests fails.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
using static NbJewel.Api.Tests.Chat.ChatTestKit;

namespace NbJewel.Api.Tests.Chat;

public sealed class ChatToolParityTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly string[] ItemFields = ["id", "code", "name", "categoryId", "designCode", "metalType", "purity", "grossWt", "netWt", "stoneWt", "makingType", "makingValue", "huid", "status", "imageUrl"];
    private static readonly string[] SummaryFields = ["id", "code", "name", "categoryId", "metalType", "purity", "netWt", "status"];

    // The body of a 200 response, as JSON.
    private static JsonNode Body<T>(ActionResult<T> result) =>
        JsonSerializer.SerializeToNode(Assert.IsType<OkObjectResult>(result.Result).Value, Web)!;

    private static JsonObject Pick(JsonNode source, string[] keys)
    {
        var picked = new JsonObject();
        foreach (var key in keys) picked[key] = source[key]?.DeepClone();
        return picked;
    }

    private static async Task<int> OnHand(ItemsController items, int itemId) =>
        Body(await items.GetStock(itemId, CancellationToken.None)).AsArray().Sum(r => r!["onHand"]!.GetValue<int>());

    private static async Task<JsonObject> WithOnHand(ItemsController items, JsonNode item)
    {
        var summary = Pick(item, SummaryFields);
        summary["onHand"] = await OnHand(items, item["id"]!.GetValue<int>());
        return summary;
    }

    private static void AssertSame(JsonNode? expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Expected {expected?.ToJsonString()} but got {actual?.ToJsonString()}");

    [Fact]
    public async Task SearchItems_MatchesGetApiItems()
    {
        var repository = SampleRepositoryFactory.Create();
        var items = new ItemsController(repository);
        var endpoint = Body(await items.GetItems("ring", null, "Gold", null, null, 1, 10));

        var tool = (await RunTool("search_items", new { query = "ring", metal = "Gold", pageSize = 10 }, repository))["data"]!;

        var expected = new JsonObject
        {
            ["items"] = new JsonArray((await Task.WhenAll(endpoint["items"]!.AsArray().Select(i => WithOnHand(items, i!)))).Cast<JsonNode>().ToArray()),
            ["page"] = endpoint["page"]!.DeepClone(),
            ["pageSize"] = endpoint["pageSize"]!.DeepClone(),
            ["totalCount"] = endpoint["totalCount"]!.DeepClone(),
        };
        AssertSame(expected, tool);
    }

    [Fact]
    public async Task GetItemDetails_MatchesGetApiItemsById()
    {
        var repository = SampleRepositoryFactory.Create();
        var endpoint = Body(await new ItemsController(repository).GetItem(3, CancellationToken.None));
        var tool = (await RunTool("get_item_details", new { itemId = 3 }, repository))["data"]!;
        AssertSame(Pick(endpoint, ItemFields), tool);
    }

    [Fact]
    public async Task GetItemPriceBreakdown_MatchesGetApiItemsPrice()
    {
        var repository = SampleRepositoryFactory.Create();
        var items = new ItemsController(repository);
        var price = Body(await items.GetPrice(1, CancellationToken.None)).AsObject();
        var item = Body(await items.GetItem(1, CancellationToken.None));

        var expected = new JsonObject { ["itemCode"] = item["code"]!.DeepClone(), ["itemName"] = item["name"]!.DeepClone() };
        foreach (var (key, value) in price) expected[key] = value?.DeepClone();

        AssertSame(expected, (await RunTool("get_item_price_breakdown", new { itemId = 1 }, repository))["data"]);
    }

    [Fact]
    public async Task GetStockByStore_MatchesGetApiItemsStockAndStores()
    {
        var repository = SampleRepositoryFactory.Create();
        var items = new ItemsController(repository);
        var stock = Body(await items.GetStock(1, CancellationToken.None)).AsArray();
        var stores = Body(await new ReferenceDataController(repository).GetStores(CancellationToken.None)).AsArray();
        var item = Body(await items.GetItem(1, CancellationToken.None));

        var rows = stock.Select(r =>
        {
            var store = stores.First(s => s!["id"]!.GetValue<int>() == r!["storeId"]!.GetValue<int>())!;
            return (JsonNode)new JsonObject
            {
                ["storeId"] = r["storeId"]!.DeepClone(), ["storeName"] = store["name"]!.DeepClone(), ["city"] = store["city"]!.DeepClone(),
                ["onHand"] = r["onHand"]!.DeepClone(), ["reserved"] = r["reserved"]!.DeepClone(),
                ["available"] = r["onHand"]!.GetValue<int>() - r["reserved"]!.GetValue<int>(),
            };
        }).ToArray();
        var expected = new JsonObject
        {
            ["itemId"] = 1, ["itemCode"] = item["code"]!.DeepClone(), ["itemName"] = item["name"]!.DeepClone(),
            ["stores"] = new JsonArray(rows), ["totalOnHand"] = stock.Sum(r => r!["onHand"]!.GetValue<int>()),
        };
        AssertSame(expected, (await RunTool("get_stock_by_store", new { itemId = 1 }, repository))["data"]);
    }

    [Fact]
    public async Task ListLowStock_MatchesItemsAndTheirStockFromTheApi()
    {
        var repository = SampleRepositoryFactory.Create();
        var items = new ItemsController(repository);
        var all = Body(await items.GetItems(null, null, null, null, null, 1, 100)).AsObject()["items"]!.AsArray();

        var low = new List<(int Id, string Code, string Name, int Available)>();
        foreach (var item in all)
        {
            var id = item!["id"]!.GetValue<int>();
            var available = Body(await items.GetStock(id, CancellationToken.None)).AsArray().Sum(r => r!["onHand"]!.GetValue<int>() - r["reserved"]!.GetValue<int>());
            if (available < 6) low.Add((id, item["code"]!.GetValue<string>(), item["name"]!.GetValue<string>(), available));
        }
        Assert.NotEmpty(low);
        var expected = new JsonObject
        {
            ["threshold"] = 6, ["store"] = null, ["count"] = low.Count,
            ["items"] = new JsonArray(low.OrderBy(x => x.Available).ThenBy(x => x.Id)
                .Select(x => (JsonNode)new JsonObject { ["itemId"] = x.Id, ["code"] = x.Code, ["name"] = x.Name, ["available"] = x.Available }).ToArray()),
        };
        AssertSame(expected, (await RunTool("list_low_stock", new { threshold = 6 }, repository))["data"]);
    }

    [Fact]
    public async Task ListCategories_MatchesGetApiCategories()
    {
        var repository = SampleRepositoryFactory.Create();
        var endpoint = Body(await new ReferenceDataController(repository).GetCategories(CancellationToken.None));
        AssertSame(endpoint, (await RunTool("list_categories", null, repository))["data"]);
    }

    [Fact]
    public async Task GetLatestMetalRate_MatchesGetApiMetalRatesLatest()
    {
        var repository = SampleRepositoryFactory.Create();
        var endpoint = Body(await new ReferenceDataController(repository).GetLatestRates(CancellationToken.None)).AsArray();
        var gold = new JsonArray(endpoint.Where(r => r!["metalType"]!.GetValue<string>() == "Gold").Select(r => r!.DeepClone()).ToArray());
        AssertSame(gold, (await RunTool("get_latest_metal_rate", new { metal = "Gold" }, repository))["data"]);
    }

    [Fact]
    public async Task ListItemsByStatus_MatchesGetApiItemsWithStatus()
    {
        var repository = SampleRepositoryFactory.Create();
        var items = new ItemsController(repository);
        var endpoint = Body(await items.GetItems(null, null, null, null, "Draft", 1, 100));

        var expected = new JsonObject
        {
            ["status"] = "Draft",
            ["totalCount"] = endpoint["totalCount"]!.DeepClone(),
            ["items"] = new JsonArray((await Task.WhenAll(endpoint["items"]!.AsArray().Select(i => WithOnHand(items, i!)))).Cast<JsonNode>().ToArray()),
            ["truncated"] = false,
        };
        AssertSame(expected, (await RunTool("list_items_by_status", new { status = "Draft" }, repository))["data"]);
    }
}
