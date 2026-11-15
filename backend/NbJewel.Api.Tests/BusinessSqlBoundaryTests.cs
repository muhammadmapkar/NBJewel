// Role: Verifies SQL startup is single-business and its adapters resolve offline without new runtime tables.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Repositories;
using NbJewel.Api.Storefront;
namespace NbJewel.Api.Tests;
public sealed class BusinessSqlBoundaryTests
{
    [Fact]
    public void SqlDefaultRegistryAndEveryOwnedAdapterResolveWithoutOpeningAConnection()
    {
        using var host = new IdentityTestFactory();
        var environment = host.Services.GetRequiredService<IWebHostEnvironment>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Repository:Provider"] = "Sql", ["NBJEWEL_SQL_CONNECTION"] = "Server=unused.invalid;Database=model_only;Integrated Security=True",
            ["BUSINESS_STORAGE_PATH"] = Path.Combine(Path.GetTempPath(), "nbjewel-sql-di-" + Guid.NewGuid().ToString("N")) }).Build();
        var registry = new BusinessRegistry(config, environment);
        Assert.Equal("nb-jewel-demo", Assert.Single(registry.All).Slug);
        var options = new ItemCutoutOptions(Path.Combine(Path.GetTempPath(), "nbjewel-sql-di-assets-" + Guid.NewGuid().ToString("N")), "unused", "unused");
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(config).AddSingleton(environment)
            .AddSingleton(registry).AddSingleton(new DefaultCutoutOptions(options)).AddSingleton<BusinessStorage>()
            .AddSingleton(new IdentityDatabase(new DbContextOptionsBuilder<IdentityDatabase>().UseSqlServer(config["NBJEWEL_SQL_CONNECTION"]).Options))
            .AddScoped<BusinessMemberships>().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var storage = services.GetRequiredService<BusinessStorage>(); storage.Validate();
        var partition = storage.For(registry.All[0]);
        Assert.IsType<SqlItemRepository>(partition.Items); Assert.IsType<SqlItemImageAssetRepository>(partition.Assets);
        using var knowledge = storage.Knowledge(new DbContextOptionsBuilder<KnowledgeDatabase>().UseSqlServer(config["NBJEWEL_SQL_CONNECTION"]).Options, registry.All[0]);
        Assert.Equal("AiPublicationProducts", knowledge.Model.FindEntityType(typeof(ProductPublication))!.GetTableName());
        Assert.Null(knowledge.Model.FindEntityType(typeof(ProductPublication))!.FindProperty("BusinessId"));
        using var scope = services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BusinessMemberships>());
        // These existing adapters can be composed without executing a query or requiring a new table.
        Assert.NotNull(new StoreDirectory(config, partition.Items, new KnowledgeService(knowledge)));
        Assert.NotNull(new CustomerPortal(scope.ServiceProvider.GetRequiredService<IdentityDatabase>()));
    }
    [Fact]
    public void SqlStartupRejectsASecondBusinessBeforeAnyDataAccess()
    {
        using var factory = new KnowledgeHttpFactory { TestConfiguration = new() {
            ["Repository:Provider"] = "Sql", ["NBJEWEL_SQL_CONNECTION"] = "Server=unused.invalid;Database=model_only;Integrated Security=True",
            ["BUSINESSES_PATH"] = Path.Combine(SampleRepositoryFactory.FindRoot(), "sample-data", "businesses.json") } };
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("SQL mode supports exactly nb-jewel-demo", error.ToString());
    }
}
