// Role: Developer tool that prepares a SQL Server database for SqlItemRepository.
// Step 1 creates the database named in the connection string (if missing).
// Step 2 runs docs/item_catalog_schema.sql (tables and views) on an empty database.
// Step 3 seeds the tables from sample-data/*.json (sample data only, never real data).
// Run it with: dotnet run --project backend/NbJewel.Api -- --setup-db [--reset]
// The tests reuse this class to build their own throwaway database.
// All data values go in as parameters. The only built SQL is the database name
// (quoted with QUOTENAME in SQL Server) and fixed table names from this file.

using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using NbJewel.Api.Models;

namespace NbJewel.Api.Repositories;

public static class SqlDatabaseSetup
{
    // Assumed purity strengths for the sample purities. Not confirmed by NB Jewel.
    private static readonly Dictionary<string, decimal> Fineness = new(StringComparer.OrdinalIgnoreCase)
    {
        ["24K"] = 0.9990m, ["22K"] = 0.9160m, ["18K"] = 0.7500m, ["925"] = 0.9250m
    };

    // Creates the database, applies the schema when the database is empty, and seeds it.
    // reset = drop the database first. Seeding is skipped when items exist, unless reseed is true.
    public static async Task RunAsync(string connectionString, string schemaPath, string sampleDataDirectory, bool reset, bool reseed, Action<string> log, CancellationToken cancellationToken = default)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("The connection string needs Initial Catalog (the database name).");

        if (reset) { await DropDatabaseAsync(connectionString, cancellationToken); log($"Dropped database {database} (if it existed)."); }
        await CreateDatabaseAsync(connectionString, cancellationToken);
        log($"Database {database} is ready.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var hasSchema = await ScalarAsync(connection, "SELECT CASE WHEN OBJECT_ID(N'dbo.Item', N'U') IS NULL THEN 0 ELSE 1 END", cancellationToken) == 1;
        if (!hasSchema) { await ApplySchemaAsync(connection, schemaPath, cancellationToken); log("Applied item_catalog_schema.sql."); }
        else log("Schema already exists, left as is. Use --reset to rebuild it.");

        var itemCount = await ScalarAsync(connection, "SELECT COUNT(*) FROM dbo.Item", cancellationToken);
        if (itemCount == 0 || reseed || reset) { await SeedAsync(connection, sampleDataDirectory, cancellationToken); log("Seeded sample data from sample-data/*.json."); }
        else log($"Database already holds {itemCount} items, seed skipped. Use --reset to reload the sample data.");
    }

