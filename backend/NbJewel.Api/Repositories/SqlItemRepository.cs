// Role: SQL Server implementation of IItemRepository, using the tables in
// docs/item_catalog_schema.sql. Every query is fixed and parameterized (no free
// SQL). It is only used when Repository:Provider is "Sql". All table and column
// names are assumed. This class is build-checked only, not run against a server.

using Microsoft.Data.SqlClient;
using NbJewel.Api.Models;

namespace NbJewel.Api.Repositories;

public sealed class SqlItemRepository(string connectionString) : IItemRepository
{
    private const string ItemSelect = """
        SELECT i.ItemId, i.ItemCode, i.Name, i.CategoryId,
               ISNULL(i.DesignId, 0) AS DesignId, ISNULL(d.DesignCode, N'') AS DesignCode,
               m.Name AS MetalType, p.Label AS Purity,
               i.GrossWeightG, i.NetWeightG, i.StoneWeightG, i.MakingType, i.MakingValue,
               i.HuidNumber, i.Status, img.Url AS ImageUrl
        FROM dbo.Item i
        JOIN dbo.Purity p ON p.PurityId = i.PurityId
        JOIN dbo.Metal m ON m.MetalId = p.MetalId
        LEFT JOIN dbo.Design d ON d.DesignId = i.DesignId
        LEFT JOIN dbo.ItemImage img ON img.ItemId = i.ItemId AND img.IsPrimary = 1
        """;

