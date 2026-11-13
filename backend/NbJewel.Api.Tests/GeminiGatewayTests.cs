// Role: Verifies trusted Gemini settings, response metadata, and bounded retries using an HTTP fake.
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NbJewel.Api.Chat;

namespace NbJewel.Api.Tests;

public sealed class GeminiGatewayTests
{
    [Fact]
    public async Task ModelAndOutputBudgetReachRestRequestAndUsageIsParsed()
    {
        var handler = new RecordingHandler((_, _) => Response(HttpStatusCode.OK,
            "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"text\":\"answer\"}]}}],\"usageMetadata\":{\"promptTokenCount\":12,\"candidatesTokenCount\":4,\"totalTokenCount\":16}}"));
        var response = await Client(handler).GenerateAsync([new("user", "hello")], [], 0.1,
            new LlmRequestSettings("trusted-model", 1024), default);
        Assert.Contains("trusted-model:generateContent", handler.Uris.Single());
        Assert.Equal(1024, handler.Bodies.Single()["generationConfig"]!["maxOutputTokens"]!.GetValue<int>());
        Assert.Equal(new LlmUsage(12, 4, 16), response.Usage);
        Assert.Equal("STOP", response.FinishReason);
    }

    [Fact]
    public async Task AbsentUsageStaysNull()
    {
        var response = await Client(new RecordingHandler((_, _) => Response(HttpStatusCode.OK, "{}")))
            .GenerateAsync([], [], 0.1, default);
        Assert.Null(response.Usage);
        Assert.Null(response.FinishReason);
    }

    [Fact]
    public async Task ThinkingFallbackCannotMultiplyRetryAndDoesNotBleedBetweenModels()
    {
        var handler = new RecordingHandler((count, _) => Response(count == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
            count == 1 ? "unsupported thinking" : "{}"));
        var client = Client(handler);
        await client.GenerateAsync([], [], 0.1, new LlmRequestSettings("first", 2048), default);
        await client.GenerateAsync([], [], 0.1, new LlmRequestSettings("second", 2048), default);
        Assert.Equal(3, handler.Bodies.Count);
        Assert.NotNull(handler.Bodies[0]["generationConfig"]!["thinkingConfig"]);
        Assert.Null(handler.Bodies[1]["generationConfig"]!["thinkingConfig"]);
        Assert.NotNull(handler.Bodies[2]["generationConfig"]!["thinkingConfig"]);
    }

    [Fact]
    public async Task TransientResponsesRetryOnlyOnce()
    {
        var handler = new RecordingHandler((_, _) => Response(HttpStatusCode.ServiceUnavailable, "busy"));
        await Assert.ThrowsAsync<LlmException>(() => Client(handler).GenerateAsync([], [], 0.1, default));
        Assert.Equal(2, handler.Bodies.Count);
    }

    [Fact]
    public async Task ThinkingFallbackThenTransientFailureDoesNotGetAThirdAttempt()
    {
        var handler = new RecordingHandler((count, _) => Response(count == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.ServiceUnavailable,
            count == 1 ? "thinking unsupported" : "busy"));
        await Assert.ThrowsAsync<LlmException>(() => Client(handler).GenerateAsync([], [], 0.1, default));
        Assert.Equal(2, handler.Bodies.Count);
    }

    [Fact]
    public async Task TwoGenerationsShareOneRetryAndNextQuestionGetsFreshBudget()
    {
        var handler = new RecordingHandler((count, _) => Response(count is 1 or 3 or 4 ? HttpStatusCode.BadRequest : HttpStatusCode.OK, count is 1 or 3 or 4 ? "thinking unsupported" : "{}"));
        var client = Client(handler);
        var budget = new ModelRetryBudget();
        var settings = new LlmRequestSettings("model", 2048, budget);
        await client.GenerateAsync([], [], 0.1, settings, default);
        await Assert.ThrowsAsync<LlmException>(() => client.GenerateAsync([], [], 0.1, settings, default));
        await client.GenerateAsync([], [], 0.1, new LlmRequestSettings("model", 2048, new ModelRetryBudget()), default);
        Assert.Equal(5, handler.Bodies.Count);
    }

    [Fact]
    public async Task ThinkingFallbackPersistsAcrossGenerationsOnlyForSameQuestion()
    {
        var handler = new RecordingHandler((count, _) => Response(count == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
            count == 1 ? "thinking unsupported" : "{}"));
        var client = Client(handler);
        var settings = new LlmRequestSettings("model", 2048, new ModelRetryBudget());
        await client.GenerateAsync([], [], 0.1, settings, default);
        await client.GenerateAsync([], [], 0.1, settings, default);
        await client.GenerateAsync([], [], 0.1, new LlmRequestSettings("model", 2048, new ModelRetryBudget()), default);
        Assert.NotNull(handler.Bodies[0]["generationConfig"]!["thinkingConfig"]);
        Assert.Null(handler.Bodies[1]["generationConfig"]!["thinkingConfig"]);
        Assert.Null(handler.Bodies[2]["generationConfig"]!["thinkingConfig"]);
        Assert.NotNull(handler.Bodies[3]["generationConfig"]!["thinkingConfig"]);
    }

    private static GeminiLlmClient Client(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = GeminiLlmClient.BaseAddress }, new GeminiOptions("test-key", "default", "low"), NullLogger<GeminiLlmClient>.Instance);

    private static Task<HttpResponseMessage> Response(HttpStatusCode code, string body)
        => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });

    private sealed class RecordingHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<JsonObject> Bodies { get; } = [];
        public List<string> Uris { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!.AbsoluteUri);
            Bodies.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject());
            return await respond(Bodies.Count, cancellationToken);
        }
    }
}
