// Role: Tests the chat forwarder with a fake AI service, covering a normal
// answer, bad questions, and the 503 response when the AI service fails.

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NbJewel.Api.Controllers;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class ChatControllerTests
{
    private static ChatController Controller(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
        new(new StubFactory(handler), NullLogger<ChatController>.Instance);

    private static void AssertUnavailable(ActionResult<ChatResponse> result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, objectResult.StatusCode);
        Assert.Equal("Assistant unavailable. Retry.", Assert.IsType<ErrorMessage>(objectResult.Value).Message);
    }

    [Fact]
    public async Task ForwardsQuestionAndReturnsAnswer()
    {
        string? sentBody = null;
        var controller = Controller(async request =>
        {
            Assert.Equal("/chat", request.RequestUri!.AbsolutePath);
            sentBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { answer = "Zork Ring is Gold 22K.", sources = new[] { new { tool = "search_items", recordId = "1" } }, refused = false })
            };
        });

        var result = await controller.Ask(new ChatRequest("Find the Zork Ring", [new("user", "hi")]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ChatResponse>(ok.Value);
        Assert.Equal("Zork Ring is Gold 22K.", body.Answer);
        Assert.Equal("search_items", body.Sources[0].Tool);
        Assert.Equal("1", body.Sources[0].RecordId);
        Assert.False(body.Refused);
        Assert.Contains("\"question\":\"Find the Zork Ring\"", sentBody);
        Assert.Contains("\"history\":[{\"role\":\"user\",\"content\":\"hi\"}]", sentBody);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankQuestionIsBadRequest(string question)
    {
        var controller = Controller(_ => throw new InvalidOperationException("must not be called"));
        var result = await controller.Ask(new ChatRequest(question, null), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task QuestionOver500CharactersIsBadRequest()
    {
        var controller = Controller(_ => throw new InvalidOperationException("must not be called"));
        var result = await controller.Ask(new ChatRequest(new string('x', 501), null), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AiServiceDownReturns503()
    {
        var controller = Controller(_ => throw new HttpRequestException("connection refused"));
        AssertUnavailable(await controller.Ask(new ChatRequest("hi", null), CancellationToken.None));
    }

    [Fact]
    public async Task AiServiceTimeoutReturns503()
    {
        var controller = Controller(_ => throw new TaskCanceledException("timed out", new TimeoutException()));
        AssertUnavailable(await controller.Ask(new ChatRequest("hi", null), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AiServiceErrorStatusReturns503(HttpStatusCode status)
    {
        var controller = Controller(_ => Task.FromResult(new HttpResponseMessage(status)));
        AssertUnavailable(await controller.Ask(new ChatRequest("hi", null), CancellationToken.None));
    }

    [Fact]
    public async Task UnreadableAiResponseReturns503()
    {
        var controller = Controller(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") }));
        AssertUnavailable(await controller.Ask(new ChatRequest("hi", null), CancellationToken.None));
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsUnavailable()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var controller = Controller(_ => throw new TaskCanceledException());
        await Assert.ThrowsAsync<TaskCanceledException>(() => controller.Ask(new ChatRequest("hi", null), cancelled.Token));
    }

    private sealed class StubFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(handler)) { BaseAddress = new Uri("http://ai.test") };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
