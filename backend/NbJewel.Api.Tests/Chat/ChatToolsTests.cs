// Role: Tests for the eight chat tools on the real sample data: one or more tests per tool,
// the argument checks, and a test that proves no tool can write. No model or network is used.

using System.Reflection;
using System.Text.Json.Nodes;
using NbJewel.Api.Chat;
using NbJewel.Api.Repositories;
using static NbJewel.Api.Tests.Chat.ChatTestKit;

namespace NbJewel.Api.Tests.Chat;

public sealed class ChatToolsTests
{
    private static string Status(JsonObject result) => result["status"]!.GetValue<string>();
    private static JsonNode Data(JsonObject result) => result["data"]!;
    private static int SourceCount(JsonObject result) => result["sources"]!.AsArray().Count;

    [Fact]
    public async Task SearchItems_FindsTheZorkRing()
    {
        var result = await RunTool("search_items", new { query = "zork" });
        Assert.Equal("ok", Status(result));
        // "zork" also matches the design code DSN-ZORK, which three items share.
        Assert.Equal("RNG-0142", Data(result)["items"]![0]!["code"]!.GetValue<string>());
        Assert.Equal(3, Data(result)["totalCount"]!.GetValue<int>());
        Assert.True(Data(result)["items"]![0]!["onHand"]!.GetValue<int>() > 0);
        Assert.Equal(3, SourceCount(result));
    }

    [Fact]
    public async Task SearchItems_FiltersByCategoryNameAndInStock()
    {
        var rings = await RunTool("search_items", new { category = "rings", pageSize = 50 });
        Assert.Equal("ok", Status(rings));
        Assert.All(Data(rings)["items"]!.AsArray(), i => Assert.Equal(2, i!["categoryId"]!.GetValue<int>()));

        var inStock = await RunTool("search_items", new { inStockOnly = true, pageSize = 50 });
        Assert.Equal("ok", Status(inStock));
        Assert.All(Data(inStock)["items"]!.AsArray(), i => Assert.True(i!["onHand"]!.GetValue<int>() > 0));
    }

    [Fact]
    public async Task SearchItems_UnknownCategoryAndNoMatchAreNotFound()
    {
        Assert.Equal("not_found", Status(await RunTool("search_items", new { category = "Spaceships" })));
        Assert.Equal("not_found", Status(await RunTool("search_items", new { query = "no-such-item-anywhere" })));
        Assert.Equal("not_found", Status(await RunTool("search_items", new { query = "zork", page = 5 })));
    }

