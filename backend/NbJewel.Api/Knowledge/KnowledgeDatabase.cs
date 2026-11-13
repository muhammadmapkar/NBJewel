// Role: Stores versioned knowledge and explicit product publication decisions.
using Microsoft.EntityFrameworkCore;
namespace NbJewel.Api.Knowledge;

public sealed class KnowledgeDatabase(DbContextOptions<KnowledgeDatabase> options) : DbContext(options)
{
    public DbSet<KnowledgeDocument> Documents => Set<KnowledgeDocument>();
    public DbSet<KnowledgeVersion> Versions => Set<KnowledgeVersion>();
    public DbSet<KnowledgeChunk> Chunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeAudit> Audits => Set<KnowledgeAudit>();
    public DbSet<ProductPublication> Products => Set<ProductPublication>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<KnowledgeDocument>().ToTable("AiKnowledgeDocuments").HasKey(x => x.Id);
        b.Entity<KnowledgeVersion>().ToTable("AiKnowledgeVersions").HasKey(x => x.Id);
        b.Entity<KnowledgeChunk>().ToTable("AiKnowledgeChunks").HasKey(x => x.Id);
        b.Entity<KnowledgeAudit>().ToTable("AiKnowledgeAudit").HasKey(x => x.Id);
        b.Entity<ProductPublication>().Property(x => x.AssetRevisionId).HasMaxLength(32);
        b.Entity<ProductPublication>().ToTable("AiPublicationProducts").HasKey(x => x.ItemId);
        b.Entity<KnowledgeDocument>().Property(x => x.LatestVersionId).IsConcurrencyToken();
        b.Entity<KnowledgeDocument>().Property(x => x.PublishedVersionId).IsConcurrencyToken();
        b.Entity<KnowledgeVersion>().HasIndex(x => x.DocumentId);
        b.Entity<KnowledgeChunk>().HasIndex(x => x.VersionId);
    }
}
public sealed class KnowledgeDocument { public string Id { get; set; } = ""; public string? PublishedVersionId { get; set; } public string LatestVersionId { get; set; } = ""; }
public sealed class KnowledgeVersion { public string Id { get; set; } = ""; public string DocumentId { get; set; } = ""; public string Title { get; set; } = ""; public string Collection { get; set; } = ""; public string Audience { get; set; } = ""; public string? RequiredPermission { get; set; } public string StoreIds { get; set; } = ""; public string? OwnerId { get; set; } }
public sealed class KnowledgeChunk { public string Id { get; set; } = ""; public string VersionId { get; set; } = ""; public string Text { get; set; } = ""; }
public sealed class ProductPublication { public int ItemId { get; set; } public bool Published { get; set; } public string? AssetRevisionId { get; set; } }
public sealed record KnowledgeInput(string Title, string Text, string Collection, string Audience, string? RequiredPermission, int[]? StoreIds, string? OwnerId, bool Confirm = false);

public sealed class KnowledgeAudit { public string Id { get; set; } = ""; public string ActorHash { get; set; } = ""; public string Operation { get; set; } = ""; public string TargetId { get; set; } = ""; public string Outcome { get; set; } = "confirmed"; public DateTimeOffset Timestamp { get; set; } }
