// Role: Holds only trusted route and membership business context for one request (assumed v1 boundary).
using System.Security.Claims;
using NbJewel.Api.Identity;
namespace NbJewel.Api.Businesses;

public sealed class BusinessContext(BusinessRegistry registry, BusinessMemberships memberships, IPermissionAdapter permissions)
{
    private string? boundBusinessId;
    public Business? Business { get; private set; }
    public BusinessAccess? Membership { get; private set; }
    public AccountAccess? Access { get; private set; }
    public async Task<bool> ResolveErpAsync(HttpContext http, CancellationToken ct)
    {
        Business = null; Membership = null; Access = null;
        if (http.User.Identity?.IsAuthenticated != true) return false;
        var id = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var access = id is null ? null : await permissions.ResolveAsync(id, ct);
        var member = access is null ? null : await memberships.ResolveAsync(access, ct);
        if (member is null) return false;
        Membership = member;
        if (boundBusinessId is not null && boundBusinessId != member.Slug) return false;
        var slug = http.Request.RouteValues["slug"]?.ToString();
        if (slug is not null && slug != member.Slug) return false;
        boundBusinessId = member.Slug;
        Business = registry.Find(member.Slug);
        Access = BusinessMemberships.Effective(access!, member);
        return Business is not null;
    }
    public bool ResolvePublic(string slug)
    {
        if (boundBusinessId is not null && boundBusinessId != slug) throw new InvalidOperationException("A request cannot change business partitions.");
        boundBusinessId = slug;
        Business = registry.Find(slug);
        Membership = null; Access = null;
        return Business is not null;
    }
}
