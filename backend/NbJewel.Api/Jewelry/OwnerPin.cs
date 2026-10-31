// Role: Checks the owner PIN sent in the X-Owner-Pin header against the OWNER_PIN backend
// environment variable. The PIN is never stored in the frontend or the repo.
// Assumed, replace with real auth later: one shared PIN is a demo gate, not real security.

using System.Security.Cryptography;
using System.Text;

namespace NbJewel.Api.Jewelry;

public enum PinResult { Ok, NotConfigured, Wrong }

public sealed class OwnerPin(IConfiguration configuration)
{
    public const string HeaderName = "X-Owner-Pin";
    // Rate limit policy name for the owner endpoints (set up in Program.cs).
    public const string RateLimitPolicy = "owner";

    public PinResult Check(string? suppliedPin)
    {
        var expected = configuration["OWNER_PIN"];
        if (string.IsNullOrWhiteSpace(expected)) return PinResult.NotConfigured;
        if (string.IsNullOrEmpty(suppliedPin)) return PinResult.Wrong;
        // Fixed time comparison, so the response time does not leak how much of the PIN matched.
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(expected.Trim()));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedPin.Trim()));
        return CryptographicOperations.FixedTimeEquals(a, b) ? PinResult.Ok : PinResult.Wrong;
    }
}