    // Drops the whole database. Used by --reset and by the tests (which only ever touch their own test database).
    public static async Task DropDatabaseAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var (master, database) = MasterConnection(connectionString);
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            IF DB_ID(@db) IS NOT NULL
            BEGIN
                DECLARE @name NVARCHAR(258) = QUOTENAME(@db);
                DECLARE @sql NVARCHAR(MAX) = N'ALTER DATABASE ' + @name + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + @name + N';';
                EXEC sys.sp_executesql @sql;
            END
            """, connection);
        command.Parameters.AddWithValue("@db", database);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task CreateDatabaseAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var (master, database) = MasterConnection(connectionString);
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            IF DB_ID(@db) IS NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@db) + N';';
                EXEC sys.sp_executesql @sql;
            END
            """, connection);
        command.Parameters.AddWithValue("@db", database);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Runs the schema file. A line holding only GO ends a batch (GO is a tool word, not T-SQL).
    public static async Task ApplySchemaAsync(SqlConnection connection, string schemaPath, CancellationToken cancellationToken = default)
    {
        var batch = new List<string>();
        async Task Flush()
        {
            var text = string.Join('\n', batch).Trim();
            batch.Clear();
            if (text.Length == 0) return;
            await using var command = new SqlCommand(text, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var line in await File.ReadAllLinesAsync(schemaPath, cancellationToken))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase)) await Flush();
            else batch.Add(line);
        }
        await Flush();
    }

    // Clears every table and loads the JSON files again, keeping the JSON ids so both providers return the same ids.
    public static async Task SeedAsync(SqlConnection connection, string sampleDataDirectory, CancellationToken cancellationToken = default)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<T> Load<T>(string file) => JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(sampleDataDirectory, file)), options) ?? [];
        var items = Load<Item>("items.json");
        var variants = Load<ItemVariant>("variants.json");
        var stock = Load<StockRecord>("stock.json");
        var categories = Load<Category>("categories.json");
        var stores = Load<Store>("stores.json");
        var designs = Load<Design>("designs.json");
        var rates = Load<MetalRate>("metal-rates.json");

        // Metals and purities come from the rates first, then from the items.
        var pairs = rates.Select(x => (x.MetalType, x.Purity)).Concat(items.Select(x => (x.MetalType, x.Purity)))
            .DistinctBy(x => (x.Item1.ToLowerInvariant(), x.Item2.ToLowerInvariant())).ToList();
        var metals = pairs.Select(x => x.MetalType).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        // Fixed statements, no user input. Children first so the foreign keys allow the delete.
        foreach (var table in new[] { "StockByStore", "ItemImage", "ItemVariant", "Item", "MetalRate", "Purity", "Metal", "Design", "Store", "Category" })
        {
            await ExecAsync(connection, transaction, $"DELETE FROM dbo.{table}", [], cancellationToken); // table comes from the fixed list above
            // Tables seeded with their JSON ids restart their counter, so the next new row gets max id + 1 on a reseed too.
            if (table is not ("StockByStore" or "ItemImage"))
                await ExecAsync(connection, transaction, $"DBCC CHECKIDENT (N'dbo.{table}', RESEED, 0) WITH NO_INFOMSGS", [], cancellationToken);
        }

        for (var i = 0; i < metals.Count; i++)
            await InsertAsync(connection, transaction, "Metal", "MetalId, Name", "@id, @name", [P("@id", i + 1), P("@name", metals[i])], cancellationToken);
        for (var i = 0; i < pairs.Count; i++)
        {
            if (!Fineness.TryGetValue(pairs[i].Purity, out var fineness)) throw new InvalidOperationException($"No fineness is known for purity '{pairs[i].Purity}'. Add it to the Fineness table in SqlDatabaseSetup.");
            var metalId = metals.FindIndex(m => m.Equals(pairs[i].MetalType, StringComparison.OrdinalIgnoreCase)) + 1;
            await InsertAsync(connection, transaction, "Purity", "PurityId, MetalId, Label, Fineness", "@id, @metal, @label, @fineness", [P("@id", i + 1), P("@metal", metalId), P("@label", pairs[i].Purity), P("@fineness", fineness)], cancellationToken);
        }
        int PurityId(string metal, string purity) => pairs.FindIndex(x => x.MetalType.Equals(metal, StringComparison.OrdinalIgnoreCase) && x.Purity.Equals(purity, StringComparison.OrdinalIgnoreCase)) + 1;

        // Parents before children, so the self-reference on Category is satisfied.
        foreach (var c in categories.OrderBy(x => x.ParentId.HasValue).ThenBy(x => x.Id))
            await InsertAsync(connection, transaction, "Category", "CategoryId, Name, ParentId", "@id, @name, @parent", [P("@id", c.Id), P("@name", c.Name), P("@parent", c.ParentId)], cancellationToken);
        foreach (var s in stores)
            await InsertAsync(connection, transaction, "Store", "StoreId, Code, Name, City", "@id, @code, @name, @city", [P("@id", s.Id), P("@code", $"ST{s.Id:00}"), P("@name", s.Name), P("@city", s.City)], cancellationToken); // Code is assumed, made from the id
        foreach (var d in designs)
            await InsertAsync(connection, transaction, "Design", "DesignId, DesignCode, Name", "@id, @code, @name", [P("@id", d.DesignId), P("@code", d.DesignCode), P("@name", d.Name)], cancellationToken);

        foreach (var i in items)
        {
            var valid = ItemWriteRules.Validate(i.Code, i.Name, i.GrossWt, i.StoneWt, i.MakingType, i.MakingValue, i.Huid, i.ImageUrl); // same rules as the API, so bad seed data fails loudly
            await InsertAsync(connection, transaction, "Item", "ItemId, ItemCode, Name, CategoryId, DesignId, PurityId, HuidNumber, Status, GrossWeightG, StoneWeightG, MakingType, MakingValue",
                "@id, @code, @name, @category, @design, @purity, @huid, @status, @gross, @stone, @makingType, @makingValue",
                [P("@id", i.Id), P("@code", valid.Code), P("@name", valid.Name), P("@category", i.CategoryId), P("@design", i.DesignId), P("@purity", PurityId(i.MetalType, i.Purity)),
                 P("@huid", valid.Huid), P("@status", ItemStatusRules.Resolve(i.Status)), P("@gross", valid.GrossWt), P("@stone", valid.StoneWt),
                 P("@makingType", ToDbMakingType(valid.MakingType)), P("@makingValue", valid.MakingValue)], cancellationToken);
            if (!string.IsNullOrWhiteSpace(i.ImageUrl))
                await ExecAsync(connection, transaction, "INSERT dbo.ItemImage (ItemId, Url, IsPrimary) VALUES (@item, @url, 1)", [P("@item", i.Id), P("@url", i.ImageUrl)], cancellationToken);
        }
        foreach (var v in variants)
            await InsertAsync(connection, transaction, "ItemVariant", "VariantId, ItemId, Sku, SizeLabel, Color, PriceDelta", "@id, @item, @sku, @size, @color, @delta",
                [P("@id", v.Id), P("@item", v.ItemId), P("@sku", v.VariantCode), P("@size", v.Size), P("@color", v.Color), P("@delta", v.PriceDelta)], cancellationToken);
        foreach (var s in stock)
            await ExecAsync(connection, transaction, "INSERT dbo.StockByStore (ItemId, VariantId, StoreId, Quantity, Reserved) VALUES (@item, @variant, @store, @onHand, @reserved)",
                [P("@item", s.ItemId), P("@variant", s.VariantId), P("@store", s.StoreId), P("@onHand", s.OnHand), P("@reserved", s.Reserved)], cancellationToken);
        foreach (var r in rates)
            await InsertAsync(connection, transaction, "MetalRate", "RateId, PurityId, RatePerGram, Currency, EffectiveAt", "@id, @purity, @rate, @currency, @at",
                [P("@id", r.Id), P("@purity", PurityId(r.MetalType, r.Purity)), P("@rate", r.RatePerGram), P("@currency", r.Currency), P("@at", r.EffectiveDate.ToDateTime(TimeOnly.MinValue))], cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    // Same mapping as SqlItemRepository: the API says perGram, percent, flat. The schema says PER_GRAM, PERCENT, FIXED.
    public static string ToDbMakingType(string value) => value.ToLowerInvariant() switch
    {
        "pergram" => "PER_GRAM",
        "percent" => "PERCENT",
        "flat" => "FIXED",
        _ => throw new DomainValidationException($"Unsupported making type '{value}'.")
    };

    // A parameter that may hold a null. Plain AddWithValue(null) is not allowed, so null becomes DBNull.
    private static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    // INSERT with IDENTITY_INSERT so the ids from the JSON files are kept. The table and column lists are fixed text in this file.
    private static async Task InsertAsync(SqlConnection connection, SqlTransaction transaction, string table, string columns, string values, SqlParameter[] parameters, CancellationToken cancellationToken)
    {
        await ExecAsync(connection, transaction, $"SET IDENTITY_INSERT dbo.{table} ON; INSERT dbo.{table} ({columns}) VALUES ({values}); SET IDENTITY_INSERT dbo.{table} OFF;", parameters, cancellationToken);
    }

    private static async Task ExecAsync(SqlConnection connection, SqlTransaction? transaction, string sql, SqlParameter[] parameters, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    // Same server and login, but the master database, so the target database can be created or dropped.
    private static (string Master, string Database) MasterConnection(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("The connection string needs Initial Catalog (the database name).");
        builder.InitialCatalog = "master";
        return (builder.ConnectionString, database);
    }
}
