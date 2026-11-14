// Role: Fetches bounded, cached GoldAPI reference quotes; never substitutes market quotes for authoritative ERP selling rates.
using System.Net.Http.Json;
using System.Text.Json;
namespace NbJewel.Api.Storefront;
public sealed record SpotQuote(string Metal, decimal PerGram, string Currency, DateTimeOffset AsOf);
public sealed record SpotSnapshot(string Status, string Provider, IReadOnlyList<SpotQuote> Quotes);
public sealed class SpotRates(HttpClient http, IConfiguration configuration, TimeProvider? time = null)
{
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1, 1);
    private SpotSnapshot cached = new("unavailable", "GoldAPI", []);
    private DateTimeOffset next;
    public async Task<SpotSnapshot> ReadAsync(CancellationToken ct)
    {
        var key = configuration["METAL_RATES_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) return new("unconfigured", "GoldAPI", []);
        await gate.WaitAsync(ct);
        try {
            if (clock.GetUtcNow() < next) return Current(cached);
            next = clock.GetUtcNow().AddMinutes(5);
            var quotes = new List<SpotQuote>();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try {
                foreach (var (symbol, name) in new[] { ("XAU", "Gold"), ("XAG", "Silver"), ("XPT", "Platinum") }) {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.goldapi.io/api/" + symbol + "/USD");
                    request.Headers.Add("x-access-token", key);
                    using var response = await http.SendAsync(request, deadline.Token); response.EnsureSuccessStatusCode();
                    var text = await response.Content.ReadAsStringAsync(deadline.Token);
                    if (text.Length > 65536) throw new JsonException();
                    using var data = JsonDocument.Parse(text); var root = data.RootElement;
                    if (root.GetProperty("metal").GetString() != symbol || root.GetProperty("currency").GetString() != "USD") throw new JsonException();
                    var price = root.GetProperty("price").GetDecimal(); var at = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("timestamp").GetInt64());
                    if (price <= 0 || price > 1000000 || at > clock.GetUtcNow().AddMinutes(1)) throw new JsonException();
                    quotes.Add(new(name, decimal.Round(price / 31.1034768m, 4), "USD", at));
                }
                cached = new("live", "GoldAPI", quotes);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException or OperationCanceledException) {
                cached = cached.Quotes.Count > 0 ? cached with { Status = "stale" } : new("unavailable", "GoldAPI", []);
                return cached;
            }
            return Current(cached);
        } finally { gate.Release(); }
    }
    private SpotSnapshot Current(SpotSnapshot snapshot) => snapshot.Quotes.Any(x => x.AsOf < clock.GetUtcNow().AddMinutes(-15)) ? snapshot with { Status = "stale" } : snapshot;
}
