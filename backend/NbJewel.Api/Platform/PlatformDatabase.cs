// Role: Stores owned conversations, idempotent turns, minimized messages, usage and security audit records in SQL.
using Microsoft.EntityFrameworkCore;

namespace NbJewel.Api.Platform;

public sealed class PlatformConversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssistantId { get; set; } = "";
    public string OwnerHash { get; set; } = "";
    public string AccessFingerprint { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public long Revision { get; set; }
}

public sealed class PlatformMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ConversationId { get; set; } = "";
    public string Role { get; set; } = "";
    public string ProtectedText { get; set; } = "";
    public string ProtectedSources { get; set; } = "";
    public string ProtectedEvidence { get; set; } = "";
    public bool Refused { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PlatformTurn
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ConversationId { get; set; } = "";
    public string ClientRequestId { get; set; } = "";
    public string QuestionHash { get; set; } = "";
    public string State { get; set; } = "pending";
    public string? ProtectedReply { get; set; }
    public string? ProtectedEvidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long ReservedTokens { get; set; }
}

public sealed class PlatformUsage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssistantId { get; set; } = "";
    public string SubjectHash { get; set; } = "";
    public string Operation { get; set; } = "message";
    public string TraceId { get; set; } = "";
    public string Model { get; set; } = "";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public long LatencyMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PlatformAudit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssistantId { get; set; } = "";
    public string SubjectHash { get; set; } = "";
    public string TraceId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Outcome { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PlatformGate
{
    public string Id { get; set; } = "";
    public DateTimeOffset MinuteStart { get; set; }
    public int MinuteCount { get; set; }
    public DateTimeOffset DayStart { get; set; }
    public int DayCount { get; set; }
    public long DayTokens { get; set; }
    public long Revision { get; set; }
}

public sealed class PlatformDataProtectionKey
{
    public int Id { get; set; }
    public string FriendlyName { get; set; } = "";
    public string ProtectedXml { get; set; } = "";
}

public sealed class PlatformDatabase(DbContextOptions<PlatformDatabase> options) : DbContext(options)
{
    public DbSet<PlatformConversation> Conversations => Set<PlatformConversation>();
    public DbSet<PlatformMessage> Messages => Set<PlatformMessage>();
    public DbSet<PlatformTurn> Turns => Set<PlatformTurn>();
    public DbSet<PlatformUsage> Usage => Set<PlatformUsage>();
    public DbSet<PlatformAudit> Audit => Set<PlatformAudit>();
    public DbSet<PlatformGate> Gates => Set<PlatformGate>();
    public DbSet<PlatformDataProtectionKey> DataProtectionKeys => Set<PlatformDataProtectionKey>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PlatformDataProtectionKey>().ToTable("AiDataProtectionKeys");
        b.Entity<PlatformConversation>().ToTable("AiConversations");
        b.Entity<PlatformConversation>().HasIndex(x => new { x.OwnerHash, x.AssistantId });
        b.Entity<PlatformConversation>().HasIndex(x => x.ExpiresAt);
        b.Entity<PlatformConversation>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<PlatformMessage>().ToTable("AiMessages");
        b.Entity<PlatformMessage>().HasOne<PlatformConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PlatformMessage>().HasIndex(x => new { x.ConversationId, x.CreatedAt });
        b.Entity<PlatformTurn>().ToTable("AiTurns");
        b.Entity<PlatformTurn>().HasOne<PlatformConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PlatformTurn>().HasIndex(x => new { x.ConversationId, x.ClientRequestId }).IsUnique();
        b.Entity<PlatformUsage>().ToTable("AiUsage");
        b.Entity<PlatformUsage>().HasIndex(x => new { x.AssistantId, x.CreatedAt });
        b.Entity<PlatformUsage>().HasIndex(x => new { x.SubjectHash, x.CreatedAt });
        b.Entity<PlatformUsage>().Property(x => x.EstimatedCost).HasPrecision(18, 8);
        b.Entity<PlatformAudit>().ToTable("AiSecurityAudit");
        b.Entity<PlatformAudit>().HasIndex(x => x.CreatedAt);
        b.Entity<PlatformGate>().ToTable("AiAdmissionGates");
        b.Entity<PlatformGate>().Property(x => x.Revision).IsConcurrencyToken();
        foreach (var type in b.Model.GetEntityTypes())
            foreach (var property in type.GetProperties().Where(x => x.ClrType == typeof(string)))
                property.SetMaxLength(property.Name.StartsWith("Protected", StringComparison.Ordinal) ? null : 256);
    }
}
