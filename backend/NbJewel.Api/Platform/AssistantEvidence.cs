// Role: Revalidates every piece of tool evidence against current ACLs, publication and configured tool policies.
using NbJewel.Api.Chat;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Platform;

// Remembers answers for one request only (for example one history read), so a source repeated across messages is checked once.
// Create a new instance per request: it must never outlive the permission state it was computed from.
public sealed class EvidenceCheckCache
{
    internal Dictionary<string, bool> Results { get; } = [];
    internal HashSet<string>? Allowed { get; set; }
    internal IReadOnlySet<int>? Published { get; set; }
    internal IReadOnlyDictionary<int, string>? Categories { get; set; }
}

public sealed class AssistantEvidence(IKnowledgeEvidenceValidator knowledge, IPublicationReader publications,
    IAssistantTools tools, IItemRepository repository, AssistantRegistry registry)
{
    public async Task<bool> CanUseAsync(AssistantContext ctx, IReadOnlyList<ChatSource> sources, CancellationToken ct, EvidenceCheckCache? cache = null)
    {
        if (sources.Count > 512) return false;
        cache ??= new EvidenceCheckCache();
        if (cache.Allowed is null)
        {
            var configured = registry.Get(ctx.AssistantId).AllowedTools;
            cache.Allowed = tools.Declarations(ctx).Where(x => configured.Contains(x.Name)).Select(x => x.Name).ToHashSet();
        }
        foreach (var source in sources)
        {
            if (!cache.Allowed.Contains(source.Tool)) return false;
            var key = source.Tool + "|" + source.RecordId;
            if (!cache.Results.TryGetValue(key, out var ok)) cache.Results[key] = ok = await CheckAsync(ctx, source, cache, ct);
            if (!ok) return false;
        }
        return true;
    }

    private async Task<bool> CheckAsync(AssistantContext ctx, ChatSource source, EvidenceCheckCache cache, CancellationToken ct)
    {
        if (source.Tool == "search_knowledge") return await knowledge.CanUseAsync(ctx, source.RecordId, ct);
        if (ctx.AssistantId == "website")
        {
            cache.Published ??= await publications.PublishedItemIdsAsync(ct);
            if (!int.TryParse(source.RecordId, out var id) || !cache.Published.Contains(id)) return false;
            var item = await repository.GetItemAsync(id, ct);
            cache.Categories ??= await PublicCatalogPolicy.AllowedCategoriesAsync(repository, ct);
            return item is not null && PublicCatalogPolicy.Allows(item, cache.Categories);
        }
        if (!ctx.AllStores && source.Tool is not ("list_categories" or "get_latest_metal_rate"))
            return int.TryParse(source.RecordId, out var id) && (await repository.GetStockAsync(id, ct)).Any(x => ctx.StoreIds.Contains(x.StoreId));
        return true;
    }
}
