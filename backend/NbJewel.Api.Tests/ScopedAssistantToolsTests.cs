// Role: Verifies fresh authorization and public catalog boundaries for assistant tools.
using System.Text.Json.Nodes;
using System.Reflection;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
using NbJewel.Api.Identity;
using NbJewel.Api.Platform;
using Xunit;

namespace NbJewel.Api.Tests;

public class ScopedAssistantToolsTests
{
    private sealed class Permissions : IPermissionAdapter
    {
        public AccountAccess? Access { get; set; }
        public Task<AccountAccess?> ResolveAsync(string userId, CancellationToken ct) => Task.FromResult(Access);
    }
    private sealed class Knowledge : IKnowledgeRetriever
    {
        public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(AssistantContext context, string query, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
    }
    private sealed class Publications(params int[] ids) : IPublicationReader
    {
        public Task<IReadOnlySet<int>> PublishedItemIdsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<int>>(ids.ToHashSet());
    }
    public class StockScopeProxy : DispatchProxy
    {
        public IItemRepository Inner { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IItemRepository.GetStockAsync)) return Task.FromResult(Rows((int)args![0]!));
            // The bulk read must see the same scripted rows as the one-by-one read.
            if (method.Name == nameof(IItemRepository.GetStockForItemsAsync))
                return Task.FromResult<IReadOnlyList<StockRecord>>(((IReadOnlyCollection<int>)args![0]!).Distinct().SelectMany(Rows).ToList());
            return method.Invoke(Inner, args);
        }
        private static IReadOnlyList<StockRecord> Rows(int id) => id switch {
            1 => [new() { ItemId = 1, StoreId = 1, OnHand = 4 }, new() { ItemId = 1, StoreId = 2, OnHand = 90 }],
            3 => [new() { ItemId = 3, StoreId = 1, OnHand = 3, Reserved = 1 }, new() { ItemId = 3, StoreId = 2, OnHand = 80 }],
            _ => [new() { ItemId = id, StoreId = 2, OnHand = 70 }]
        };
    }
    private static IItemRepository ScopedRepository()
    {
        var repo = DispatchProxy.Create<IItemRepository, StockScopeProxy>();
        ((StockScopeProxy)repo).Inner = SampleRepositoryFactory.Create();
        return repo;
    }
    private static (AssistantTools Tools, Permissions Permissions, AssistantContext Context) ScopedTools()
    {
        string[] grants = ["chat.read", "items.read", "prices.read", "status.read", "stock.read"];
        var permissions = new Permissions { Access = new("u", "", "erp", grants, [1], false) };
        return (new AssistantTools(permissions, ScopedRepository(), new Knowledge(), new Publications()), permissions, new("erp", "u", "erp", grants, [1], false));
    }
    [Fact]
    public async Task Unknown_assistants_cannot_use_knowledge()
    {
        var tools = new AssistantTools(new Permissions(), SampleRepositoryFactory.Create(), new Knowledge(), new Publications());
        var context = new AssistantContext("unknown", "u", "customer", [], [], false);
        Assert.Empty(tools.Declarations(context));
        await Assert.ThrowsAsync<AssistantDeniedException>(() => tools.RunAsync(context, "search_knowledge", new JsonObject { ["query"] = "gold" }, default));
    }
    [Fact]
    public async Task Scoped_search_and_stock_count_only_visible_rows()
    {
        var (tools, _, context) = ScopedTools();
        var search = await tools.RunAsync(context, "search_items", new JsonObject { ["page"] = JsonNode.Parse("2"), ["pageSize"] = JsonNode.Parse("1") }, default);
        Assert.Equal(2, (int)search["data"]!["totalCount"]!);
        Assert.Equal(3, (int)search["data"]!["items"]![0]!["id"]!);
        Assert.Equal(3, (int)search["data"]!["items"]![0]!["onHand"]!);
        var stock = await tools.RunAsync(context, "get_stock_by_store", new JsonObject { ["itemId"] = JsonNode.Parse("1") }, default);
        Assert.Equal(4, (int)stock["data"]!["totalOnHand"]!);
        Assert.Single(stock["data"]!["stores"]!.AsArray());
        var inStock = await tools.RunAsync(context, "search_items", new JsonObject { ["inStockOnly"] = true }, default);
        Assert.Equal(2, (int)inStock["data"]!["totalCount"]!);
        var low = await tools.RunAsync(context, "list_low_stock", new JsonObject { ["threshold"] = JsonNode.Parse("4") }, default);
        Assert.Equal(1, (int)low["data"]!["count"]!);
        Assert.Equal(3, (int)low["data"]!["items"]![0]!["itemId"]!);
        Assert.Equal(2, (int)low["data"]!["items"]![0]!["available"]!);
        foreach (var name in new[] { "get_item_details", "get_item_price_breakdown" })
            Assert.Equal("not_found", (string?)(await tools.RunAsync(context, name, new JsonObject { ["itemId"] = JsonNode.Parse("2") }, default))["status"]);
    }
    [Fact]
    public async Task Revoking_tool_permission_and_store_scope_takes_effect_immediately()
    {
        var (tools, permissions, context) = ScopedTools();
        Assert.Equal("ok", (string?)(await tools.RunAsync(context, "get_item_details", new JsonObject { ["itemId"] = JsonNode.Parse("1") }, default))["status"]);
        permissions.Access = permissions.Access! with { Permissions = ["chat.read", "items.read", "status.read"] };
        await Assert.ThrowsAsync<AssistantDeniedException>(() => tools.RunAsync(context, "get_item_details", new JsonObject { ["itemId"] = JsonNode.Parse("1") }, default));
        permissions.Access = permissions.Access with { Permissions = context.Permissions, StoreIds = [] };
        Assert.Equal("not_found", (string?)(await tools.RunAsync(context, "get_item_details", new JsonObject { ["itemId"] = JsonNode.Parse("1") }, default))["status"]);
        Assert.Equal("not_found", (string?)(await tools.RunAsync(context, "search_items", new(), default))["status"]);
    }
    [Fact]
    public async Task Revocation_is_checked_on_every_call()
    {
        var perms = new Permissions { Access = new("u", "", "erp", ["chat.read", "reference.read"], [], false) };
        var tools = new AssistantTools(perms, SampleRepositoryFactory.Create(), new Knowledge(), new Publications());
        var context = new AssistantContext("erp", "u", "erp", ["chat.read", "reference.read"], [], false);
        Assert.Equal("ok", (string?) (await tools.RunAsync(context, "list_categories", new(), default))["status"]);
        perms.Access = null;
        await Assert.ThrowsAsync<AssistantDeniedException>(() => tools.RunAsync(context, "list_categories", new(), default));
    }
    [Fact]
    public async Task Public_tools_deny_guessed_ids_and_unknown_arguments()
    {
        var tools = new AssistantTools(new Permissions(), SampleRepositoryFactory.Create(), new Knowledge(), new Publications());
        var context = new AssistantContext("website", "visitor", "customer", [], [], false);
        Assert.Equal("not_found", (string?)(await tools.RunAsync(context, "get_public_product", new JsonObject { ["itemId"] = JsonNode.Parse("1") }, default))["status"]);
        Assert.Equal("error", (string?)(await tools.RunAsync(context, "search_public_products", new JsonObject { ["unexpected"] = true }, default))["status"]);
    }
    [Fact]
    public async Task Scope_filters_stock_and_pages_before_counting()
    {
        var repo = SampleRepositoryFactory.Create();
        var stores = await repo.GetStoresAsync(default);
        var scope = new AssistantContext("erp", "u", "erp", [], [stores[0].Id], false);
        var reader = new ScopedChatDataReader(repo, scope);
        var full = await reader.SearchItemsAsync(null, null, null, null, null, 1, 100, default);
        var page = await reader.SearchItemsAsync(null, null, null, null, null, 1, 1, default);
        Assert.Equal(full.TotalCount, page.TotalCount);
        foreach (var item in full.Items)
        {
            var stock = await reader.GetStockAsync(item.Id, default);
            Assert.NotEmpty(stock);
            Assert.All(stock, row => Assert.Equal(stores[0].Id, row.StoreId));
        }
        var empty = new ScopedChatDataReader(repo, scope with { StoreIds = [] });
        Assert.Equal(0, (await empty.SearchItemsAsync(null, null, null, null, null, 1, 10, default)).TotalCount);
    }
    [Fact]
    public async Task Public_projection_omits_internal_fields()
    {
        var repo = SampleRepositoryFactory.Create();
        var item = (await repo.SearchItemsAsync(null, null, null, null, null, 1, 1, default)).Items[0];
        var tools = new AssistantTools(new Permissions(), repo, new Knowledge(), new Publications(item.Id));
        var context = new AssistantContext("website", "visitor", "customer", [], [], false);
        var result = await tools.RunAsync(context, "get_public_product", new JsonObject { ["itemId"] = item.Id }, default);
        var data = result["data"]!.AsObject();
        Assert.Equal("ok", (string?)result["status"]);
        Assert.Equal(new[] { "categoryId", "code", "currency", "id", "imageUrl", "metalType", "name", "price", "purity" }, data.Select(x => x.Key).OrderBy(x => x).ToArray());
    }
}
