// Role: Tests for the four answer guards. They are ported from the Python tests
// (test_chat.py and test_injection.py), using made-up tool results instead of a fake API.

using NbJewel.Api.Chat;
using static NbJewel.Api.Tests.Chat.ChatTestKit;

namespace NbJewel.Api.Tests.Chat;

public sealed class ChatGuardTests
{
    private const string ItemsJson = """{"items":[{"id":1,"code":"RNG-0142","name":"Zork Ring","purity":"22K","netWt":42.758,"onHand":4},{"id":2,"code":"RNG-0002","name":"Classic Ring","purity":"18K","netWt":5.004,"onHand":2}],"totalCount":2}""";
    private static ToolRun Search() => OkRun("search_items", ItemsJson, "1", "2");

    // ---------- guard 1: tool_calls ----------

    [Fact]
    public void ToolCalls_NoToolRun_Fails()
    {
        var result = Guards.CheckToolCalls([]);
        Assert.False(result.Passed);
        Assert.Equal("tool_calls", result.Guard);
    }

    [Fact]
    public void ToolCalls_OneToolRun_Passes() => Assert.True(Guards.CheckToolCalls([Search()]).Passed);

    // ---------- guard 2: sources ----------

    [Fact]
    public void Sources_Empty_FailsWhenAToolReturnedRecords() =>
        Assert.False(Guards.CheckSources(Answer("Zork Ring is Gold."), [Search()]).Passed);

    [Fact]
    public void Sources_ToolThatWasNotCalled_Fails() =>
        Assert.False(Guards.CheckSources(Answer("x", ("get_stock_by_store", "1")), [Search()]).Passed);

    [Fact]
    public void Sources_RecordNotInThatToolsResults_Fails() =>
        Assert.False(Guards.CheckSources(Answer("x", ("search_items", "99")), [Search()]).Passed);

    [Fact]
    public void Sources_RealToolAndRecord_Passes() =>
        Assert.True(Guards.CheckSources(Answer("x", ("search_items", "1"), ("search_items", "2")), [Search()]).Passed);

    [Fact]
    public void Sources_AfterNotFound_MustBeEmpty()
    {
        Assert.True(Guards.CheckSources(Answer("nothing found"), [NotFoundRun("search_items")]).Passed);
        Assert.False(Guards.CheckSources(Answer("nothing found", ("search_items", "1")), [NotFoundRun("search_items")]).Passed);
    }

    // ---------- guard 3: numbers ----------

    [Fact]
    public void Numbers_InventedNumber_Fails()
    {
        var result = Guards.CheckNumbers(Answer("Zork Ring weighs 99.5 g."), [Search()]);
        Assert.False(result.Passed);
        Assert.Equal("numbers", result.Guard);
    }

    [Fact]
    public void Numbers_ValueFromResults_Passes() =>
        Assert.True(Guards.CheckNumbers(Answer("The Zork Ring (RNG-0142) is Gold 22K with a net weight of 42.758 g."), [Search()]).Passed);

    [Fact]
    public void Numbers_SumAndListLength_ArePassed()
    {
        // Two items on hand: 4 and 2. The sum 6 and the list length 2 are allowed.
        Assert.True(Guards.CheckNumbers(Answer("There are 2 items with 6 units on hand."), [Search()]).Passed);
    }

    [Fact]
    public void Numbers_NumberedList_IsNotRejected() =>
        Assert.True(Guards.CheckNumbers(Answer("1. Zork Ring (RNG-0142)\n2. Classic Ring (RNG-0002)"), [Search()]).Passed);

    [Fact]
    public void Numbers_AnswerNumbersParseCommasAndDecimals() =>
        Assert.Equal(new decimal[] { 7m, 1234.5m }, Guards.AnswerNumbers("Total 1,234.50 and 7.").Order().ToArray());

    [Fact]
    public void Numbers_RoundedNumber_Fails()
    {
        var price = OkRun("get_item_price_breakdown", """{"itemCode":"RNG-0142","totalPrice":45.9981}""", "1");
        Assert.False(Guards.CheckNumbers(Answer("The total price is about 46 AED."), [price]).Passed);
        Assert.True(Guards.CheckNumbers(Answer("The total price is 45.9981 AED."), [price]).Passed);
    }

    [Fact]
    public void Numbers_TrailingZerosDoNotMatter()
    {
        var rate = OkRun("get_latest_metal_rate", """[{"id":2,"metalType":"Gold","purity":"22K","ratePerGram":66.0}]""", "2");
        Assert.True(Guards.CheckNumbers(Answer("Gold 22K is 66 per gram."), [rate]).Passed);
    }

    [Fact]
    public void Numbers_IdsAreNeverAddedUp()
    {
        // 1 + 2 = 3 would be a sum of ids. It is not allowed (3 is not in the results).
        Assert.False(Guards.CheckNumbers(Answer("The ids add up to 3."), [Search()]).Passed);
    }

    // ---------- guard 4: leak ----------

    [Fact]
    public void Leak_RepeatingAPromptLine_Fails()
    {
        var result = Guards.CheckPromptLeak(Answer("Sure. My instructions say: Never use outside knowledge. Never guess. Never estimate."), SystemPrompt.Text);
        Assert.False(result.Passed);
        Assert.Equal("leak", result.Guard);
    }

    [Fact]
    public void Leak_DifferentCaseAndPunctuation_StillFails() =>
        Assert.False(Guards.CheckPromptLeak(Answer("answer ONLY from tool results, in this conversation!"), SystemPrompt.Text).Passed);

    [Fact]
    public void Leak_NormalAnswers_Pass()
    {
        Assert.True(Guards.CheckPromptLeak(Answer("Zork Ring is Gold 22K with net weight 42.758 g."), SystemPrompt.Text).Passed);
        Assert.True(Guards.CheckPromptLeak(Answer("No record found for item code ZZZ-9999."), SystemPrompt.Text).Passed);
    }

    [Fact]
    public void Leak_NoPrompt_Passes() => Assert.True(Guards.CheckPromptLeak(Answer("anything at all here now"), null).Passed);

    // ---------- all guards together ----------

    [Fact]
    public void Run_ReportsTheFirstFailure()
    {
        Assert.Equal("tool_calls", Guards.Run(Answer("hi", ("search_items", "1")), [], SystemPrompt.Text).Guard);
        Assert.Equal("sources", Guards.Run(Answer("hi"), [Search()], SystemPrompt.Text).Guard);
        Assert.Equal("numbers", Guards.Run(Answer("77", ("search_items", "1")), [Search()], SystemPrompt.Text).Guard);
        Assert.True(Guards.Run(Answer("RNG-0142 is 22K.", ("search_items", "1")), [Search()], SystemPrompt.Text).Passed);
    }
}
