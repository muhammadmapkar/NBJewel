// Role: Authorizes assistant tools and exposes only approved public catalog fields.
using System.Text.Json.Nodes;
using NbJewel.Api.Businesses;
using NbJewel.Api.Chat;
using NbJewel.Api.Identity;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Platform;

public sealed class AssistantTools(IPermissionAdapter permissions, IItemRepository repository, IKnowledgeRetriever knowledge, IPublicationReader publications, BusinessMemberships? memberships = null, BusinessContext? business = null) : IAssistantTools
{
    private static readonly Dictionary<string, string[]> Required = new()
    {
        ["get_item_details"] = ["items.read", "prices.read", "status.read"],
        ["search_items"] = ["items.read", "status.read", "stock.read"],
        ["list_items_by_status"] = ["items.read", "status.read", "stock.read"],
        ["get_item_price_breakdown"] = ["items.read", "prices.read"],
        ["get_stock_by_store"] = ["items.read", "stock.read"],
        ["list_low_stock"] = ["items.read", "stock.read"],
        ["list_categories"] = ["reference.read"],
        ["get_latest_metal_rate"] = ["prices.read", "reference.read"]
    };
    private static bool Can(AssistantContext c, string name) => c.AccountType == "erp" && c.Has("chat.read") && Required.TryGetValue(name, out var required) && required.All(c.Has);
    private static ToolDeclaration Declaration(string name, string description, JsonObject properties, params string[] required) => new(name, description, new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray(required.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()), ["additionalProperties"] = false });
    private static ToolDeclaration Knowledge() => Declaration("search_knowledge", "Search approved knowledge.", new() { ["query"] = new JsonObject { ["type"] = "string", ["maxLength"] = 500 } }, "query");
    private static ToolDeclaration PublicSearch() => Declaration("search_public_products", "Search published products.", new() { ["query"] = new JsonObject { ["type"] = "string", ["maxLength"] = 100 }, ["page"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }, ["pageSize"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 50 } });
    private static ToolDeclaration PublicGet() => Declaration("get_public_product", "Get a published product.", new() { ["itemId"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 } }, "itemId");
    public IReadOnlyList<ToolDeclaration> Declarations(AssistantContext context)
    {
        if (context.AssistantId is not ("erp" or "website")) return [];
        var list = new List<ToolDeclaration> { Knowledge() };
        if (context.AssistantId == "website") { list.Add(PublicSearch()); list.Add(PublicGet()); }
        else if (context.AssistantId == "erp") list.AddRange(new ChatTools(new ScopedChatDataReader(repository, context)).Declarations.Where(x => Can(context, x.Name)));
        return list;
    }
    private static JsonObject Result(string status, JsonNode? data = null, JsonArray? sources = null) => new() { ["status"] = status, ["data"] = data, ["sources"] = sources ?? new JsonArray() };
    private static JsonObject Source(string name, string id) => new() { ["tool"] = name, ["recordId"] = id };
    private static bool TextArg(JsonObject args, string name, int max, bool required, out string? value)
    {
        value = null;
        if (!args.TryGetPropertyValue(name, out var n)) return !required;
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s) || string.IsNullOrWhiteSpace(s) || s.Trim().Length > max) return false;
        value = s.Trim(); return true;
    }
    private static bool IntArg(JsonObject args, string name, int min, int max, int fallback, out int value)
    {
        value = fallback;
        if (!args.TryGetPropertyValue(name, out var n)) return true;
        return n is JsonValue v && v.TryGetValue<int>(out value) && value >= min && value <= max;
    }
    private static bool Only(JsonObject args, params string[] keys) => args.All(x => keys.Contains(x.Key));
    public async Task<JsonObject> RunAsync(AssistantContext context, string name, JsonObject args, CancellationToken ct)
    {
        if (context.AssistantId is not ("erp" or "website")) throw new AssistantDeniedException();
        if (context.AssistantId == "erp")
        {
            var access = await permissions.ResolveAsync(context.SubjectId, ct);
            var member = access is null || memberships is null ? null : await memberships.ResolveAsync(access, ct);
            if (memberships is not null && (member is null || member.Slug != context.BusinessId || business?.Business?.Slug != member.Slug)) throw new AssistantDeniedException();
            if (member is not null) access = BusinessMemberships.Effective(access!, member);
            if (access is null || access.AccountType != "erp" || !access.Permissions.Contains("chat.read")) throw new AssistantDeniedException();
            context = context with { AccountType = access.AccountType, Permissions = access.Permissions, StoreIds = access.StoreIds, AllStores = access.AllStores };
        }
        if (name == "search_knowledge")
        {
            if (!Only(args, "query") || !TextArg(args, "query", 500, true, out var query)) return Result("error");
            var hits = await knowledge.SearchAsync(context, query!, ct);
            return Result("ok", new JsonArray(hits.Select(h => (JsonNode)new JsonObject { ["title"] = h.Title, ["text"] = h.Text, ["chunkId"] = h.ChunkId }).ToArray()), new JsonArray(hits.Select(h => (JsonNode)Source(name, h.ChunkId)).ToArray()));
        }
        if (context.AssistantId == "erp")
        {
            if (!Can(context, name)) throw new AssistantDeniedException();
            return await new ChatTools(new ScopedChatDataReader(repository, context)).RunAsync(name, args, ct);
        }
        if (context.AssistantId != "website" || (name != "search_public_products" && name != "get_public_product")) throw new AssistantDeniedException();
        if (name == "get_public_product")
        {
            if (!Only(args, "itemId") || !args.ContainsKey("itemId") || !IntArg(args, "itemId", 1, int.MaxValue, 0, out var id)) return Result("error");
            var ids = await publications.PublishedItemIdsAsync(ct);
            if (!ids.Contains(id)) return Result("not_found");
            var item = await repository.GetItemAsync(id, ct);
            return item is null || !PublicCatalogPolicy.Allows(item, await PublicCatalogPolicy.AllowedCategoriesAsync(repository, ct)) ? Result("not_found") : Result("ok", await PublicItem(item, ct), new JsonArray(Source(name, id.ToString())));
        }
        if (!Only(args, "query", "page", "pageSize") || !TextArg(args, "query", 100, false, out var search) || !IntArg(args, "page", 1, int.MaxValue, 1, out var page) || !IntArg(args, "pageSize", 1, 50, 10, out var size)) return Result("error");
        var published = await publications.PublishedItemIdsAsync(ct);
        if (published.Count > 10000) throw new AssistantUnavailableException();
        // Read the catalog in pages of 100 (the repository search already narrows by text) and keep published matches,
        // instead of one query per published item. The repository search also matches design codes, so the code/name check stays.
        var categories = await PublicCatalogPolicy.AllowedCategoriesAsync(repository, ct);
        var matches = new List<Item>();
        for (var current = 1; ; current++)
        {
            var batch = await repository.SearchItemsAsync(search, null, null, null, ItemStatusRules.Complete, current, 100, ct);
            if (batch.TotalCount > 10000) throw new AssistantUnavailableException();
            matches.AddRange(batch.Items.Where(item => published.Contains(item.Id) && PublicCatalogPolicy.Allows(item, categories) &&
                (search is null || item.Code.Contains(search, StringComparison.OrdinalIgnoreCase) || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase))));
            if ((long)current * 100 >= batch.TotalCount) break;
            if (batch.Items.Count == 0) throw new AssistantUnavailableException();
        }
        matches = matches.OrderBy(item => item.Id).ToList();
        var shown = matches.Skip((int)Math.Min((long)(page - 1) * size, int.MaxValue)).Take(size).ToArray();
        var rows = new JsonArray(); foreach (var item in shown) rows.Add(await PublicItem(item, ct));
        return Result("ok", new JsonObject { ["items"] = rows, ["totalCount"] = matches.Count, ["page"] = page, ["pageSize"] = size }, new JsonArray(shown.Select(i => (JsonNode)Source(name, i.Id.ToString())).ToArray()));
    }
    private async Task<JsonObject> PublicItem(Item item, CancellationToken ct)
    {
        var price = await repository.GetPriceAsync(item.Id, ct);
        return new JsonObject { ["id"] = item.Id, ["code"] = item.Code, ["name"] = item.Name, ["categoryId"] = item.CategoryId, ["metalType"] = item.MetalType, ["purity"] = item.Purity, ["imageUrl"] = item.ImageUrl, ["price"] = price?.TotalPrice, ["currency"] = price?.Currency };
    }
}
