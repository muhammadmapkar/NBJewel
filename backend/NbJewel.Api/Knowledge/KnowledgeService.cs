// Role: Manages approved knowledge versions and enforces retrieval access rules.
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using NbJewel.Api.Platform;
namespace NbJewel.Api.Knowledge;

public sealed class KnowledgeService(KnowledgeDatabase db) : IKnowledgeRetriever, IPublicationReader, IKnowledgeEvidenceValidator
{
    private static string Id() => Guid.NewGuid().ToString("N");
    private void Audit(string? actor, string operation, string target) => db.Audits.Add(new KnowledgeAudit
    {
        Id = Id(), ActorHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actor ?? "system"))),
        Operation = operation, TargetId = target, Timestamp = DateTimeOffset.UtcNow
    });
    private static bool Allowed(AssistantContext c, KnowledgeVersion v)
    {
        if (c.KnowledgeCollections is not null && !c.KnowledgeCollections.Contains(v.Collection)) return false;
        if (c.AssistantId != "website" && c.AssistantId != "erp") return false;
        if (v.Collection != "public" && !(c.AssistantId == "erp" && v.Collection == "erp")) return false;
        if (v.OwnerId is not null && v.OwnerId != c.SubjectId) return false;
        var stores = v.StoreIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (stores.Length > 0 && !(c.AllStores || stores.All(s => int.TryParse(s, out var id) && c.StoreIds.Contains(id)))) return false;
        if (!string.IsNullOrWhiteSpace(v.RequiredPermission) && !c.Has(v.RequiredPermission)) return false;
        if (v.Audience == "public") return v.Collection == "public";
        if (v.Audience != "erp" || c.AssistantId != "erp" || c.AccountType != "erp" || string.IsNullOrWhiteSpace(v.RequiredPermission) || !c.Has(v.RequiredPermission)) return false;
        return stores.Length > 0;
    }
    private static void Validate(KnowledgeInput i)
    {
        if (i is null || string.IsNullOrWhiteSpace(i.Title) || i.Title.Length > 200 || string.IsNullOrWhiteSpace(i.Text) || i.Text.Length > 100000 || (i.Collection != "public" && i.Collection != "erp") || (i.Audience != "public" && i.Audience != "erp") || (i.Audience == "public" && i.Collection != "public") || (i.Audience == "erp" && (string.IsNullOrWhiteSpace(i.RequiredPermission) || i.StoreIds is not { Length: > 0 })) || i.StoreIds?.Any(x => x <= 0) == true || i.OwnerId?.Length > 256 || i.RequiredPermission?.Length > 256) throw new ArgumentException("Invalid knowledge document.");
    }
    public async Task<string> SaveAsync(string? documentId, KnowledgeInput input, CancellationToken ct, string? actor = null)
    {
        Validate(input);
        var doc = documentId is null ? new KnowledgeDocument { Id = Id() } : await db.Documents.SingleOrDefaultAsync(x => x.Id == documentId, ct) ?? throw new KeyNotFoundException();
        if (documentId is null) db.Documents.Add(doc);
        var version = new KnowledgeVersion { Id = Id(), DocumentId = doc.Id, Title = input.Title.Trim(), Collection = input.Collection, Audience = input.Audience, RequiredPermission = input.RequiredPermission, StoreIds = string.Join(',', input.StoreIds ?? []), OwnerId = string.IsNullOrWhiteSpace(input.OwnerId) ? null : input.OwnerId };
        doc.LatestVersionId = version.Id; db.Versions.Add(version);
        for (int offset = 0; offset < input.Text.Length; offset += 900) db.Chunks.Add(new KnowledgeChunk { Id = Id(), VersionId = version.Id, Text = input.Text.Substring(offset, Math.Min(1000, input.Text.Length - offset)) });
        Audit(actor, documentId is null ? "document.import" : "document.update", doc.Id);
        await db.SaveChangesAsync(ct); return doc.Id;
    }
    // Publishing names the exact version the admin approved. If a newer save has landed since, the publish is refused instead of
    // silently publishing text nobody reviewed. The concurrency tokens on the document row also catch a save that lands mid-publish.
    public async Task SetPublishedAsync(string id, bool published, CancellationToken ct, string? actor = null, string? versionId = null)
    {
        if (published && string.IsNullOrWhiteSpace(versionId)) throw new ArgumentException("Publishing needs the version that was approved.");
        var d = await db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        if (published && d.LatestVersionId != versionId) throw new KnowledgeVersionConflictException();
        d.PublishedVersionId = published ? versionId : null;
        Audit(actor, published ? "document.publish" : "document.revoke", id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new KnowledgeVersionConflictException(); }
    }
    public async Task SetProductAsync(int id, bool published, CancellationToken ct, string? actor = null)
    { if (id <= 0) throw new ArgumentException("Invalid item ID."); var p = await db.Products.FindAsync([id], ct); if (p is null) db.Products.Add(new ProductPublication { ItemId = id, Published = published }); else p.Published = published; Audit(actor, published ? "product.publish" : "product.revoke", id.ToString(System.Globalization.CultureInfo.InvariantCulture)); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlySet<int>> PublishedItemIdsAsync(CancellationToken ct)
    { try { return (await db.Products.Where(x => x.Published).Select(x => x.ItemId).ToListAsync(ct)).ToHashSet(); } catch (Exception e) when (e is not OperationCanceledException) { throw new AssistantUnavailableException(); } }
    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(AssistantContext context, string query, CancellationToken ct)
    {
        try {
            var terms = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => x.Length >= 2).Take(8).ToArray();
            if (terms.Length == 0) return [];
            var versions = await (from d in db.Documents join v in db.Versions on d.PublishedVersionId equals v.Id select v).AsNoTracking().Take(1001).ToListAsync(ct);
            if (versions.Count > 1000) throw new AssistantUnavailableException();
            var allowed = versions.Where(v => Allowed(context, v)).ToDictionary(v => v.Id);
            if (allowed.Count == 0) return [];
            var ids = allowed.Keys.ToArray();
            var chunks = db.Chunks.AsNoTracking().Where(c => ids.Contains(c.VersionId));
            // Limit the SQL candidate set before lexical ranking.
            var candidates = new List<KnowledgeChunk>();
            foreach (var term in terms) candidates.AddRange(await chunks.Where(c => c.Text.Contains(term)).Take(100).ToListAsync(ct));
            return candidates.DistinctBy(c => c.Id).Select(c => new { Chunk = c, Score = terms.Count(t => c.Text.Contains(t, StringComparison.OrdinalIgnoreCase)) }).OrderByDescending(x => x.Score).ThenBy(x => x.Chunk.Id).Take(5).Select(x => { var v = allowed[x.Chunk.VersionId]; return new KnowledgeHit(v.DocumentId, v.Id, x.Chunk.Id, v.Title, x.Chunk.Text); }).ToArray();
        } catch (Exception e) when (e is not OperationCanceledException) { throw new AssistantUnavailableException(); }
    }
    public async Task<bool> CanUseAsync(AssistantContext context, string chunkId, CancellationToken ct)
    { try { var version = await (from c in db.Chunks join v in db.Versions on c.VersionId equals v.Id join d in db.Documents on v.Id equals d.PublishedVersionId where c.Id == chunkId select v).AsNoTracking().SingleOrDefaultAsync(ct); return version is not null && Allowed(context, version); } catch (Exception e) when (e is not OperationCanceledException) { throw new AssistantUnavailableException(); } }
}

public sealed class KnowledgeVersionConflictException : Exception;
