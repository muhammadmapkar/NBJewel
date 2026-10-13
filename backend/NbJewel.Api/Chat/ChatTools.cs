// Role: The eight read-only catalog tools the chat assistant may call, ported from the old
// Python ai-service/app/tools/catalog_tools.py. Each tool reads data only through
// IChatDataReader (no writes, no SQL) and returns the same JSON shape the Python tools
// returned: {"status": "ok" | "not_found" | "error", "data": ..., "sources": [...]}.
// The tool results match what the API endpoints return, because both use the same repository
// and the same JSON settings. Arguments are checked here: unknown fields are rejected.
// List tools add the stock of each item themselves (assumed: no list endpoint has stock),
// so the model needs one tool call, not one per item. Stock is read one item at a time,
// which is fine for the sample catalog (assumed small).

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using NbJewel.Api.Models;

namespace NbJewel.Api.Chat;

public sealed record ToolDeclaration(string Name, string Description, JsonObject Parameters);

public sealed class ChatTools
{
    private readonly IChatDataReader data;
    private const int MaxPageSize = 100;
    private const int LowStockLimit = 50;
    // Same rule as the dashboard Needs attention card (GET /api/stock/low): low means fewer than this
    // many units available (on hand minus reserved). Assumed.
    private const int LowStockDefaultThreshold = 2;
    private static readonly string[] Statuses = ["Draft", "With Smith", "Received", "Complete"];
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Only these item fields are passed to the model. Free-text fields that are not part of the item
    // contract could carry planted instructions, so they are dropped.
    private static readonly string[] ItemFields = ["id", "code", "name", "categoryId", "designCode", "metalType", "purity", "grossWt", "netWt", "stoneWt", "makingType", "makingValue", "huid", "status", "imageUrl"];
    private static readonly string[] ItemSummaryFields = ["id", "code", "name", "categoryId", "metalType", "purity", "netWt", "status"];

    private readonly Dictionary<string, Func<ArgReader, CancellationToken, Task<JsonObject>>> handlers = [];

    /// <summary>Name, description and JSON schema of every tool, for the language model.</summary>
    public IReadOnlyList<ToolDeclaration> Declarations { get; }

    public ChatTools(IChatDataReader data)
    {
        this.data = data;
        Declarations = BuildTools();
    }

    // Fills the handler table and builds the declarations.
    private IReadOnlyList<ToolDeclaration> BuildTools()
    {
        handlers["search_items"] = SearchItems;
        handlers["get_item_details"] = GetItemDetails;
        handlers["get_item_price_breakdown"] = GetItemPriceBreakdown;
        handlers["get_stock_by_store"] = GetStockByStore;
        handlers["list_low_stock"] = ListLowStock;
        handlers["list_categories"] = ListCategories;
        handlers["get_latest_metal_rate"] = GetLatestMetalRate;
        handlers["list_items_by_status"] = ListItemsByStatus;

        var itemId = Schema(new JsonObject { ["itemId"] = IntSchema("Numeric item id.", 1) }, "itemId");
        return
        [
            new("search_items", "Search the item catalog by text, category, metal, purity or status. Each item includes onHand, the units in stock across all stores. Set inStockOnly to keep only items with stock; totalCount then counts only those. Use totalCount to count items.",
                Schema(new JsonObject
                {
                    ["query"] = StrSchema("Text to match in the item code or name.", 100),
                    ["category"] = StrSchema("Category name (for example Rings) or category id.", 60),
                    ["metal"] = StrSchema("Metal type, for example Gold or Silver.", 30),
                    ["purity"] = StrSchema("Purity, for example 22K, 18K or 925.", 10),
                    ["status"] = StrSchema("Item status.", null, Statuses),
                    ["page"] = IntSchema("Page number, starting at 1.", 1, null, 1),
                    ["pageSize"] = IntSchema("Items per page, 1 to 50.", 1, 50, 10),
                    ["inStockOnly"] = new JsonObject { ["type"] = "boolean", ["description"] = "If true, return only items with at least 1 unit on hand in any store.", ["default"] = false },
                })),
            new("get_item_details", "Get every field of one item, by item id or by item code.",
                Schema(new JsonObject { ["itemId"] = IntSchema("Numeric item id.", 1), ["code"] = StrSchema("Item code, for example RNG-0142.", 40) })),
            new("get_item_price_breakdown", "Get the price breakdown of one item: net weight, rate per gram, metal value, making charge and total.", itemId),
            new("get_stock_by_store", "Get on hand, reserved and available stock of one item in each store.", itemId.DeepClone().AsObject()),
            new("list_low_stock", "List items with fewer than a threshold number of units available (on hand minus reserved, default 2), across all stores or in one store.",
                Schema(new JsonObject
                {
                    ["threshold"] = IntSchema("Items with fewer than this many units available (on hand minus reserved) are listed.", 1, 1000, LowStockDefaultThreshold),
                    ["store"] = StrSchema("Optional store name or store id to check only that store.", 60),
                })),
            new("list_categories", "List all product categories.", Schema(new JsonObject())),
            new("get_latest_metal_rate", "Get the latest rate per gram for a metal, optionally for one purity.",
                Schema(new JsonObject { ["metal"] = StrSchema("Metal type, for example Gold or Silver.", 30), ["purity"] = StrSchema("Optional purity, for example 22K.", 10) }, "metal")),
            new("list_items_by_status", "List items that have a given status (Draft, With Smith, Received or Complete). Each item includes onHand, the units in stock across all stores.",
                Schema(new JsonObject { ["status"] = StrSchema("Item status.", null, Statuses) }, "status")),
        ];
    }

