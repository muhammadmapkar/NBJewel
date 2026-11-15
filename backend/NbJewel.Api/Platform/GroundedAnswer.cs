// Role: Renders public replies only from selected, returned public records and knowledge excerpts.
using System.Text.Json.Nodes;
using NbJewel.Api.Chat;
using NbJewel.Api.Models;
namespace NbJewel.Api.Platform;
public static class GroundedAnswer
{
    public static string? Render(IReadOnlyList<ChatSource> sources, IReadOnlyList<ToolRun> runs)
    {
        var lines = new List<string>();
        foreach (var source in sources.Distinct().Take(5))
        {
            foreach (var run in runs.Where(r => r.Name == source.Tool && r.Status == "ok" && r.RecordIds.Contains(source.RecordId)))
            {
                JsonObject? row = null;
                if (source.Tool == "search_knowledge" && run.Result["data"] is JsonArray chunks)
                    row = chunks.OfType<JsonObject>().FirstOrDefault(x => x["chunkId"]?.ToString() == source.RecordId);
                else if (source.Tool == "get_public_product" && run.Result["data"] is JsonObject product && product["id"]?.ToString() == source.RecordId)
                    row = product;
                else if (source.Tool == "search_public_products" && run.Result["data"]?["items"] is JsonArray items)
                    row = items.OfType<JsonObject>().FirstOrDefault(x => x["id"]?.ToString() == source.RecordId);
                if (row is null) continue;
                string Field(string key, int limit = 200) { var value = row[key]?.ToString() ?? ""; return value.Length > limit ? value[..limit] : value; }
                if (source.Tool == "search_knowledge") lines.Add($"Approved knowledge excerpt, {Field("title")}: \"{Field("text", 1000)}\"");
                else { var price = row["price"] is null ? "" : $"; Price: {Field("price")} {Field("currency")}"; lines.Add($"ID: {Field("id")}; Name: {Field("name")}; Metal: {Field("metalType")}; Purity: {Field("purity")}{price}"); }
                break;
            }
        }
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }
}
