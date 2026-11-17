// Role: Fails closed on unsafe public demo hosting before SQL access and preserves the existing local launcher mode.
namespace NbJewel.Api.Platform;

public static class PublicDemoSetup
{
    public static Task RequireHttps(HttpContext context, RequestDelegate next)
    {
        // The preceding trusted proxy middleware alone may establish HTTPS. Never infer it from an arbitrary header.
        if (context.Request.IsHttps || (HttpMethods.IsGet(context.Request.Method) &&
            (context.Request.Path == "/health/live" || context.Request.Path == "/health/ready/core"))) return next(context);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return Task.CompletedTask;
    }

    public static readonly string[] StorageKeys = ["ITEM_ASSET_STORAGE_PATH", "BUSINESS_STORAGE_PATH", "JEWELRY_STORAGE_PATH"];
    public static bool Enabled(IConfiguration configuration) => configuration.GetValue<bool>("PublicDemo:Enabled");
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!Enabled(configuration)) return;
        if (!environment.IsProduction() || !string.Equals(configuration["Repository:Provider"], "Sql", StringComparison.OrdinalIgnoreCase) ||
            configuration.GetValue<bool>("SampleIdentity:Enabled") || configuration["urls"] != "http://127.0.0.1:5157" ||
            configuration["TRUSTED_PROXIES"] != "127.0.0.1" || configuration.GetSection("Kestrel:Endpoints").GetChildren().Any() ||
            configuration.GetValue<bool>("FORWARDEDHEADERS_ENABLED") ||
            configuration["AllowedHosts"] != "nb-jewel.vercel.app;localhost;127.0.0.1" ||
            configuration["APP_PUBLIC_ORIGIN"] != "https://nb-jewel.vercel.app" ||
            configuration["CORS_ORIGINS"] != "https://nb-jewel.vercel.app")
            throw new InvalidOperationException("Public demo requires the fixed Production SQL loopback launcher settings.");
        foreach (var key in new[] { "NBJEWEL_SQL_CONNECTION", "NBJEWEL_IDENTITY_CONNECTION", "AI_PLATFORM_CONNECTION" })
            if (string.IsNullOrWhiteSpace(configuration[key])) throw new InvalidOperationException("Public demo requires all backend SQL connections.");
        byte[] secret;
        try { secret = Convert.FromBase64String(configuration["AI_DATA_PROTECTION_SECRET"] ?? ""); }
        catch (FormatException) { throw new InvalidOperationException("Public demo requires a stable 32-byte protection secret."); }
        if (secret.Length != 32) throw new InvalidOperationException("Public demo requires a stable 32-byte protection secret.");
        foreach (var key in StorageKeys)
            if (configuration[key] is not { Length: > 0 } path || !Path.IsPathFullyQualified(path) || !Directory.Exists(path))
                throw new InvalidOperationException("Public demo requires existing absolute durable storage directories.");
    }
}
