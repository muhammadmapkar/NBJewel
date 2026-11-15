// Role: Defines the assumed business registry and validates immutable slugs without SQL schema changes.
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NbJewel.Api.Businesses;

public sealed record Business(string Id, string Slug, string Name, string Kind, DateTimeOffset CreatedAt);

public sealed class BusinessRegistry
{
    public const string DemoSlug = "nb-jewel-demo";
    public static readonly Business Demo = new(DemoSlug, DemoSlug, "NB Jewel", "retailer", DateTimeOffset.UnixEpoch);
    private readonly object sync = new();
    private readonly List<Business> businesses;
    private readonly string registrationPath;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public BusinessRegistry(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var root = configuration["BUSINESS_STORAGE_PATH"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "businesses");
        registrationPath = Path.Combine(Path.GetFullPath(root), "registry.json");
        var seed = configuration["BUSINESSES_PATH"];
        // SQL's default registry is the existing business only. An explicit registry is never filtered.
        var sql = string.Equals(configuration["Repository:Provider"], "Sql", StringComparison.OrdinalIgnoreCase);
        seed ??= sql ? null : Path.Combine(AppContext.BaseDirectory, "sample-data", "businesses.json");
        businesses = seed is null ? [Demo] : JsonSerializer.Deserialize<List<Business>>(File.ReadAllText(seed), Json) ?? throw new InvalidOperationException("Invalid business registry.");
        if (File.Exists(registrationPath)) businesses.AddRange(JsonSerializer.Deserialize<List<Business>>(File.ReadAllText(registrationPath), Json) ?? throw new InvalidOperationException("Invalid business registrations."));
        if (!businesses.Any(x => x.Slug == DemoSlug) || businesses.Any(x => !ValidSlug(x.Slug) || x.Id != x.Slug || string.IsNullOrWhiteSpace(x.Name) || x.Kind is not ("retailer" or "wholesaler")) || businesses.Select(x => x.Slug).Distinct().Count() != businesses.Count)
            throw new InvalidOperationException("Invalid business registry.");
    }
    public static bool ValidSlug(string slug) => Regex.IsMatch(slug, "^[a-z0-9-]{3,40}$", RegexOptions.CultureInvariant) && slug is not ("preview" or "api" or "assets");
    public IReadOnlyList<Business> All { get { lock (sync) return businesses.ToArray(); } }
    public Business? Find(string slug) { lock (sync) return businesses.SingleOrDefault(x => x.Slug == slug); }
    public Business Add(string slug, string name, string kind)
    {
        lock (sync)
        {
            if (!ValidSlug(slug) || string.IsNullOrWhiteSpace(name) || name.Length > 120 || kind is not ("retailer" or "wholesaler") || businesses.Any(x => x.Slug == slug))
                throw new ArgumentException("Invalid or duplicate business.");
            var business = new Business(slug, slug, name.Trim(), kind, DateTimeOffset.UtcNow);
            Directory.CreateDirectory(Path.GetDirectoryName(registrationPath)!);
            // Persist only locally provisioned additions, never rewrite the seed fixture.
            var additions = File.Exists(registrationPath) ? JsonSerializer.Deserialize<List<Business>>(File.ReadAllText(registrationPath), Json)! : [];
            additions.Add(business);
            var temporary = registrationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(additions, Json));
            File.Move(temporary, registrationPath, true);
            businesses.Add(business);
            return business;
        }
    }
}
