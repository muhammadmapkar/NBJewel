// Role: Proves the Sample and SQL providers give the same answer. It runs one fixed
// script of reads and writes through the real controllers on each provider, turns every
// response into JSON (status code plus body), and compares them step by step. Numbers
// are compared by value (5.2 equals 5.200). It needs SQL Server, so it is skipped
// unless NBJEWEL_SQL_CONNECTION is set. Without it, Sample-only tests still run.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests;

public sealed class ParityTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [SqlFact]
    public async Task EveryEndpoint_ReturnsTheSameJsonOnSampleAndSql()
    {
        var sample = await Script(Repos.Create("Sample"));
        var sql = await Script(Repos.Create("Sql"));

        Assert.Equal(sample.Count, sql.Count);
        for (var i = 0; i < sample.Count; i++)
        {
            Assert.Equal(sample[i].Label, sql[i].Label);
            Assert.True(sample[i].Json == sql[i].Json, $"Step '{sample[i].Label}' differs.\nSample: {sample[i].Json}\nSql:    {sql[i].Json}");
        }
    }

    [SqlFact]
    public async Task RatesWithTies_ReturnTheSameJsonOnSampleAndSql()
    {
        var sample = Describe(await new ReferenceDataController(Repos.CreateWithRates("Sample", "metal-rates.ties.json")).GetLatestRates(Ct));
        var sql = Describe(await new ReferenceDataController(Repos.CreateWithRates("Sql", "metal-rates.ties.json")).GetLatestRates(Ct));
        Assert.Equal(sample, sql);
    }

    // Runs the script. Each step is a label plus the normalized JSON of the HTTP result.
    private static async Task<List<(string Label, string Json)>> Script(IItemRepository repository)
    {
        var items = new ItemsController(repository);
        var reference = new ReferenceDataController(repository);
        var steps = new List<(string, string)>();
        void Add(string label, string json) => steps.Add((label, json));

        // Reads
        Add("items all", Describe(await items.GetItems(null, null, null, null, null, 1, 100, Ct)));
        Add("items page 2", Describe(await items.GetItems(null, null, null, null, null, 2, 7, Ct)));
        Add("items search", Describe(await items.GetItems("ring", null, null, null, null, 1, 100, Ct)));
        Add("items search design", Describe(await items.GetItems("dsn-10", null, null, null, null, 1, 100, Ct)));
        Add("items category", Describe(await items.GetItems(null, 2, null, null, null, 1, 100, Ct)));
        Add("items metal+purity", Describe(await items.GetItems(null, null, "gold", "22k", null, 1, 100, Ct)));
        Add("items status", Describe(await items.GetItems(null, null, null, null, "draft", 1, 100, Ct)));
        Add("items bad page", Describe(await items.GetItems(null, null, null, null, null, 0, 100, Ct)));
        foreach (var id in new[] { 1, 2, 3, 9999 })
        {
            Add($"item {id}", Describe(await items.GetItem(id, Ct)));
            Add($"stock {id}", Describe(await items.GetStock(id, Ct)));
            Add($"variants {id}", Describe(await items.GetVariants(id, Ct)));
            Add($"price {id}", Describe(await items.GetPrice(id, Ct)));
        }
        Add("categories", Describe(await reference.GetCategories(Ct)));
        Add("stores", Describe(await reference.GetStores(Ct)));
        Add("designs", Describe(await reference.GetDesigns(Ct)));
        Add("rates", Describe(await reference.GetLatestRates(Ct)));
        Add("low stock default", Describe(await reference.GetLowStock(cancellationToken: Ct)));
        Add("low stock 5", Describe(await reference.GetLowStock(5, Ct)));
        Add("low stock bad", Describe(await reference.GetLowStock(0, Ct)));

        // Writes and their errors
        var create = new ItemCreateRequest("PAR-1", "Parity Ring", 2, 3, "gold", "22k", 12.3456m, 999m, 1.2m, "PerGram", 3.456m, "HUID01", "/images/par.jpg");
        Add("create", Describe(await items.Create(create, Ct)));
        Add("create duplicate", Describe(await items.Create(create, Ct)));
        Add("create stone>gross", Describe(await items.Create(create with { Code = "PAR-2", GrossWt = 1m, StoneWt = 2m }, Ct)));
        Add("create bad category", Describe(await items.Create(create with { Code = "PAR-3", CategoryId = 99 }, Ct)));
        Add("create bad purity", Describe(await items.Create(create with { Code = "PAR-4", Purity = "11K" }, Ct)));
        Add("update", Describe(await items.Update(31, new ItemUpdateRequest("PAR-1", "Parity Ring 2", 3, 4, "Gold", "18K", 20m, 0m, 2m, "flat", 50m, null, null), Ct)));
        Add("update missing", Describe(await items.Update(9999, new ItemUpdateRequest("PAR-9", "None", 2, 1, "Gold", "22K", 1m, 0m, 0m, "flat", 1m, null, null), Ct)));
        Add("status", Describe(await items.UpdateStatus(31, new StatusUpdateRequest("with smith"), Ct)));
        Add("status bad", Describe(await items.UpdateStatus(31, new StatusUpdateRequest("Pending"), Ct)));
        Add("stock add new row", Describe(await items.AddStock(1, new StockAddRequest(12, 1, 3), Ct)));
        Add("stock add existing row", Describe(await items.AddStock(1, new StockAddRequest(11, 1, 2), Ct)));
        Add("stock add no-variant item", Describe(await items.AddStock(3, new StockAddRequest(null, 2, 4), Ct)));
        Add("stock add bad variant", Describe(await items.AddStock(1, new StockAddRequest(21, 1, 1), Ct)));
        Add("stock add bad qty", Describe(await items.AddStock(1, new StockAddRequest(12, 1, 0), Ct)));
        Add("stock add overflow", Describe(await items.AddStock(1, new StockAddRequest(12, 1, int.MaxValue), Ct)));
        Add("stock add missing item", Describe(await items.AddStock(9999, new StockAddRequest(12, 1, 1), Ct)));

        // Reads after the writes
        Add("items after writes", Describe(await items.GetItems(null, null, null, null, null, 1, 100, Ct)));
        Add("stock 1 after", Describe(await items.GetStock(1, Ct)));
        Add("stock 3 after", Describe(await items.GetStock(3, Ct)));
        Add("low stock after", Describe(await reference.GetLowStock(3, Ct)));
        return steps;
    }

    // Status code plus the body as JSON, with every number written the same way.
    private static string Describe<T>(ActionResult<T> result)
    {
        if (result.Result is null) return Wrap(200, result.Value);
        return Describe(result.Result);
    }

    private static string Describe(IActionResult result) => result switch
    {
        ObjectResult o => Wrap(o.StatusCode ?? 200, o.Value),
        StatusCodeResult s => Wrap(s.StatusCode, null),
        _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}.")
    };

    private static string Wrap(int status, object? body)
    {
        var node = new JsonObject { ["status"] = status, ["body"] = Normalize(JsonSerializer.SerializeToNode(body, Web)) };
        return node.ToJsonString();
    }

    // 5.2 and 5.200 are the same number. Write every number as a double so the text matches.
    private static JsonNode? Normalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                var copy = new JsonObject();
                foreach (var pair in o) copy[pair.Key] = Normalize(pair.Value);
                return copy;
            case JsonArray a:
                var list = new JsonArray();
                foreach (var x in a) list.Add(Normalize(x));
                return list;
            case JsonValue v when v.TryGetValue<decimal>(out var d):
                return JsonValue.Create((double)d);
            default:
                return node?.DeepClone();
        }
    }
}