    /// <summary>Runs one tool by name. It never throws: every failure becomes a result with status "error".</summary>
    public async Task<JsonObject> RunAsync(string name, JsonObject? args, CancellationToken cancellationToken)
    {
        if (!handlers.TryGetValue(name, out var handler)) return Error($"Unknown tool '{name}'.");
        try { return await handler(new ArgReader(args), cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Error("The tool failed unexpectedly."); }
    }

    // ---------- tools ----------

    private async Task<JsonObject> SearchItems(ArgReader a, CancellationToken ct)
    {
        var query = a.Text("query", 100);
        var category = a.Text("category", 60);
        var metal = a.Text("metal", 30);
        var purity = a.Text("purity", 10);
        var status = a.Status("status", false);
        var page = a.Int("page", 1, int.MaxValue) ?? 1;
        var pageSize = a.Int("pageSize", 1, 50) ?? 10;
        var inStockOnly = a.Bool("inStockOnly");
        if (a.Problem() is { } problem) return Error(problem);

        int? categoryId = null;
        if (category is not null)
        {
            categoryId = await ResolveCategoryAsync(category, ct);
            if (categoryId is null) return NotFound($"No category named '{category}'.");
        }

        List<Item> items;
        int total;
        Dictionary<int, IReadOnlyList<StockRecord>> stock;
        if (inStockOnly)
        {
            // The data layer cannot filter by stock, so read every match, check stock, then page the result here.
            var matches = await FetchAllItemsAsync(query, categoryId, metal, purity, status, ct);
            stock = await StockOfAsync(matches.Select(i => i.Id), ct);
            matches = matches.Where(i => OnHand(stock[i.Id]) > 0).ToList();
            total = matches.Count;
            items = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            var result = await data.SearchItemsAsync(query, categoryId, metal, purity, status, page, pageSize, ct);
            items = result.Items.ToList();
            total = result.TotalCount;
            stock = await StockOfAsync(items.Select(i => i.Id), ct);
        }
        if (items.Count == 0) return NotFound(total == 0 ? "No items match the search." : "That page has no items.");

        var summaries = new JsonArray(items.Select(i => (JsonNode)WithOnHand(Summary(i), stock[i.Id])).ToArray());
        return Ok(new JsonObject { ["items"] = summaries, ["page"] = page, ["pageSize"] = pageSize, ["totalCount"] = total },
            items.Select(i => Source("search_items", i.Id)));
    }

    private async Task<JsonObject> GetItemDetails(ArgReader a, CancellationToken ct)
    {
        var itemId = a.Int("itemId", 1, int.MaxValue);
        var code = a.Text("code", 40);
        if ((itemId is null) == (code is null)) a.AddError("arguments", "Give either itemId or code, not both and not neither.");
        if (a.Problem() is { } problem) return Error(problem);

        Item? item;
        if (itemId is not null)
        {
            item = await data.GetItemAsync(itemId.Value, ct);
            if (item is null) return NotFound($"No item with id {itemId}.");
        }
        else
        {
            // The search matches part of a code, so keep only an exact code match.
            var result = await data.SearchItemsAsync(code, null, null, null, null, 1, MaxPageSize, ct);
            item = result.Items.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase));
            if (item is null) return NotFound($"No item with code '{code}'.");
        }
        return Ok(Pick(ToObject(item), ItemFields), [Source("get_item_details", item.Id)]);
    }

    private async Task<JsonObject> GetItemPriceBreakdown(ArgReader a, CancellationToken ct)
    {
        var itemId = a.Int("itemId", 1, int.MaxValue, required: true);
        if (a.Problem() is { } problem) return Error(problem);

        var item = await data.GetItemAsync(itemId!.Value, ct);
        if (item is null) return NotFound($"No item with id {itemId}.");
        var price = await data.GetPriceAsync(itemId.Value, ct);
        if (price is null) return NotFound("No latest metal rate exists for this item.");

        var result = ItemLabel(item);
        foreach (var (key, value) in ToObject(price)) result[key] = value?.DeepClone();
        return Ok(result, [Source("get_item_price_breakdown", itemId.Value)]);
    }

    private async Task<JsonObject> GetStockByStore(ArgReader a, CancellationToken ct)
    {
        var itemId = a.Int("itemId", 1, int.MaxValue, required: true);
        if (a.Problem() is { } problem) return Error(problem);

        var item = await data.GetItemAsync(itemId!.Value, ct);
        if (item is null) return NotFound($"No item with id {itemId}.");
        var records = await data.GetStockAsync(itemId.Value, ct);
        if (records.Count == 0) return NotFound("This item has no stock records.");

        var stores = (await data.GetStoresAsync(ct)).ToDictionary(s => s.Id);
        var rows = records.Select(r => (JsonNode)new JsonObject
        {
            ["storeId"] = r.StoreId,
            ["storeName"] = stores.TryGetValue(r.StoreId, out var s) ? s.Name : null,
            ["city"] = stores.TryGetValue(r.StoreId, out var c) ? c.City : null,
            ["onHand"] = r.OnHand,
            ["reserved"] = r.Reserved,
            ["available"] = r.OnHand - r.Reserved,
        }).ToArray();
        var result = new JsonObject { ["itemId"] = itemId.Value };
        foreach (var (key, value) in ItemLabel(item)) result[key] = value?.DeepClone();
        result["stores"] = new JsonArray(rows);
        result["totalOnHand"] = records.Sum(r => r.OnHand);
        return Ok(result, [Source("get_stock_by_store", itemId.Value)]);
    }

    private async Task<JsonObject> ListLowStock(ArgReader a, CancellationToken ct)
    {
        var threshold = a.Int("threshold", 1, 1000) ?? LowStockDefaultThreshold;
        var store = a.Text("store", 60);
        if (a.Problem() is { } problem) return Error(problem);

        int? storeId = null;
        if (store is not null)
        {
            var stores = await data.GetStoresAsync(ct);
            if (store.All(char.IsAsciiDigit) && int.TryParse(store, out var numeric) && stores.Any(s => s.Id == numeric)) storeId = numeric;
            else storeId = stores.FirstOrDefault(s => string.Equals(s.Name, store, StringComparison.OrdinalIgnoreCase))?.Id;
            if (storeId is null) return NotFound($"No store named '{store}'.");
        }

        var items = await FetchAllItemsAsync(null, null, null, null, null, ct);
        var stock = await StockOfAsync(items.Select(i => i.Id), ct);
        var low = items.Select(i => (Item: i, Available: Available(stock[i.Id], storeId)))
            .Where(x => x.Available < threshold)
            .OrderBy(x => x.Available).ThenBy(x => x.Item.Id)
            .ToList();
        if (low.Count == 0) return NotFound($"No items have fewer than {threshold} units available.");

        var shown = low.Take(LowStockLimit).ToList();
        var rows = shown.Select(x => (JsonNode)new JsonObject { ["itemId"] = x.Item.Id, ["code"] = x.Item.Code, ["name"] = x.Item.Name, ["available"] = x.Available }).ToArray();
        return Ok(new JsonObject { ["threshold"] = threshold, ["store"] = store, ["count"] = low.Count, ["items"] = new JsonArray(rows) },
            shown.Select(x => Source("list_low_stock", x.Item.Id)));
    }

    private async Task<JsonObject> ListCategories(ArgReader a, CancellationToken ct)
    {
        if (a.Problem() is { } problem) return Error(problem);
        var categories = await data.GetCategoriesAsync(ct);
        if (categories.Count == 0) return NotFound("No categories exist.");
        return Ok(ToArray(categories), categories.Select(c => Source("list_categories", c.Id)));
    }

    private async Task<JsonObject> GetLatestMetalRate(ArgReader a, CancellationToken ct)
    {
        var metal = a.Text("metal", 30, required: true);
        var purity = a.Text("purity", 10);
        if (a.Problem() is { } problem) return Error(problem);

        var matches = (await data.GetLatestRatesAsync(ct))
            .Where(r => string.Equals(r.MetalType, metal, StringComparison.OrdinalIgnoreCase)
                && (purity is null || string.Equals(r.Purity, purity, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (matches.Count == 0) return NotFound("No metal rate matches that metal and purity.");
        return Ok(ToArray(matches), matches.Select(r => Source("get_latest_metal_rate", r.Id)));
    }

    private async Task<JsonObject> ListItemsByStatus(ArgReader a, CancellationToken ct)
    {
        var status = a.Status("status", true);
        if (a.Problem() is { } problem) return Error(problem);

        var result = await data.SearchItemsAsync(null, null, null, null, status, 1, MaxPageSize, ct);
        if (result.Items.Count == 0) return NotFound($"No items have status {status}.");
        var stock = await StockOfAsync(result.Items.Select(i => i.Id), ct);
        var rows = result.Items.Select(i => (JsonNode)WithOnHand(Summary(i), stock[i.Id])).ToArray();
        return Ok(new JsonObject { ["status"] = status, ["totalCount"] = result.TotalCount, ["items"] = new JsonArray(rows), ["truncated"] = result.TotalCount > result.Items.Count },
            result.Items.Select(i => Source("list_items_by_status", i.Id)));
    }

    // ---------- helpers ----------

    private async Task<int?> ResolveCategoryAsync(string value, CancellationToken ct)
    {
        if (value.All(char.IsAsciiDigit) && int.TryParse(value, out var id)) return id;
        var match = (await data.GetCategoriesAsync(ct)).FirstOrDefault(c => string.Equals(c.Name, value, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    private async Task<List<Item>> FetchAllItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, CancellationToken ct)
    {
        var items = new List<Item>();
        for (var page = 1; ; page++)
        {
            var result = await data.SearchItemsAsync(search, categoryId, metal, purity, status, page, MaxPageSize, ct);
            items.AddRange(result.Items);
            if (page * MaxPageSize >= result.TotalCount) return items;
        }
    }

    private async Task<Dictionary<int, IReadOnlyList<StockRecord>>> StockOfAsync(IEnumerable<int> itemIds, CancellationToken ct)
    {
        var stock = new Dictionary<int, IReadOnlyList<StockRecord>>();
        foreach (var id in itemIds.Distinct()) stock[id] = await data.GetStockAsync(id, ct);
        return stock;
    }

    private static int OnHand(IEnumerable<StockRecord> records, int? storeId = null) => records.Where(r => storeId is null || r.StoreId == storeId).Sum(r => r.OnHand);
    private static int Available(IEnumerable<StockRecord> records, int? storeId = null) => records.Where(r => storeId is null || r.StoreId == storeId).Sum(r => r.OnHand - r.Reserved);

    private static JsonObject Summary(Item item) => Pick(ToObject(item), ItemSummaryFields);
    private static JsonObject WithOnHand(JsonObject summary, IEnumerable<StockRecord> records) { summary["onHand"] = OnHand(records); return summary; }
    // The code and name of an item, so answers can name the item the numbers belong to.
    private static JsonObject ItemLabel(Item item) => new() { ["itemCode"] = item.Code, ["itemName"] = item.Name };

    private static JsonObject ToObject<T>(T value) => JsonSerializer.SerializeToNode(value, Web)!.AsObject();
    private static JsonArray ToArray<T>(IEnumerable<T> values) => JsonSerializer.SerializeToNode(values, Web)!.AsArray();

    private static JsonObject Pick(JsonObject source, string[] keys)
    {
        var picked = new JsonObject();
        foreach (var key in keys) picked[key] = source[key]?.DeepClone();
        return picked;
    }

    private static JsonObject Source(string tool, int recordId) => new() { ["tool"] = tool, ["recordId"] = recordId };

    private static JsonObject Ok(JsonNode payload, IEnumerable<JsonObject> sources) =>
        new() { ["status"] = "ok", ["data"] = payload, ["sources"] = new JsonArray(sources.Cast<JsonNode>().ToArray()) };
    public static JsonObject NotFound(string message) => Failure("not_found", message);
    public static JsonObject Error(string message) => Failure("error", message);
    private static JsonObject Failure(string status, string message) =>
        new() { ["status"] = status, ["data"] = new JsonObject { ["message"] = message }, ["sources"] = new JsonArray() };

    // ---------- JSON schema helpers for the tool declarations ----------

    private static JsonObject Schema(JsonObject properties, params string[] required)
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Length > 0) schema["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray());
        return schema;
    }

    private static JsonObject StrSchema(string description, int? maxLength, string[]? allowed = null)
    {
        var schema = new JsonObject { ["type"] = "string", ["description"] = description };
        if (maxLength is not null) schema["maxLength"] = maxLength;
        if (allowed is not null) schema["enum"] = new JsonArray(allowed.Select(v => (JsonNode)v).ToArray());
        return schema;
    }

    private static JsonObject IntSchema(string description, int minimum, int? maximum = null, int? defaultValue = null)
    {
        var schema = new JsonObject { ["type"] = "integer", ["description"] = description, ["minimum"] = minimum };
        if (maximum is not null) schema["maximum"] = maximum;
        if (defaultValue is not null) schema["default"] = defaultValue;
        return schema;
    }

    // Reads and checks the arguments of one tool call. Problem() lists everything wrong,
    // including fields the tool does not know.
    private sealed class ArgReader(JsonObject? raw)
    {
        private readonly JsonObject args = raw ?? [];
        private readonly HashSet<string> used = [];
        private readonly List<string> errors = [];

        public void AddError(string field, string message) => errors.Add($"{field}: {message}");

        public string? Text(string name, int maxLength, bool required = false)
        {
            used.Add(name);
            if (!args.TryGetPropertyValue(name, out var node) || node is null) { if (required) AddError(name, "is required."); return null; }
            if (node is not JsonValue value || !value.TryGetValue<string>(out var text)) { AddError(name, "must be text."); return null; }
            text = text.Trim();
            if (text.Length == 0) { if (required) AddError(name, "is required."); return null; }
            if (text.Length > maxLength) { AddError(name, $"must be at most {maxLength} characters."); return null; }
            return text;
        }

        public int? Int(string name, int min, int max, bool required = false)
        {
            used.Add(name);
            if (!args.TryGetPropertyValue(name, out var node) || node is null) { if (required) AddError(name, "is required."); return null; }
            if (node is not JsonValue value || !value.TryGetValue<decimal>(out var number) || number != decimal.Truncate(number)) { AddError(name, "must be a whole number."); return null; }
            if (number < min || number > max) { AddError(name, $"must be between {min} and {max}."); return null; }
            return (int)number;
        }

        public bool Bool(string name)
        {
            used.Add(name);
            if (!args.TryGetPropertyValue(name, out var node) || node is null) return false;
            if (node is JsonValue value && value.TryGetValue<bool>(out var flag)) return flag;
            AddError(name, "must be true or false.");
            return false;
        }

        // Accepts any letter case ("with smith") and returns the canonical status.
        public string? Status(string name, bool required)
        {
            var text = Text(name, 20, required);
            if (text is null) return null;
            var title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
            if (Statuses.Contains(title)) return title;
            AddError(name, $"must be one of {string.Join(", ", Statuses)}.");
            return null;
        }

        public string? Problem()
        {
            foreach (var key in args.Select(p => p.Key).Where(k => !used.Contains(k))) AddError(key, "is not a known argument.");
            return errors.Count == 0 ? null : "Invalid arguments. " + string.Join("; ", errors);
        }
    }
}