    public async Task<PagedResult<Item>> SearchItemsAsync(string? search, int? categoryId, string? metal, string? purity, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var where = new List<string>();
        var parameters = new List<SqlParameter>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(i.Name LIKE @search ESCAPE '\\' OR i.ItemCode LIKE @search ESCAPE '\\' OR d.DesignCode LIKE @search ESCAPE '\\')");
            parameters.Add(new("@search", $"%{EscapeLike(search)}%"));
        }
        if (categoryId.HasValue) { where.Add("i.CategoryId = @category"); parameters.Add(new("@category", categoryId.Value)); }
        if (!string.IsNullOrWhiteSpace(metal)) { where.Add("m.Name = @metal"); parameters.Add(new("@metal", metal)); }
        if (!string.IsNullOrWhiteSpace(purity)) { where.Add("p.Label = @purity"); parameters.Add(new("@purity", purity)); }
        if (!string.IsNullOrWhiteSpace(status)) { where.Add("i.Status = @status"); parameters.Add(new("@status", status)); }
        var clause = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);

        await using var connection = await OpenAsync(cancellationToken);
        var total = await ScalarAsync<int>(connection, null, "SELECT COUNT(*) FROM dbo.Item i JOIN dbo.Purity p ON p.PurityId = i.PurityId JOIN dbo.Metal m ON m.MetalId = p.MetalId LEFT JOIN dbo.Design d ON d.DesignId = i.DesignId" + clause, Clone(parameters), cancellationToken);

        parameters.Add(new("@skip", (page - 1) * pageSize));
        parameters.Add(new("@take", pageSize));
        var rows = await QueryAsync(connection, null, ItemSelect + clause + " ORDER BY i.ItemId OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", parameters, ReadItem, cancellationToken);
        return new PagedResult<Item>(rows, page, pageSize, total);
    }

    public async Task<Item?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await GetItemAsync(connection, null, id, cancellationToken);
    }

    public async Task<IReadOnlyList<StockRecord>> GetStockAsync(int itemId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await QueryAsync(connection, null,
            "SELECT v.ItemId, s.VariantId, s.StoreId, s.Quantity FROM dbo.StockByStore s JOIN dbo.ItemVariant v ON v.VariantId = s.VariantId WHERE v.ItemId = @itemId ORDER BY s.VariantId, s.StoreId",
            [new("@itemId", itemId)],
            r => new StockRecord { ItemId = r.GetInt32(0), VariantId = r.GetInt32(1), StoreId = r.GetInt32(2), OnHand = r.GetInt32(3), Reserved = 0 }, // no Reserved column in the schema, assumed 0
            cancellationToken);
    }

    public async Task<IReadOnlyList<StockRecord>> GetLowStockAsync(int threshold, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await QueryAsync(connection, null,
            "SELECT v.ItemId, s.VariantId, s.StoreId, s.Quantity FROM dbo.StockByStore s JOIN dbo.ItemVariant v ON v.VariantId = s.VariantId WHERE s.Quantity < @threshold ORDER BY v.ItemId, s.VariantId, s.StoreId",
            [new("@threshold", threshold)],
            r => new StockRecord { ItemId = r.GetInt32(0), VariantId = r.GetInt32(1), StoreId = r.GetInt32(2), OnHand = r.GetInt32(3), Reserved = 0 }, // no Reserved column in the schema, assumed 0
            cancellationToken);
    }

    public async Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(int itemId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await GetVariantsAsync(connection, null, itemId, cancellationToken);
    }

    public async Task<ItemPrice?> GetPriceAsync(int itemId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var item = await GetItemAsync(connection, null, itemId, cancellationToken);
        if (item is null) return null;
        var rates = await GetLatestRatesAsync(connection, cancellationToken);
        var rate = rates.FirstOrDefault(x => x.MetalType.Equals(item.MetalType, StringComparison.OrdinalIgnoreCase) && x.Purity.Equals(item.Purity, StringComparison.OrdinalIgnoreCase));
        return rate is null ? null : ItemPricing.Calculate(item, rate);
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await QueryAsync(connection, null, "SELECT CategoryId, Name, ParentId FROM dbo.Category ORDER BY CategoryId", [],
            r => new Category { Id = r.GetInt32(0), Name = r.GetString(1), ParentId = r.IsDBNull(2) ? null : r.GetInt32(2) }, cancellationToken);
    }

    public async Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await QueryAsync(connection, null, "SELECT StoreId, Name, City FROM dbo.Store ORDER BY StoreId", [],
            r => new Store { Id = r.GetInt32(0), Name = r.GetString(1), City = r.IsDBNull(2) ? "" : r.GetString(2) }, cancellationToken);
    }

    public async Task<IReadOnlyList<Design>> GetDesignsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await QueryAsync(connection, null, "SELECT DesignId, DesignCode, Name FROM dbo.Design ORDER BY DesignCode", [],
            r => new Design { DesignId = r.GetInt32(0), DesignCode = r.GetString(1), Name = r.GetString(2) }, cancellationToken);
    }

    public async Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await GetLatestRatesAsync(connection, cancellationToken);
    }

    public async Task<Item> CreateAsync(ItemCreateRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await EnsureDesignAsync(connection, transaction, request.DesignId, cancellationToken);
            var purityId = await FindPurityAsync(connection, transaction, request.MetalType, request.Purity, cancellationToken);
            var id = await ScalarAsync<int>(connection, transaction, """
                INSERT dbo.Item (ItemCode, Name, CategoryId, DesignId, PurityId, HuidNumber, Status, GrossWeightG, StoneWeightG, MakingType, MakingValue)
                OUTPUT INSERTED.ItemId
                VALUES (@code, @name, @category, @design, @purity, @huid, @status, @gross, @stone, @makingType, @makingValue)
                """,
                [.. WriteParameters(request.Code, request.Name, request.CategoryId, request.DesignId, purityId, request.Huid, request.GrossWt, request.StoneWt, request.MakingType, request.MakingValue), new("@status", ItemStatusRules.Draft)],
                cancellationToken);
            var item = await GetItemAsync(connection, transaction, id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return item!;
        }
        catch (SqlException ex) { throw Translate(ex); }
    }

    public async Task<Item?> UpdateAsync(int id, ItemUpdateRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await GetItemAsync(connection, transaction, id, cancellationToken) is null) return null;
            await EnsureDesignAsync(connection, transaction, request.DesignId, cancellationToken);
            var purityId = await FindPurityAsync(connection, transaction, request.MetalType, request.Purity, cancellationToken);
            // Status is not touched here. It only changes through UpdateStatusAsync.
            await ExecuteAsync(connection, transaction, """
                UPDATE dbo.Item
                SET ItemCode = @code, Name = @name, CategoryId = @category, DesignId = @design, PurityId = @purity,
                    HuidNumber = @huid, GrossWeightG = @gross, StoneWeightG = @stone,
                    MakingType = @makingType, MakingValue = @makingValue, UpdatedAt = SYSUTCDATETIME()
                WHERE ItemId = @id
                """,
                [.. WriteParameters(request.Code, request.Name, request.CategoryId, request.DesignId, purityId, request.Huid, request.GrossWt, request.StoneWt, request.MakingType, request.MakingValue), new("@id", id)],
                cancellationToken);
            var item = await GetItemAsync(connection, transaction, id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return item;
        }
        catch (SqlException ex) { throw Translate(ex); }
    }

    public async Task<Item?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        // UPDLOCK keeps two requests from moving the same item at the same time.
        var current = (await QueryAsync(connection, transaction, "SELECT Status FROM dbo.Item WITH (UPDLOCK, ROWLOCK) WHERE ItemId = @id", [new("@id", id)], r => r.GetString(0), cancellationToken)).FirstOrDefault();
        if (current is null) return null;
        var next = ItemStatusRules.Resolve(status);
        // Any known status is allowed. Choosing the current one writes nothing.
        if (!next.Equals(current, StringComparison.Ordinal))
            await ExecuteAsync(connection, transaction, "UPDATE dbo.Item SET Status = @status, UpdatedAt = SYSUTCDATETIME() WHERE ItemId = @id", [new("@status", next), new("@id", id)], cancellationToken);
        var item = await GetItemAsync(connection, transaction, id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return item;
    }

    public async Task<StockRecord?> AddStockAsync(int itemId, StockAddRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await GetItemAsync(connection, transaction, itemId, cancellationToken) is null) return null;
            StockRules.EnsureQuantity(request.Quantity);
            if (await ScalarAsync<int>(connection, transaction, "SELECT COUNT(*) FROM dbo.Store WHERE StoreId = @store", [new("@store", request.StoreId)], cancellationToken) == 0)
                throw new DomainValidationException($"Store {request.StoreId} does not exist.");

            // The schema keeps stock per variant, so an item needs a variant to hold stock.
            var itemVariants = await GetVariantsAsync(connection, transaction, itemId, cancellationToken);
            if (itemVariants.Count == 0) throw new DomainValidationException("This item has no variants, and stock is stored per variant. Add a variant first.");
            if (request.VariantId is null) throw new DomainValidationException("variantId is required for an item that has variants.");
            if (itemVariants.All(x => x.Id != request.VariantId)) throw new DomainValidationException($"Variant {request.VariantId} does not belong to item {itemId}.");

            var onHand = await ScalarAsync<int>(connection, transaction, """
                MERGE dbo.StockByStore WITH (HOLDLOCK) AS t
                USING (SELECT @variant AS VariantId, @store AS StoreId) AS s
                  ON t.VariantId = s.VariantId AND t.StoreId = s.StoreId
                WHEN MATCHED THEN UPDATE SET Quantity = t.Quantity + @qty
                WHEN NOT MATCHED THEN INSERT (VariantId, StoreId, Quantity) VALUES (@variant, @store, @qty)
                OUTPUT INSERTED.Quantity;
                """,
                [new("@variant", request.VariantId.Value), new("@store", request.StoreId), new("@qty", request.Quantity)],
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new StockRecord { ItemId = itemId, VariantId = request.VariantId, StoreId = request.StoreId, OnHand = onHand, Reserved = 0 };
        }
        catch (SqlException ex) { throw Translate(ex); }
    }

    // ---------- helpers ----------

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<Item?> GetItemAsync(SqlConnection connection, SqlTransaction? transaction, int id, CancellationToken cancellationToken) =>
        (await QueryAsync(connection, transaction, ItemSelect + " WHERE i.ItemId = @id", [new("@id", id)], ReadItem, cancellationToken)).FirstOrDefault();

    private static Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(SqlConnection connection, SqlTransaction? transaction, int itemId, CancellationToken cancellationToken) =>
        QueryAsync(connection, transaction, "SELECT VariantId, ItemId, ISNULL(SizeLabel, N''), Sku FROM dbo.ItemVariant WHERE ItemId = @itemId ORDER BY VariantId", [new("@itemId", itemId)],
            r => new ItemVariant { Id = r.GetInt32(0), ItemId = r.GetInt32(1), Size = r.GetString(2), Color = "", VariantCode = r.GetString(3), PriceDelta = 0 }, // schema has no color or price delta, assumed empty
            cancellationToken);

    private static Task<IReadOnlyList<MetalRate>> GetLatestRatesAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        QueryAsync(connection, null, """
            SELECT r.RateId, m.Name, p.Label, r.RatePerGram, r.EffectiveAt
            FROM dbo.MetalRate r
            JOIN dbo.Purity p ON p.PurityId = r.PurityId
            JOIN dbo.Metal m ON m.MetalId = p.MetalId
            WHERE r.RateId = (SELECT TOP 1 r2.RateId FROM dbo.MetalRate r2 WHERE r2.PurityId = r.PurityId ORDER BY r2.EffectiveAt DESC, r2.RateId DESC)
            ORDER BY r.RateId
            """, [],
            r => new MetalRate { Id = r.GetInt32(0), MetalType = r.GetString(1), Purity = r.GetString(2), RatePerGram = r.GetDecimal(3), Currency = "AED", EffectiveDate = DateOnly.FromDateTime(r.GetDateTime(4)) }, // schema has no currency, assumed AED
            cancellationToken);

    private static Item ReadItem(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Code = r.GetString(1),
        Name = r.GetString(2),
        CategoryId = r.GetInt32(3),
        DesignId = r.GetInt32(4),
        DesignCode = r.GetString(5),
        MetalType = r.GetString(6),
        Purity = r.GetString(7),
        GrossWt = r.GetDecimal(8),
        NetWt = r.GetDecimal(9),
        StoneWt = r.GetDecimal(10),
        MakingType = FromDbMakingType(r.GetString(11)),
        MakingValue = r.GetDecimal(12),
        Huid = r.IsDBNull(13) ? null : r.GetString(13),
        Status = r.GetString(14),
        ImageUrl = r.IsDBNull(15) ? null : r.GetString(15)
    };

    // The API uses perGram, percent, flat. The schema uses PER_GRAM, PERCENT, FIXED.
    private static string FromDbMakingType(string value) => value switch { "PER_GRAM" => "perGram", "PERCENT" => "percent", "FIXED" => "flat", _ => value };
    private static string ToDbMakingType(string value) => value.ToLowerInvariant() switch
    {
        "pergram" => "PER_GRAM",
        "percent" => "PERCENT",
        "flat" => "FIXED",
        _ => throw new DomainValidationException($"Unsupported making type '{value}'.")
    };

    private static SqlParameter[] WriteParameters(string code, string name, int categoryId, int designId, int purityId, string? huid, decimal gross, decimal stone, string makingType, decimal makingValue) =>
    [
        new("@code", code), new("@name", name), new("@category", categoryId), new("@design", designId), new("@purity", purityId),
        new("@huid", (object?)huid ?? DBNull.Value), new("@gross", gross), new("@stone", stone),
        new("@makingType", ToDbMakingType(makingType)), new("@makingValue", makingValue)
    ];

    private static async Task EnsureDesignAsync(SqlConnection connection, SqlTransaction transaction, int designId, CancellationToken cancellationToken)
    {
        if (await ScalarAsync<int>(connection, transaction, "SELECT COUNT(*) FROM dbo.Design WHERE DesignId = @id", [new("@id", designId)], cancellationToken) == 0)
            throw new DomainValidationException($"Design {designId} does not exist.");
    }

    private static async Task<int> FindPurityAsync(SqlConnection connection, SqlTransaction transaction, string metal, string purity, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(connection, transaction, "SELECT p.PurityId FROM dbo.Purity p JOIN dbo.Metal m ON m.MetalId = p.MetalId WHERE m.Name = @metal AND p.Label = @purity", [new("@metal", metal), new("@purity", purity)], r => r.GetInt32(0), cancellationToken);
        return rows.Count == 1 ? rows[0] : throw new DomainValidationException($"No purity '{purity}' exists for metal '{metal}'.");
    }

    // Turns database rule failures into the same 400 errors the sample repository gives.
    private static Exception Translate(SqlException ex) => ex.Number switch
    {
        2627 or 2601 => new DomainValidationException("That item code already exists."),
        547 => new DomainValidationException("The data breaks a database rule (check weights, quantity, and linked ids)."),
        8115 => new DomainValidationException("Quantity is too large; the on-hand total would overflow."),
        _ => ex
    };

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
    private static List<SqlParameter> Clone(List<SqlParameter> source) => source.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToList();

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(SqlConnection connection, SqlTransaction? transaction, string sql, IEnumerable<SqlParameter> parameters, Func<SqlDataReader, T> read, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) rows.Add(read(reader));
        return rows;
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, SqlTransaction? transaction, string sql, IEnumerable<SqlParameter> parameters, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters.ToArray());
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction? transaction, string sql, IEnumerable<SqlParameter> parameters, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
