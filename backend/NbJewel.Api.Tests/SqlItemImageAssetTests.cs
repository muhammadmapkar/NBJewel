// Role: Verifies SQL asset model and repository behavior without connecting to a database server.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Tests;

public sealed class SqlItemImageAssetTests
{
    private static ItemImageAsset Asset(string revision, string state = "processing") =>
        new(32, revision, new string('A', 64), state == "ready" ? new string('B', 64) : null,
            state, "pending", "open", 20, 30, DateTimeOffset.UtcNow);
    [Fact]
    public async Task RestartRetainsRevisionAndReviewAndRejectsStaleCompletion()
    {
        var options = new DbContextOptionsBuilder<ItemAssetDatabase>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var first = new SqlItemImageAssetRepository(options);
        var revision = Guid.NewGuid().ToString("N");
        await first.SaveAsync(Asset(revision), default);
        await first.SaveAsync(Asset(revision, "ready"), default);
        var restarted = new SqlItemImageAssetRepository(options);
        Assert.Equal("pending", (await restarted.GetAsync(32, default))!.QualityReview);
        Assert.Equal("approved", (await restarted.ReviewAsync(32, revision, true, default))!.QualityReview);
        var replacement = Guid.NewGuid().ToString("N");
        await restarted.SaveAsync(Asset(replacement), default);
        await Assert.ThrowsAsync<ItemAssetConflictException>(() => first.SaveAsync(Asset(revision, "ready"), default));
        await Assert.ThrowsAsync<ItemAssetConflictException>(() => first.ReviewAsync(32, revision, true, default));
        Assert.Equal("pending", (await first.GetAsync(32, default))!.QualityReview);
        Assert.Null(await first.GetAsync(999, default));
    }
    [Fact]
    public void SqlModelUsesExternalItemIdAndRevisionConcurrencyToken()
    {
        using var db = new ItemAssetDatabase(new DbContextOptionsBuilder<ItemAssetDatabase>()
            .UseSqlServer("Server=unused.invalid;Database=model_only;Integrated Security=True").Options);
        var entity = db.Model.FindEntityType(typeof(ItemImageAsset))!;
        Assert.Equal("ItemImageAsset", entity.GetTableName());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty("ItemId")!.ValueGenerated);
        Assert.True(entity.FindProperty("RevisionId")!.IsConcurrencyToken);
        Assert.Equal(32, entity.FindProperty("RevisionId")!.GetMaxLength());
    }
}

// Owner-run only against a separately prepared disposable SQL database, never the demo database.
public sealed class CatalogSqlFactAttribute : FactAttribute
{
    public CatalogSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NBJEWEL_CATALOG_TEST_CONNECTION")))
            Skip = "Set an approved isolated NBJEWEL_CATALOG_TEST_CONNECTION after preparing its schema/reference data.";
    }
}
public sealed class CatalogSqlIntegrationTests
{
    [CatalogSqlFact]
    public async Task SqlItemAndAssetSurviveNewRepositoryInstancesAndRejectOldReview()
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NBJEWEL_CATALOG_TEST_CONNECTION"));
        if (connection.DataSource != "127.0.0.1,15433" || connection.InitialCatalog != "NBJewelCatalogTest")
            throw new InvalidOperationException("Use only the separately prepared disposable NBJewelCatalogTest on 127.0.0.1,15433.");
        var repo = new SqlItemRepository(connection.ConnectionString);
        var category = (await repo.GetCategoriesAsync(default)).Single(x => x.Name == "Necklaces");
        var design = (await repo.GetDesignsAsync(default)).Single(x => x.DesignCode == "DEMO-HERITAGE");
        var code = "TEST-" + Guid.NewGuid().ToString("N")[..20];
        var item = await repo.CreateAsync(new(code, "Assumed SQL test necklace", category.Id, design.DesignId, "Gold", "22K", 10, 10, 0, "PERCENT", 10, null, null,
            new NbJewel.Api.Models.StorefrontMetadata { Description="Assumed SQL test", Collection="Antique" }), default);
        var options = new DbContextOptionsBuilder<ItemAssetDatabase>().UseSqlServer(connection.ConnectionString).Options;
        var assets = new SqlItemImageAssetRepository(options); var revision=Guid.NewGuid().ToString("N");
        var asset = new ItemImageAsset(item.Id, revision, new string('A',64), null,"processing","pending","open",0,0,DateTimeOffset.UtcNow);
        await assets.SaveAsync(asset,default);
        await assets.SaveAsync(asset with { State="ready",PreviewSha256=new string('B',64),Width=20,Height=30 },default);
        await assets.ReviewAsync(item.Id,revision,true,default);
        Assert.Equal(code,(await new SqlItemRepository(connection.ConnectionString).GetItemAsync(item.Id,default))!.Code);
        var restarted = new SqlItemImageAssetRepository(options);
        Assert.Equal("approved",(await restarted.GetAsync(item.Id,default))!.QualityReview);
        await restarted.SaveAsync(asset with { RevisionId=Guid.NewGuid().ToString("N") },default);
        await Assert.ThrowsAsync<ItemAssetConflictException>(()=>assets.ReviewAsync(item.Id,revision,true,default));
        // Non-destructive test: uniquely named records are left in the disposable database for inspection.
    }
}
