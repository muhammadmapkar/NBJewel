// Role: Validates assumed portal revisions against the owning business and publishes without exposing draft assets or incomplete products.
using System.Text.RegularExpressions;
using NbJewel.Api.Models;
using NbJewel.Api.Platform;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Businesses;
public sealed class PortalService(PortalStore store, IItemRepository items, IPublicationReader publications)
{
    public static void ValidateDraft(PortalSettings s)
    {
        static bool Text(string? value, int max) => value is not null && value.Length <= max && !value.Any(c => c is '<' or '>' || (char.IsControl(c) && c is not ('\r' or '\n' or '\t')));
        if (s is null || !Text(s.Name, 120) || !Text(s.ColorPreset, 20) || !Text(s.Headline, 200) || !Text(s.Introduction, 2000) || !Text(s.Story, 5000) || !Text(s.Phone, 40) || !Text(s.WhatsApp, 40) || !Text(s.Email, 254) || !Text(s.OpeningHours, 500) ||
            s.FeaturedItemIds is not { Length: <= 12 } || s.FeaturedItemIds.Any(x => x <= 0) || s.FeaturedItemIds.Distinct().Count() != s.FeaturedItemIds.Length || s.VisibleBranchIds is not { Length: <= 100 } || s.VisibleBranchIds.Any(x => x <= 0) || s.VisibleBranchIds.Distinct().Count() != s.VisibleBranchIds.Length || s.VisibleCollections is not { Length: <= 100 } || s.VisibleCollections.Any(x => !Text(x, 100)) ||
            (s.LogoAssetId is not null && !Guid.TryParseExact(s.LogoAssetId, "N", out _)) || (s.BannerAssetId is not null && !Guid.TryParseExact(s.BannerAssetId, "N", out _))) throw new PortalValidationException("Check the portal text, fixed settings and references.");
    }
    public Task<PortalDocument> SaveAsync(Business business, long revision, PortalSettings settings, string actor, CancellationToken ct)
    {
        ValidateDraft(settings);
        return store.ChangeAsync(business, document => {
            if (document.Draft.Revision != revision) throw new PortalConflictException();
            var draft = new PortalRevision(checked(revision + 1), settings, actor, DateTimeOffset.UtcNow);
            document.Audit.Add(new(business.Slug, actor, "save", draft.Revision, draft.At));
            return Task.FromResult(document with { Draft = draft });
        }, ct);
    }
    public Task<PortalDocument> PublishAsync(Business business, long revision, string actor, bool online, CancellationToken ct) => store.ChangeAsync(business, async document => {
        if (document.Draft.Revision != revision) throw new PortalConflictException();
        if (online) await ValidatePublication(business, document.Draft.Settings, ct);
        var now = DateTimeOffset.UtcNow; document.Audit.Add(new(business.Slug, actor, online ? "publish" : "offline", revision, now));
        return document with { Publication = online ? document.Draft with { ActorId = actor, At = now } : document.Publication, Online = online };
    }, ct);
    private async Task ValidatePublication(Business business, PortalSettings s, CancellationToken ct)
    {
        ValidateDraft(s);
        if (string.IsNullOrWhiteSpace(s.Name) || s.ColorPreset is not ("gold" or "silver" or "rose")) throw new PortalValidationException("Choose a business name and approved color preset.");
        if (s.Enquiries && !Regex.IsMatch(s.WhatsApp, "^\\+[1-9][0-9]{7,14}$", RegexOptions.CultureInvariant)) throw new PortalValidationException("Enter a WhatsApp number in E.164 format when enquiries are enabled.");
        if ((s.LogoAssetId is not null && store.Asset(business, s.LogoAssetId) is null) || (s.BannerAssetId is not null && store.Asset(business, s.BannerAssetId) is null)) throw new PortalValidationException("Choose this business's uploaded assets.");
        var branches = (await items.GetStoresAsync(ct)).Select(x => x.Id).ToHashSet();
        if (s.VisibleBranchIds.Any(id => !branches.Contains(id)) || (s.Reservations && s.VisibleBranchIds.Length == 0)) throw new PortalValidationException("Choose available branches for reservation requests.");
        var published = await publications.PublishedItemIdsAsync(ct); var categories = await PublicCatalogPolicy.AllowedCategoriesAsync(items, ct);
        foreach (var id in s.FeaturedItemIds) { var item = await items.GetItemAsync(id, ct); if (!published.Contains(id) || item is null || !PublicCatalogPolicy.Allows(item, categories)) throw new PortalValidationException("Choose currently published, eligible featured products."); }
        if (s.VisibleCollections.Length > 0)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var page = 1; ; page++) { var batch = await items.SearchItemsAsync(null, null, null, null, ItemStatusRules.Complete, page, 100, ct); if (batch.TotalCount > 10000) throw new PortalValidationException("Catalog is too large."); foreach (var item in batch.Items) if (published.Contains(item.Id) && PublicCatalogPolicy.Allows(item, categories) && item.Storefront is not null) names.Add(item.Storefront.Collection); if (page * 100 >= batch.TotalCount) break; if (batch.Items.Count == 0) throw new PortalValidationException("Catalog is incomplete."); }
            if (s.VisibleCollections.Any(x => !names.Contains(x))) throw new PortalValidationException("Choose currently published collections from this business.");
        }
    }
}
