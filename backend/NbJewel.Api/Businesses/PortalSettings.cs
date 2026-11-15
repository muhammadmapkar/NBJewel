// Role: Defines assumed fixed-template portal settings, revisions and audit entries; no arbitrary markup or styles.
namespace NbJewel.Api.Businesses;
public sealed record PortalSettings
{
    public string Name { get; init; } = "";
    public string ColorPreset { get; init; } = "gold";
    public string? LogoAssetId { get; init; }
    public string? BannerAssetId { get; init; }
    public string Headline { get; init; } = "";
    public string Introduction { get; init; } = "";
    public string Story { get; init; } = "";
    public bool ShowBanner { get; init; } = true;
    public bool ShowCollections { get; init; } = true;
    public bool ShowFeatured { get; init; } = true;
    public bool ShowStory { get; init; } = true;
    public bool ShowStores { get; init; } = true;
    public bool ShowMarketRates { get; init; } = true;
    public bool ShowPrices { get; init; } = true;
    public bool Enquiries { get; init; }
    public bool TryOn { get; init; } = true;
    public bool Bag { get; init; } = true;
    public bool Reservations { get; init; } = true;
    public string Phone { get; init; } = "";
    public string WhatsApp { get; init; } = "";
    public string Email { get; init; } = "";
    public string OpeningHours { get; init; } = "";
    public string[] VisibleCollections { get; init; } = [];
    public int[] FeaturedItemIds { get; init; } = [];
    public int[] VisibleBranchIds { get; init; } = [];
    public static PortalSettings Preset(Business business, int[] branches) => new() {
        Name = business.Name, Headline = business.Name, Introduction = "Assumed sample portal content.",
        VisibleBranchIds = branches, ShowPrices = business.Kind == "retailer", Enquiries = business.Kind == "wholesaler",
        TryOn = business.Kind == "retailer", Bag = business.Kind == "retailer", Reservations = business.Kind == "retailer" && branches.Length > 0
    };
}
public sealed record PortalRevision(long Revision, PortalSettings Settings, string ActorId, DateTimeOffset At);
public sealed record PortalAudit(string Business, string ActorId, string Action, long Revision, DateTimeOffset At);
public sealed record PortalDocument(PortalRevision Draft, PortalRevision? Publication, bool Online, List<PortalAudit> Audit);
public sealed class PortalConflictException : Exception;
public sealed class PortalValidationException(string message) : Exception(message);