    [Fact]
    public async Task GetItemDetails_ByIdAndByCode_ReturnOnlyContractFields()
    {
        var byId = await RunTool("get_item_details", new { itemId = 1 });
        var byCode = await RunTool("get_item_details", new { code = "rng-0142" });
        Assert.Equal("ok", Status(byId));
        Assert.True(JsonNode.DeepEquals(Data(byId), Data(byCode)));
        Assert.Equal(15, Data(byId).AsObject().Count);
        Assert.Null(Data(byId).AsObject()["designId"]);
        Assert.Equal(42.758m, Data(byId)["netWt"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task GetItemDetails_MissingItemsAreNotFound()
    {
        Assert.Equal("not_found", Status(await RunTool("get_item_details", new { itemId = 9999 })));
        Assert.Equal("not_found", Status(await RunTool("get_item_details", new { code = "ZZZ-9999" })));
    }

    [Fact]
    public async Task GetItemDetails_NeedsExactlyOneOfIdOrCode()
    {
        Assert.Equal("error", Status(await RunTool("get_item_details", new { })));
        Assert.Equal("error", Status(await RunTool("get_item_details", new { itemId = 1, code = "RNG-0142" })));
    }

    [Fact]
    public async Task GetItemPriceBreakdown_ReturnsThePriceWithTheItemName()
    {
        var result = await RunTool("get_item_price_breakdown", new { itemId = 1 });
        Assert.Equal("ok", Status(result));
        Assert.Equal("RNG-0142", Data(result)["itemCode"]!.GetValue<string>());
        Assert.Equal(66m, Data(result)["ratePerGram"]!.GetValue<decimal>());
        Assert.NotNull(Data(result)["totalPrice"]);
        Assert.Equal("not_found", Status(await RunTool("get_item_price_breakdown", new { itemId = 9999 })));
    }

    [Fact]
    public async Task GetStockByStore_ReturnsEachStoreAndTheTotal()
    {
        var result = await RunTool("get_stock_by_store", new { itemId = 1 });
        Assert.Equal("ok", Status(result));
        var stores = Data(result)["stores"]!.AsArray();
        Assert.NotEmpty(stores);
        Assert.NotNull(stores[0]!["storeName"]);
        Assert.Equal(stores.Sum(s => s!["onHand"]!.GetValue<int>()), Data(result)["totalOnHand"]!.GetValue<int>());
        Assert.All(stores, s => Assert.Equal(s!["onHand"]!.GetValue<int>() - s["reserved"]!.GetValue<int>(), s["available"]!.GetValue<int>()));
        Assert.Equal("not_found", Status(await RunTool("get_stock_by_store", new { itemId = 9999 })));
    }

    [Fact]
    public async Task ListLowStock_UsesAvailableUnitsAndTheStoreFilter()
    {
        var all = await RunTool("list_low_stock", new { threshold = 1000 });
        Assert.Equal("ok", Status(all));
        Assert.Equal(30, Data(all)["count"]!.GetValue<int>());
        var availables = Data(all)["items"]!.AsArray().Select(i => i!["available"]!.GetValue<int>()).ToList();
        Assert.Equal(availables.Order().ToList(), availables);

        Assert.Equal("ok", Status(await RunTool("list_low_stock", new { threshold = 1000, store = "dubai" })));
        Assert.Equal("not_found", Status(await RunTool("list_low_stock", new { store = "Atlantis" })));

        // The default threshold is 2 (the dashboard rule): the message names it when nothing is low.
        var defaultResult = await RunTool("list_low_stock", new { });
        if (Status(defaultResult) == "ok") Assert.Equal(2, Data(defaultResult)["threshold"]!.GetValue<int>());
        else Assert.Contains("fewer than 2 units", Data(defaultResult)["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task ListCategories_ReturnsAllCategories()
    {
        var result = await RunTool("list_categories");
        Assert.Equal("ok", Status(result));
        Assert.Equal(8, Data(result).AsArray().Count);
        Assert.Equal(8, SourceCount(result));
    }

    [Fact]
    public async Task GetLatestMetalRate_FiltersByMetalAndPurity()
    {
        var gold = await RunTool("get_latest_metal_rate", new { metal = "gold" });
        Assert.Equal(3, Data(gold).AsArray().Count);
        var gold22 = await RunTool("get_latest_metal_rate", new { metal = "Gold", purity = "22k" });
        Assert.Equal(66m, Data(gold22)[0]!["ratePerGram"]!.GetValue<decimal>());
        Assert.Equal("not_found", Status(await RunTool("get_latest_metal_rate", new { metal = "Platinum" })));
    }

    [Fact]
    public async Task ListItemsByStatus_ReturnsItemsWithStock()
    {
        var draft = await RunTool("list_items_by_status", new { status = "draft" });
        Assert.Equal("ok", Status(draft));
        Assert.Equal("Draft", Data(draft)["status"]!.GetValue<string>());
        Assert.Equal(2, Data(draft)["totalCount"]!.GetValue<int>());
        Assert.False(Data(draft)["truncated"]!.GetValue<bool>());
        Assert.NotNull(Data(draft)["items"]![0]!["onHand"]);
        Assert.Equal("not_found", Status(await RunTool("list_items_by_status", new { status = "With Smith" })));
    }

    // ---------- argument checks ----------

    [Theory]
    [InlineData("search_items", """{"colour":"red"}""")]
    [InlineData("search_items", """{"pageSize":500}""")]
    [InlineData("search_items", """{"status":"Sold"}""")]
    [InlineData("get_item_price_breakdown", """{}""")]
    [InlineData("get_item_price_breakdown", """{"itemId":0}""")]
    [InlineData("get_item_price_breakdown", """{"itemId":"one"}""")]
    [InlineData("list_low_stock", """{"threshold":0}""")]
    [InlineData("list_categories", """{"extra":1}""")]
    [InlineData("get_latest_metal_rate", """{}""")]
    [InlineData("list_items_by_status", """{}""")]
    public async Task BadArguments_ReturnAnErrorResult(string tool, string argsJson)
    {
        var result = await Tools().RunAsync(tool, JsonNode.Parse(argsJson)!.AsObject(), CancellationToken.None);
        Assert.Equal("error", Status(result));
        Assert.StartsWith("Invalid arguments.", Data(result)["message"]!.GetValue<string>());
        Assert.Equal(0, SourceCount(result));
    }

    [Fact]
    public async Task UnknownTool_ReturnsAnErrorResult()
    {
        var result = await RunTool("drop_table", new { name = "items" });
        Assert.Equal("error", Status(result));
    }

    [Fact]
    public void Declarations_ListAllEightToolsWithSchemas()
    {
        var declarations = Tools().Declarations;
        Assert.Equal(
            ["search_items", "get_item_details", "get_item_price_breakdown", "get_stock_by_store", "list_low_stock", "list_categories", "get_latest_metal_rate", "list_items_by_status"],
            declarations.Select(d => d.Name).ToArray());
        Assert.All(declarations, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Description));
            Assert.Equal("object", d.Parameters["type"]!.GetValue<string>());
        });
    }

    // ---------- read-only proof ----------

    [Fact]
    public void ChatDataReader_HasOnlyReadMethods()
    {
        foreach (var method in typeof(IChatDataReader).GetMethods())
            Assert.True(method.Name.StartsWith("Get") || method.Name.StartsWith("Search"), $"{method.Name} is not a read method.");
    }

    [Fact]
    public void ChatTools_CannotReachTheWritableRepository()
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.DoesNotContain(typeof(ChatTools).GetFields(all), f => f.FieldType == typeof(IItemRepository));
        Assert.DoesNotContain(typeof(ChatTools).GetConstructors().SelectMany(c => c.GetParameters()), p => p.ParameterType == typeof(IItemRepository));
        Assert.False(typeof(IItemRepository).IsAssignableFrom(typeof(IChatDataReader)));
        Assert.False(typeof(IItemRepository).IsAssignableFrom(typeof(ChatDataReader)));
    }

    [Fact]
    public async Task EveryTool_OnlyCallsReadMethodsOnTheRepository()
    {
        var spy = DispatchProxy.Create<IItemRepository, RecordingProxy>();
        var proxy = (RecordingProxy)(object)spy;
        proxy.Target = SampleRepositoryFactory.Create();

        var calls = new (string Tool, object Args)[]
        {
            ("search_items", new { query = "ring", inStockOnly = true }), ("get_item_details", new { itemId = 1 }), ("get_item_details", new { code = "RNG-0142" }),
            ("get_item_price_breakdown", new { itemId = 1 }), ("get_stock_by_store", new { itemId = 1 }), ("list_low_stock", new { store = "Dubai" }),
            ("list_categories", new { }), ("get_latest_metal_rate", new { metal = "Gold" }), ("list_items_by_status", new { status = "Draft" }),
        };
        foreach (var (tool, args) in calls) Assert.Equal("ok", Status(await RunTool(tool, args, spy)));

        Assert.NotEmpty(proxy.Calls);
        Assert.All(proxy.Calls, name => Assert.True(name.StartsWith("Get") || name.StartsWith("Search"), $"{name} is a write method."));
    }

    public class RecordingProxy : DispatchProxy
    {
        public IItemRepository Target { get; set; } = null!;
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod!.Name);
            try { return targetMethod.Invoke(Target, args); }
            catch (TargetInvocationException error) { throw error.InnerException!; }
        }
    }
}
