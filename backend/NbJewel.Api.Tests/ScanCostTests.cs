// Role: Proves catalog scans, stock checks and evidence revalidation use bulk reads instead of one query per item, with unchanged results.
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using NbJewel.Api.Chat;
using NbJewel.Api.Identity;
using NbJewel.Api.Models;
using NbJewel.Api.Platform;
using NbJewel.Api.Repositories;
using NbJewel.Api.Tests.Chat;
using Xunit;

namespace NbJewel.Api.Tests;

public class ScanCostTests
{
    // Delegates everything to the sample repository and counts the lookups that cost one query each.
    private sealed class CountingRepository(IItemRepository inner) : IItemRepository
    {
        public int GetItemCalls, GetStockCalls, BulkStockCalls, SearchCalls;
        public Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken ct) { SearchCalls++; return inner.SearchItemsAsync(search, categoryId, metal, purity, status, page, pageSize, ct); }
        public Task<Item?> GetItemAsync(int id, CancellationToken ct) { GetItemCalls++; return inner.GetItemAsync(id, ct); }
        // Synthetic stock so scopes differ per item: every third item is held only by store 2, and every fourth item has nothing on hand.
        public static IReadOnlyList<StockRecord> Rows(int id) => id % 3 == 0
            ? [new() { ItemId = id, StoreId = 2, OnHand = id % 4 == 0 ? 0 : 5 }]
            : [new() { ItemId = id, StoreId = 1, OnHand = id % 4 == 0 ? 0 : 3 }, new() { ItemId = id, StoreId = 2, OnHand = id % 4 == 0 ? 0 : 2 }];
        public Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken ct) { GetStockCalls++; return Task.FromResult(Rows(itemId)); }
        public Task<IReadOnlyList<StockRecord>> GetStockForItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct) { BulkStockCalls++; return Task.FromResult<IReadOnlyList<StockRecord>>(itemIds.Distinct().SelectMany(Rows).ToList()); }
        public Task<IReadOnlyList<StockRecord>> GetLowStockAsync(int threshold, CancellationToken ct) => inner.GetLowStockAsync(threshold, ct);
        public Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(int itemId, CancellationToken ct) => inner.GetVariantsAsync(itemId, ct);
        public Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken ct) => inner.GetPriceAsync(itemId, ct);
        public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct) => inner.GetCategoriesAsync(ct);
        public Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken ct) => inner.GetStoresAsync(ct);
        public Task<IReadOnlyList<Design>> GetDesignsAsync(CancellationToken ct) => inner.GetDesignsAsync(ct);
        public Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken ct) => inner.GetLatestRatesAsync(ct);
        public Task<Item> CreateAsync(ItemCreateRequest request, CancellationToken ct) => inner.CreateAsync(request, ct);
        public Task<Item?> UpdateAsync(int id, ItemUpdateRequest request, CancellationToken ct) => inner.UpdateAsync(id, request, ct);
        public Task<Item?> UpdateStatusAsync(int id, string status, CancellationToken ct) => inner.UpdateStatusAsync(id, status, ct);
        public Task<StockRecord?> AddStockAsync(int itemId, StockAddRequest request, CancellationToken ct) => inner.AddStockAsync(itemId, request, ct);
    }
    private sealed class Permissions(AccountAccess? access) : IPermissionAdapter
    {
        public Task<AccountAccess?> ResolveAsync(string userId, CancellationToken ct) => Task.FromResult(access);
    }
    private sealed class NoKnowledge : IKnowledgeRetriever, IKnowledgeEvidenceValidator
    {
        public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(AssistantContext context, string query, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
        public Task<bool> CanUseAsync(AssistantContext context, string chunkId, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class Publications(IReadOnlySet<int> ids) : IPublicationReader
    {
        public Task<IReadOnlySet<int>> PublishedItemIdsAsync(CancellationToken ct) => Task.FromResult(ids);
    }
    private static readonly string[] Grants = ["chat.read", "items.read", "prices.read", "status.read", "stock.read"];

    private static async Task<List<Item>> AllItems(IItemRepository repository) =>
        (await repository.SearchItemsAsync(null, null, null, null, null, 1, 1000, default)).Items.ToList();

    [Fact]
    public async Task Public_search_does_not_load_published_items_one_by_one()
    {
        var sample = SampleRepositoryFactory.Create(); var items = await AllItems(sample);
        var published = items.Select(x => x.Id).ToHashSet(); var repo = new CountingRepository(sample);
        var tools = new AssistantTools(new Permissions(null), repo, new NoKnowledge(), new Publications(published));
        var word = items[0].Name.Split(' ')[0];
        var truth = items.Where(x => x.Code.Contains(word, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(word, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Id).ToList();
        var context = new AssistantContext("website", "visitor:a", "visitor", [], [], false, ["public"]);
        var result = await tools.RunAsync(context, "search_public_products", new JsonObject { ["query"] = word, ["pageSize"] = 50 }, default);
        Assert.Equal("ok", (string)result["status"]!);
        Assert.Equal(truth.Count, (int)result["data"]!["totalCount"]!);
        Assert.Equal(truth.Take(50).Select(x => x.Id), result["data"]!["items"]!.AsArray().Select(x => (int)x!["id"]!));
        Assert.Equal(0, repo.GetItemCalls);
    }

    [Fact]
    public async Task Public_search_still_hides_items_that_are_not_published()
    {
        var sample = SampleRepositoryFactory.Create(); var items = await AllItems(sample);
        var published = new HashSet<int> { items[0].Id }; var repo = new CountingRepository(sample);
        var tools = new AssistantTools(new Permissions(null), repo, new NoKnowledge(), new Publications(published));
        var context = new AssistantContext("website", "visitor:a", "visitor", [], [], false, ["public"]);
        var result = await tools.RunAsync(context, "search_public_products", new JsonObject(), default);
        Assert.Equal(1, (int)result["data"]!["totalCount"]!);
        Assert.Equal(items[0].Id, (int)result["data"]!["items"]![0]!["id"]!);
    }

    [Fact]
    public async Task Store_scoped_search_reads_stock_in_bulk_with_the_same_result()
    {
        var sample = SampleRepositoryFactory.Create(); var items = await AllItems(sample);
        var visible = new List<int>();
        foreach (var item in items) if (CountingRepository.Rows(item.Id).Any(s => s.StoreId == 1)) visible.Add(item.Id);
        var repo = new CountingRepository(sample);
        var tools = new AssistantTools(new Permissions(new("u", "", "erp", Grants, [1], false)), repo, new NoKnowledge(), new Publications(new HashSet<int>()));
        var context = new AssistantContext("erp", "u", "erp", Grants, [1], false);
        var result = await tools.RunAsync(context, "search_items", new JsonObject { ["pageSize"] = JsonNode.Parse("50") }, default);
        Assert.Equal("ok", (string)result["status"]!);
        Assert.Equal(visible.Count, (int)result["data"]!["totalCount"]!);
        Assert.Equal(0, repo.GetStockCalls);
        Assert.True(repo.BulkStockCalls >= 1);
    }

    [Fact]
    public async Task In_stock_filter_reads_stock_in_bulk_with_the_same_result()
    {
        var sample = SampleRepositoryFactory.Create(); var items = await AllItems(sample);
        var inStock = 0;
        foreach (var item in items) if (CountingRepository.Rows(item.Id).Sum(s => s.OnHand) > 0) inStock++;
        var repo = new CountingRepository(sample);
        var result = await ChatTestKit.RunTool("search_items", new { inStockOnly = true, pageSize = 50 }, repo);
        Assert.Equal("ok", (string)result["status"]!);
        Assert.Equal(inStock, (int)result["data"]!["totalCount"]!);
        Assert.Equal(0, repo.GetStockCalls);
        Assert.True(repo.BulkStockCalls >= 1);
    }

    [Fact]
    public async Task Evidence_check_reuses_answers_for_repeated_sources_across_messages()
    {
        var sample = SampleRepositoryFactory.Create(); var items = await AllItems(sample); var repo = new CountingRepository(sample);
        var access = new Permissions(new("u", "", "erp", Grants, [1], false));
        var tools = new AssistantTools(access, repo, new NoKnowledge(), new Publications(new HashSet<int>()));
        var evidence = new AssistantEvidence(new NoKnowledge(), new Publications(new HashSet<int>()), tools, repo, new AssistantRegistry(new ConfigurationBuilder().Build()));
        var context = new AssistantContext("erp", "u", "erp", Grants, [1], false);
        var visible = new List<int>();
        foreach (var item in items) if (CountingRepository.Rows(item.Id).Any(s => s.StoreId == 1)) visible.Add(item.Id);
        var sources = visible.Take(3).Select(id => new ChatSource("get_stock_by_store", id.ToString())).ToList();
        repo.GetStockCalls = 0;
        var cache = new EvidenceCheckCache();
        for (var message = 0; message < 40; message++) Assert.True(await evidence.CanUseAsync(context, sources, default, cache));
        Assert.Equal(sources.Count, repo.GetStockCalls);
    }

    [Fact]
    public async Task Evidence_cache_does_not_hide_a_source_that_is_not_visible()
    {
        var sample = SampleRepositoryFactory.Create(); var items = await AllItems(sample); var repo = new CountingRepository(sample);
        var access = new Permissions(new("u", "", "erp", Grants, [1], false));
        var tools = new AssistantTools(access, repo, new NoKnowledge(), new Publications(new HashSet<int>()));
        var evidence = new AssistantEvidence(new NoKnowledge(), new Publications(new HashSet<int>()), tools, repo, new AssistantRegistry(new ConfigurationBuilder().Build()));
        var context = new AssistantContext("erp", "u", "erp", Grants, [1], false);
        int? hidden = null;
        foreach (var item in items) if (!CountingRepository.Rows(item.Id).Any(s => s.StoreId == 1)) { hidden = item.Id; break; }
        Assert.NotNull(hidden);
        var cache = new EvidenceCheckCache();
        var sources = new List<ChatSource> { new("get_stock_by_store", hidden.ToString()!) };
        Assert.False(await evidence.CanUseAsync(context, sources, default, cache));
        Assert.False(await evidence.CanUseAsync(context, sources, default, cache));
    }
}
