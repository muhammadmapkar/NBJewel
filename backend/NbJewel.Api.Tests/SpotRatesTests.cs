// Role: Verifies provider units, cache freshness and safe outage behavior using a fake clock and transport.
using System.Net;
using Microsoft.Extensions.Configuration;
using NbJewel.Api.Storefront;
namespace NbJewel.Api.Tests;
public sealed class SpotRatesTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Handler(Clock clock) : HttpMessageHandler
    {
        public int Calls; public bool Fail;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal("test-key", request.Headers.GetValues("x-access-token").Single());
            var symbol = request.RequestUri!.Segments[^2].Trim('/');
            return Task.FromResult(Fail ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent($$"""{"metal":"{{symbol}}","currency":"USD","price":3110.34768,"timestamp":{{clock.Now.ToUnixTimeSeconds()}}}""") });
        }
    }
    [Fact]
    public async Task Quotes_convert_ounces_to_grams_and_failed_refresh_stays_stale_in_cache()
    {
        var clock = new Clock(); var handler = new Handler(clock);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["METAL_RATES_API_KEY"] = "test-key" }).Build();
        var service = new SpotRates(new HttpClient(handler), config, clock);
        var first = await service.ReadAsync(default);
        Assert.Equal("live", first.Status); Assert.Equal(3, first.Quotes.Count); Assert.All(first.Quotes, q => Assert.Equal(100m, q.PerGram));
        await service.ReadAsync(default); Assert.Equal(3, handler.Calls);
        clock.Now = clock.Now.AddMinutes(6); handler.Fail = true;
        Assert.Equal("stale", (await service.ReadAsync(default)).Status);
        Assert.Equal("stale", (await service.ReadAsync(default)).Status); Assert.Equal(4, handler.Calls);
    }
}
