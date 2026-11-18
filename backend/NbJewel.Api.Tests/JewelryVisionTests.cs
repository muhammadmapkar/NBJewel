// Role: Verifies Vision key isolation, structured response validation and safe provider failures without network calls.
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using NbJewel.Api.Jewelry;
using Xunit;
namespace NbJewel.Api.Tests;
public class JewelryVisionTests
{
    private sealed class Handler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Key;
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Key = request.Headers.GetValues("x-goog-api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private static IConfiguration Config(params (string, string)[] entries) => new ConfigurationBuilder().AddInMemoryCollection(entries.ToDictionary(x => x.Item1, x => (string?)x.Item2)).Build();
    [Fact] public void Dedicated_key_overrides_shared_and_blank_falls_back()
    {
        Assert.Equal("erp-test", JewelryVisionOptions.Read(Config(("GEMINI_ERP_API_KEY", "erp-test"), ("GEMINI_API_KEY", "shared-test"))).Key);
        Assert.Equal("shared-test", JewelryVisionOptions.Read(Config(("GEMINI_ERP_API_KEY", " "), ("GEMINI_API_KEY", "shared-test"))).Key);
        Assert.Equal("", JewelryVisionOptions.Read(Config(("GEMINI_CHATBOT_API_KEY", "chat-only"))).Key);
    }
    [Fact] public async Task Structured_copy_uses_image_and_dedicated_header()
    {
        var handler = new Handler("""{"candidates":[{"content":{"parts":[{"text":"{\"description\":\"A gold-tone floral motif for festive occasions.\",\"collection\":\"Temple\"}"}]}}]}""");
        var client = new JewelryVisionClient(new HttpClient(handler), new("erp-test", "test-model"));
        var result = await client.AnalyzeAsync([1, 2, 3], default);
        Assert.Equal("Temple", result.Collection); Assert.Equal("erp-test", handler.Key);
        Assert.Contains("inlineData", handler.Body); Assert.Contains("AQID", handler.Body);
    }
    [Fact]
    public void A_necklace_trim_suggestion_is_accepted_with_copy()
    {
        var suggestion = JewelryVisionClient.ParseSuggestion("""{"description":"A floral necklace.","collection":"Temple","cameraTrimPercent":22}""");
        Assert.Equal(22, suggestion.CameraTrimPercent);
    }
    [Theory]
    [InlineData("-1")]
    [InlineData("50")]
    [InlineData("20.5")]
    [InlineData("\"28\"")]
    public void Invalid_trim_keeps_copy_and_discards_the_cut_line(string trim)
    {
        var suggestion = JewelryVisionClient.ParseSuggestion($$"""{"description":"A floral necklace.","collection":"Temple","cameraTrimPercent":{{trim}}}""");
        Assert.Equal("Temple", suggestion.Collection);
        Assert.Null(suggestion.CameraTrimPercent);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"description\":\"copy\",\"collection\":\"Unknown\"}")]
    [InlineData("{\"description\":\"<script>bad</script>\",\"collection\":\"Temple\"}")]
    public void Invalid_suggestions_are_rejected(string json) => Assert.Throws<VisionUnavailableException>(() => JewelryVisionClient.ParseSuggestion(json));
    [Fact] public async Task Missing_key_never_calls_provider()
    {
        var handler = new Handler("{}");
        await Assert.ThrowsAsync<VisionUnavailableException>(() => new JewelryVisionClient(new(handler), new("", "test")).AnalyzeAsync([1], default));
        Assert.Null(handler.Key);
    }
    [Fact] public async Task Rate_limit_is_safe_and_not_retried()
    {
        var handler = new Handler("private upstream error", HttpStatusCode.TooManyRequests);
        var error = await Assert.ThrowsAsync<VisionUnavailableException>(() => new JewelryVisionClient(new(handler), new("test", "test")).AnalyzeAsync([1], default));
        Assert.Equal(429, error.Status); Assert.DoesNotContain("private", error.Message);
    }
}
