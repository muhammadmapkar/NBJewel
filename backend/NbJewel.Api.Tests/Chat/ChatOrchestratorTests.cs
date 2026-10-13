// Role: Tests for the chat loop with a fake language model (no key, no network). They cover
// grounded answers, the refusals each guard causes, the tool and model call caps, JSON retry,
// history limits, "No data found.", and that no tool data means no answer.

using System.Text.Json.Nodes;
using NbJewel.Api.Chat;
using NbJewel.Api.Models;
using static NbJewel.Api.Tests.Chat.ChatTestKit;

namespace NbJewel.Api.Tests.Chat;

public sealed class ChatOrchestratorTests
{
    private static Task<ChatResponse> Ask(FakeLlm llm, string question = "Tell me about the Zork Ring", List<ChatHistoryMessage>? history = null) =>
        Orchestrator(llm).AskAsync(question, history, CancellationToken.None);

    private static void AssertRefused(ChatResponse response)
    {
        Assert.True(response.Refused);
        Assert.Equal(Guards.RefusalText, response.Answer);
        Assert.Empty(response.Sources);
    }

    [Fact]
    public async Task NoToolCalled_IsRefused()
    {
        // Off-topic: the model answers from memory without any tool.
        var response = await Ask(new FakeLlm(Say("Gold is about 72 per gram.", ("search_items", "1"))), "What is the price of gold on the world market?");
        AssertRefused(response);
    }

    [Fact]
    public async Task NoToolData_MeansNoAnswer_NotEvenAFriendlyOne()
    {
        var response = await Ask(new FakeLlm(Say("I think the Zork Ring is nice.")), "Opinion?");
        AssertRefused(response);
    }

    [Fact]
    public async Task GroundedAnswer_Passes_AndKeepsTheRequestShape()
    {
        var llm = new FakeLlm(Call("search_items", new { query = "zork" }), Say("The Zork Ring (RNG-0142) is Gold 22K with a net weight of 42.758 g.", ("search_items", "1")));
        var response = await Ask(llm);

        Assert.False(response.Refused);
        Assert.Contains("42.758", response.Answer);
        Assert.Equal([new ChatSource("search_items", "1")], response.Sources);
        Assert.Equal(ChatOrchestrator.Temperature, llm.Temperatures[0]);
        Assert.Equal(0.1, llm.Temperatures[0]);
        Assert.Equal("system", llm.Calls[0][0].Role);
        Assert.Equal(SystemPrompt.Text, llm.Calls[0][0].Content);
        Assert.Equal(8, llm.ToolLists[0].Count);
        Assert.Equal("tool", llm.Calls[1][^1].Role); // the result was fed back
    }

    [Fact]
    public async Task EmptySources_AreRefused()
    {
        var llm = new FakeLlm(Call("search_items", new { query = "zork" }), Say("Zork Ring is Gold 22K."));
        AssertRefused(await Ask(llm));
    }

    [Fact]
    public async Task InventedNumber_IsRefused()
    {
        var llm = new FakeLlm(Call("search_items", new { query = "zork" }), Say("Zork Ring weighs 99.5 g.", ("search_items", "1")));
        AssertRefused(await Ask(llm));
    }

    [Fact]
    public async Task SourceFromAToolThatWasNotCalled_IsRefused()
    {
        var llm = new FakeLlm(Call("search_items", new { query = "zork" }), Say("Zork Ring is Gold 22K.", ("get_stock_by_store", "1")));
        AssertRefused(await Ask(llm));
    }

    [Fact]
    public async Task AnswerThatRepeatsThePrompt_IsRefused()
    {
        var llm = new FakeLlm(Call("search_items", new { query = "zork" }), Say("Never use outside knowledge. Never guess. Never estimate.", ("search_items", "1")));
        AssertRefused(await Ask(llm));
    }

    [Fact]
    public async Task EveryToolFindingNothing_AnswersNoDataFound_WithoutRefusing()
    {
        // Even if the model words its own answer, "No data found." is what the user gets.
        var llm = new FakeLlm(Call("search_items", new { query = "nothing-matches-this" }), Say("Sorry, I could not find it, but it is probably a ring."));
        var response = await Ask(llm, "Find the nothing-matches-this item");

        Assert.False(response.Refused);
        Assert.Equal("No data found.", response.Answer);
        Assert.Empty(response.Sources);
    }

    [Fact]
    public async Task SecondToolFindsData_SoTheModelAnswerIsUsed()
    {
        var llm = new FakeLlm(Call("search_items", new { query = "nothing" }), Call("list_categories"),
            Say("There are 8 categories.", ("list_categories", "1")));
        var response = await Ask(llm, "How many categories?");
        Assert.False(response.Refused);
        Assert.Equal("There are 8 categories.", response.Answer);
    }

    [Fact]
    public async Task ToolError_IsNotReportedAsNoData()
    {
        var llm = new FakeLlm(Call("get_item_price_breakdown", new { itemId = 0 }), Say("The tool could not run, so I cannot answer."));
        var response = await Ask(llm, "Price of item 0?");
        Assert.False(response.Refused);
        Assert.Contains("cannot answer", response.Answer);
    }

