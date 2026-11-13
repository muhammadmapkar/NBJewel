// Role: Reads TRUSTED_PROXIES and enables forwarded headers only for those proxies, so the real client address is never taken from an untrusted peer.
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace NbJewel.Api.Platform;

public static class TrustedProxySetup
{
    // TRUSTED_PROXIES is a comma separated list of proxy addresses or CIDR ranges, for example "10.0.0.5,172.16.0.0/12".
    // Empty means no proxy is trusted: forwarded headers stay disabled and the socket address is used as is.
    // Only the last forwarded hop is read (ForwardLimit 1), which is the entry the trusted proxy itself appended.
    // The value is read when the middleware is built, and an invalid entry stops startup.
    public static void Configure(IServiceCollection services) =>
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var entries = (configuration["TRUSTED_PROXIES"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (entries.Length == 0) return;
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var entry in entries)
            {
                if (entry.Contains('/') && System.Net.IPNetwork.TryParse(entry, out var network)) options.KnownIPNetworks.Add(network);
                else if (!entry.Contains('/') && IPAddress.TryParse(entry, out var address)) options.KnownProxies.Add(address);
                else throw new InvalidOperationException("TRUSTED_PROXIES contains an entry that is not an IP address or CIDR range.");
            }
        });
}
