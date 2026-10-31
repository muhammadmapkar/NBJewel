// Role: QA check that the five guards do not wrongly refuse the plain answers the demo expects.
// Each case runs a real tool on the sample data, then checks a typical model answer against it.
// This is a stand-in for a live Gemini run, which needs a key. Real wording from the model can differ.

using System.Text.Json.Nodes;
using NbJewel.Api.Chat;
using static NbJewel.Api.Tests.Chat.ChatTestKit;

namespace NbJewel.Api.Tests.Chat;

public sealed class DemoAnswersGuardTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "get_stock_by_store", """{"itemId":"zork"}""", "RNG-0142 has Dubai 4, Chennai 2 and Bengaluru 1." },
        { "get_latest_metal_rate", """{"metal":"Gold","purity":"22K"}""", "The latest 22K gold rate is 66 AED per gram." },
        { "list_items_by_status", """{"status":"Draft"}""", "The Draft items are BNG-0011 and EAR-0022." },
        { "search_items", """{"query":"zork"}""", "Yes, the Zork Ring (RNG-0142) is in the catalog." },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TypicalDemoAnswer_PassesAllGuards(string tool, string argsJson, string text)
    {
        var args = JsonNode.Parse(argsJson)!.AsObject();
        if (args["itemId"] is not null)
        {
            // Stock tools take the numeric id, so look the item up by name first.
            var found = await Tools().RunAsync("search_items", new JsonObject { ["query"] = "zork" }, CancellationToken.None);
            // Parse from text so the number is JSON-backed, as it is when it comes from the model.
            args["itemId"] = JsonNode.Parse(found["data"]!["items"]![0]!["id"]!.ToJsonString());
        }
        var result = await Tools().RunAsync(tool, args, CancellationToken.None);
        var run = new ToolRun(tool, args, result);
        Assert.True(run.Status == "ok", result.ToJsonString());

        var sources = run.RecordIds.Take(1).Select(id => (tool, id)).ToArray();
        var verdict = Guards.Run(Answer(text, sources), [run], SystemPrompt.Text);

        Assert.True(verdict.Passed, $"{verdict.Guard}: {verdict.Reason}");
    }
}