    [Fact]
    public async Task UnknownTool_CannotGroundAnAnswer()
    {
        var llm = new FakeLlm(Call("drop_table", new { name = "items" }), Say("Done, 5 rows removed.", ("drop_table", "1")));
        AssertRefused(await Ask(llm, "Delete items"));
    }

    [Fact]
    public async Task MoreThanFiveToolCalls_AreRefused_AndTheExtraCallIsNotRun()
    {
        var llm = new FakeLlm(Enumerable.Range(0, ChatOrchestrator.MaxToolCalls + 1).Select(_ => Call("list_categories")).ToArray());
        AssertRefused(await Ask(llm, "Keep looking things up"));
        Assert.Equal(ChatOrchestrator.MaxToolCalls + 1, llm.Calls.Count);
    }

    [Fact]
    public async Task FiveToolCalls_AreAllowed()
    {
        var script = Enumerable.Range(0, ChatOrchestrator.MaxToolCalls).Select(_ => Call("list_categories")).Append(Say("There are 8 categories.", ("list_categories", "1"))).ToArray();
        Assert.False((await Ask(new FakeLlm(script), "How many categories?")).Refused);
    }

    [Fact]
    public async Task ModelCallCap_StopsALoopOfEmptyReplies()
    {
        // Empty replies are retried once as bad JSON, then refused. The cap is a second safety net.
        var llm = new FakeLlm(Call("list_categories"), new LlmResponse(null, []), new LlmResponse(null, []));
        AssertRefused(await Ask(llm, "List categories"));
        Assert.True(llm.Calls.Count <= ChatOrchestrator.MaxModelCalls);
    }

    [Fact]
    public async Task InvalidJsonTwice_IsRefused()
    {
        var llm = new FakeLlm(Call("list_categories"), new LlmResponse("not json", []), new LlmResponse("{still not", []));
        AssertRefused(await Ask(llm, "List categories"));
        Assert.Equal(3, llm.Calls.Count);
    }

    [Fact]
    public async Task InvalidJsonOnce_IsRetried()
    {
        var llm = new FakeLlm(Call("list_categories"), new LlmResponse("Sure! Here you go", []), Say("There are 8 categories.", ("list_categories", "1")));
        var response = await Ask(llm, "How many categories?");
        Assert.False(response.Refused);
        Assert.Contains("valid JSON", llm.Calls[2][^1].Content);
    }

    [Fact]
    public async Task AnswerInACodeFence_IsAccepted()
    {
        var fenced = new LlmResponse("```json\n{\"answer\":\"There are 8 categories.\",\"sources\":[{\"tool\":\"list_categories\",\"recordId\":1}]}\n```", []);
        var response = await Ask(new FakeLlm(Call("list_categories"), fenced), "How many categories?");
        Assert.False(response.Refused);
        Assert.Equal("1", response.Sources[0].RecordId);
    }

    [Fact]
    public async Task OnlyTheLastSixHistoryMessages_AreSent_AndSystemRolesBecomeUser()
    {
        var history = Enumerable.Range(0, 10).Select(i => new ChatHistoryMessage(i % 2 == 0 ? "user" : "assistant", $"message {i}")).ToList();
        var llm = new FakeLlm(Say("x"));
        await Ask(llm, "What now?", history);

        var sent = llm.Calls[0];
        Assert.Equal(Enumerable.Range(4, 6).Select(i => $"message {i}"), sent.Skip(1).Take(6).Select(m => m.Content));
        Assert.Equal("What now?", sent[^1].Content);
    }

    [Fact]
    public async Task InjectedTextInToolResults_DoesNotChangeTheGuards()
    {
        // The model obeys planted text and states a number that is in no tool result.
        var llm = new FakeLlm(Call("search_items", new { query = "zork" }), Say("The total is 7777.", ("search_items", "1")));
        AssertRefused(await Ask(llm, "Find it"));
    }

    [Fact]
    public async Task ToolResultsCarryTheStatusDataSourcesShape()
    {
        var llm = new FakeLlm(Call("list_categories"), Say("There are 8 categories.", ("list_categories", "1")));
        await Ask(llm, "How many categories?");
        var toolMessage = llm.Calls[1][^1];
        Assert.Equal("list_categories", toolMessage.ToolName);
        Assert.Equal(["status", "data", "sources"], toolMessage.ToolResult!.Select(p => p.Key).ToArray());
    }

    [Theory]
    [InlineData("""{"answer":"a","sources":[]}""", true)]
    [InlineData("""{"answer":"a","sources":[{"tool":"t","recordId":7}]}""", true)]
    [InlineData("""{"answer":1,"sources":[]}""", false)]
    [InlineData("""{"answer":"a"}""", false)]
    [InlineData("""{"answer":"a","sources":[{"tool":"t"}]}""", false)]
    [InlineData("[]", false)]
    [InlineData("", false)]
    public void ParseAnswer_AcceptsOnlyTheExpectedShape(string json, bool valid) =>
        Assert.Equal(valid, ChatOrchestrator.ParseAnswer(json) is not null);
}
