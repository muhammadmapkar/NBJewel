// Role: Lets one test body run against both data providers. Repos.Providers feeds
// [Theory] tests: "Sample" always, and "Sql" only when NBJEWEL_SQL_CONNECTION is set.
// SQL tests never touch the database named in that variable. They use their own
// database called NBJewelTest on the same server, which is dropped once at the start of
// a test run and re-seeded from sample-data before every test.
// Tests run one at a time (see the assembly attribute below) because the SQL database is shared.

using Microsoft.Data.SqlClient;
using NbJewel.Api.Repositories;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NbJewel.Api.Tests;

public static class Repos
{
    public static IEnumerable<object[]> Providers
    {
        get
        {
            yield return ["Sample"];
            if (SqlTestDatabase.IsConfigured) yield return ["Sql"];
        }
    }

    public static IItemRepository Create(string provider) =>
        provider == "Sql" ? SqlTestDatabase.Create(SampleRepositoryFactory.DataFolder()) : SampleRepositoryFactory.Create();

    // Same as Create, but the metal rates come from a file in the Fixtures folder.
    public static IItemRepository CreateWithRates(string provider, string fixtureFile) =>
        provider == "Sql" ? SqlTestDatabase.Create(SampleRepositoryFactory.DataFolder(fixtureFile)) : SampleRepositoryFactory.CreateWithRates(fixtureFile);
}

internal static class SqlTestDatabase
{
    public const string DatabaseName = "NBJewelTest";
    private static bool schemaReady;

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NBJEWEL_SQL_CONNECTION"));

    // The connection string from the environment, pointed at the test database.
    public static string ConnectionString =>
        new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NBJEWEL_SQL_CONNECTION")) { InitialCatalog = DatabaseName }.ConnectionString;

    // Fresh schema once per run, then fresh sample data for every call.
    public static SqlItemRepository Create(string dataFolder)
    {
        PrepareAsync(dataFolder).GetAwaiter().GetResult();
        return new SqlItemRepository(ConnectionString);
    }

    private static async Task PrepareAsync(string dataFolder)
    {
        if (!schemaReady)
        {
            await SqlDatabaseSetup.DropDatabaseAsync(ConnectionString);
            await SqlDatabaseSetup.CreateDatabaseAsync(ConnectionString);
            await using var first = new SqlConnection(ConnectionString);
            await first.OpenAsync();
            await SqlDatabaseSetup.ApplySchemaAsync(first, Path.Combine(SampleRepositoryFactory.FindRoot(), "docs", "item_catalog_schema.sql"));
            schemaReady = true;
        }
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await SqlDatabaseSetup.SeedAsync(connection, dataFolder);
    }
}

// Like [Fact], but skipped (and shown as skipped) when no SQL Server is configured.
public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (!SqlTestDatabase.IsConfigured) Skip = "Set NBJEWEL_SQL_CONNECTION to run tests against SQL Server.";
    }
}
