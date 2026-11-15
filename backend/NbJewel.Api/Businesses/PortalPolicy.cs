// Role: Reads the current assumed live portal policy per request; only authenticated portal management can select a draft preview.
using NbJewel.Api.Platform;
namespace NbJewel.Api.Businesses;
public sealed class PortalPolicy(BusinessContext context, PortalStore store)
{
    private PortalRevision? preview;
    public Business Business => context.Business ?? throw new InvalidOperationException("Business context is missing.");
    public bool IsPreview => preview is not null;
    public void UsePreview(PortalRevision draft)
    {
        if (context.Membership is null || context.Access?.Permissions.Contains("portal.manage") != true) throw new AssistantDeniedException();
        preview = draft;
    }
    public async Task<PortalRevision?> RevisionAsync(CancellationToken ct)
    {
        if (preview is not null) return preview;
        var document = await store.ReadAsync(Business, ct);
        return document.Online ? document.Publication : null;
    }
    public async Task<PortalSettings?> SettingsAsync(CancellationToken ct) => (await RevisionAsync(ct))?.Settings;
    public async Task<string?> PublicStampAsync(CancellationToken ct)
    {
        var revision = await RevisionAsync(ct);
        return revision is null ? null : Business.Slug + ":" + revision.Revision + ":" + revision.Settings.ShowPrices;
    }
}
