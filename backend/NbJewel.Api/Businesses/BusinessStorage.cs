// Role: Resolves assumed per-business Sample partitions and pins every existing SQL adapter to the sole demo business.
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Businesses;

public sealed record DefaultCutoutOptions(ItemCutoutOptions Value);
public sealed record BusinessPartition(IItemRepository Items, IItemImageAssetRepository Assets, ItemCutoutOptions Cutout, FileProductPublicationStore Publications, ItemCutoutPipeline Pipeline);
public sealed class BusinessStorage(IConfiguration configuration, IWebHostEnvironment environment, BusinessRegistry registry, DefaultCutoutOptions defaults)
{
    private readonly ConcurrentDictionary<string, Lazy<BusinessPartition>> partitions = new();
    private readonly string databasePrefix = "business-knowledge-" + Guid.NewGuid().ToString("N");
    public bool Sql => string.Equals(configuration["Repository:Provider"], "Sql", StringComparison.OrdinalIgnoreCase);
    public void Validate()
    {
        var provider = configuration["Repository:Provider"];
        if (!string.IsNullOrWhiteSpace(provider) && !string.Equals(provider, "Sample", StringComparison.OrdinalIgnoreCase) && !Sql) throw new InvalidOperationException("Unknown repository provider.");
        if (Sql && (registry.All.Count != 1 || registry.All[0].Slug != BusinessRegistry.DemoSlug)) throw new InvalidOperationException("SQL mode supports exactly nb-jewel-demo. Multi-business requires Sample mode.");
        if (Sql && string.IsNullOrWhiteSpace(configuration["NBJEWEL_SQL_CONNECTION"])) throw new InvalidOperationException("SQL repository connection is not configured.");
    }
    public string Root(Business business)
    {
        if (registry.Find(business.Slug) != business) throw new InvalidOperationException("Unknown business partition.");
        var root = configuration["BUSINESS_STORAGE_PATH"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "businesses");
        return Path.Combine(Path.GetFullPath(root), business.Slug);
    }
    public string SampleDirectory(Business business)
    {
        var root = Directory.Exists(Path.Combine(environment.ContentRootPath, "sample-data")) ? Path.Combine(environment.ContentRootPath, "sample-data") : Path.Combine(AppContext.BaseDirectory, "sample-data");
        return business.Slug == BusinessRegistry.DemoSlug ? root : Path.Combine(root, "businesses", business.Slug);
    }
    public string? DirectoryPath(Business business) => business.Slug == BusinessRegistry.DemoSlug ? configuration["STOREFRONT_STORES_PATH"] : Path.Combine(SampleDirectory(business), "store-locations.json");
    public BusinessPartition For(Business business)
    {
        Validate();
        if (registry.Find(business.Slug) != business || (Sql && business.Slug != BusinessRegistry.DemoSlug)) throw new InvalidOperationException("Business partition unavailable.");
        return partitions.GetOrAdd(business.Slug, _ => new Lazy<BusinessPartition>(() => Create(business))).Value;
    }
    private BusinessPartition Create(Business business)
    {
        var options = business.Slug == BusinessRegistry.DemoSlug ? defaults.Value : defaults.Value with { StoragePath = Path.Combine(Root(business), "item-assets") };
        IItemRepository items = Sql ? new SqlItemRepository(configuration["NBJEWEL_SQL_CONNECTION"]!) : new SampleItemRepository(SampleDirectory(business), options, allowEmpty: business.Slug != BusinessRegistry.DemoSlug);
        IItemImageAssetRepository assets = Sql ? new SqlItemImageAssetRepository(new DbContextOptionsBuilder<ItemAssetDatabase>().UseSqlServer(configuration["NBJEWEL_SQL_CONNECTION"]).Options) : new FileItemImageAssetRepository(options);
        return new(items, assets, options, new FileProductPublicationStore(options), new ItemCutoutPipeline(items, assets, options));
    }
    public KnowledgeDatabase Knowledge(DbContextOptions<KnowledgeDatabase> original, Business business)
    {
        Validate();
        if (registry.Find(business.Slug) != business || (Sql && business.Slug != BusinessRegistry.DemoSlug)) throw new InvalidOperationException("Business partition unavailable.");
        return business.Slug == BusinessRegistry.DemoSlug ? new(original)
            : new(new DbContextOptionsBuilder<KnowledgeDatabase>().UseInMemoryDatabase(databasePrefix + ":" + business.Slug).Options);
    }
    public static Business Current(IServiceProvider services)
    {
        var http = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        // Explicit host/test preparation adopts the demo; web requests must resolve before controller construction.
        return http is null ? services.GetRequiredService<BusinessRegistry>().Find(BusinessRegistry.DemoSlug)!
            : http.RequestServices.GetRequiredService<BusinessContext>().Business ?? throw new InvalidOperationException("Business context is missing.");
    }
}
