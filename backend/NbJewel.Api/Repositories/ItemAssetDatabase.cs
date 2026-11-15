// Role: Maps ERP preview metadata to SQL; image bytes remain in durable revision folders.
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Jewelry;
namespace NbJewel.Api.Repositories;

public sealed class ItemAssetDatabase(DbContextOptions<ItemAssetDatabase> options) : DbContext(options)
{
    public DbSet<ItemImageAsset> Assets => Set<ItemImageAsset>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var asset = model.Entity<ItemImageAsset>();
        asset.ToTable("ItemImageAsset", "dbo").HasKey(x => x.ItemId);
        asset.Property(x => x.ItemId).ValueGeneratedNever();
        asset.Property(x => x.RevisionId).HasMaxLength(32).IsConcurrencyToken();
        asset.Property(x => x.SourceSha256).HasMaxLength(64);
        asset.Property(x => x.PreviewSha256).HasMaxLength(64);
        asset.Property(x => x.State).HasMaxLength(20);
        asset.Property(x => x.QualityReview).HasMaxLength(20).IsConcurrencyToken();
        asset.Property(x => x.LicenseStatus).HasMaxLength(20);
        asset.Property(x => x.FailureCode).HasMaxLength(100);
    }
}
