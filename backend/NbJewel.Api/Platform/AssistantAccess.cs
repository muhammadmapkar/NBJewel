// Role: Resolves server identities and signs opaque visitor cookies without accepting client-selected permissions.
using System.Net;
using NbJewel.Api.Businesses;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using NbJewel.Api.Chat;
using NbJewel.Api.Identity;

namespace NbJewel.Api.Platform;

public sealed class AssistantAccess(IPermissionAdapter permissions, IDataProtectionProvider protection,
    AssistantRegistry registry, IWebHostEnvironment environment, BusinessMemberships? memberships = null, BusinessContext? business = null, PortalPolicy? portals = null)
{
    private readonly IDataProtector visitor = protection.CreateProtector("nbjewel.ai.visitor.v1");
    public async Task<AssistantContext> ResolveAsync(string assistant, HttpContext http, CancellationToken ct)
    {
        if (assistant is not ("erp" or "website")) throw new KeyNotFoundException();
        var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var account = subject is null ? null : await permissions.ResolveAsync(subject, ct);
            if (account is null) throw new AssistantDeniedException();
            if (assistant == "erp")
            {
                var member = memberships is null ? null : await memberships.ResolveAsync(account, ct);
                if (memberships is not null && member is null) throw new AssistantDeniedException();
                if (member is not null) account = BusinessMemberships.Effective(account, member);
                if (account.AccountType != "erp" || !account.Permissions.Contains("chat.read")) throw new AssistantDeniedException();
                return new(assistant, account.Id, account.AccountType, account.Permissions, account.StoreIds,
                    account.AllStores, registry.Get(assistant).KnowledgeCollections.ToArray(), BusinessId: member?.Slug ?? BusinessRegistry.DemoSlug);
            }
            // An ERP account visiting the website receives exactly the public customer context.
            return await PublicContext(new(assistant, account.Id, "customer", [], [], false, registry.Get(assistant).KnowledgeCollections.ToArray()), ct);
        }
        if (assistant == "erp") throw new AssistantDeniedException();
        string? id = null;
        if (http.Request.Cookies.TryGetValue("nbjewel.visitor", out var cookie))
        {
            try { var raw = visitor.Unprotect(cookie); if (Guid.TryParseExact(raw, "N", out _)) id = raw; }
            catch (CryptographicException) { }
        }
        if (id is null)
        {
            id = Guid.NewGuid().ToString("N");
            http.Response.Cookies.Append("nbjewel.visitor", visitor.Protect(id), new CookieOptions
            {
                HttpOnly = true, Secure = !environment.IsDevelopment() || http.Request.IsHttps,
                SameSite = SameSiteMode.Strict, Path = "/api/ai/website", MaxAge = TimeSpan.FromDays(7), IsEssential = true
            });
        }
        return await PublicContext(new(assistant, "visitor:" + id, "visitor", [], [], false, registry.Get(assistant).KnowledgeCollections.ToArray(),
            AddressKey(http.Connection.RemoteIpAddress)), ct);
    }

    // Network key for abuse limits. RemoteIpAddress is already the real client address when the request came through a trusted proxy.
    // IPv6 callers share a key per /64 so rotating addresses inside one network does not reset a limit.
    public static string AddressKey(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.AddressFamily == AddressFamily.InterNetworkV6 ? Convert.ToHexString(address.GetAddressBytes()[..8]) + "/64" : address.ToString();
    }

    // Deleting your own transcript must keep working after permissions, stores or collections change, so this proves identity only.
    // The owner hash binds assistant, account type and subject, so a caller can only ever match conversations that subject created.
    // It never creates a visitor cookie and grants no tool, store or knowledge access.
    public AssistantContext ResolveForDeletion(string assistant, HttpContext http)
    {
        if (assistant is not ("erp" or "website")) throw new KeyNotFoundException();
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(subject)) throw new AssistantDeniedException();
            return new(assistant, subject, assistant == "erp" ? "erp" : "customer", [], [], false, BusinessId: assistant == "erp" ? business?.Business?.Slug ?? BusinessRegistry.DemoSlug : BusinessRegistry.DemoSlug);
        }
        if (assistant == "erp") throw new AssistantDeniedException();
        if (http.Request.Cookies.TryGetValue("nbjewel.visitor", out var cookie))
        {
            try { var raw = visitor.Unprotect(cookie); if (Guid.TryParseExact(raw, "N", out _)) return new(assistant, "visitor:" + raw, "visitor", [], [], false); }
            catch (CryptographicException) { }
        }
        throw new KeyNotFoundException();
    }

    public async Task<AssistantContext> RefreshAsync(AssistantContext context, CancellationToken ct)
    {
        if (context.AccountType == "visitor") return await PublicContext(context, ct);
        var account = await permissions.ResolveAsync(context.SubjectId, ct);
        if (account is null) throw new AssistantDeniedException();
        if (context.AssistantId == "website") return await PublicContext(context, ct);
        var member = memberships is null ? null : await memberships.ResolveAsync(account, ct);
        if (memberships is not null && (member is null || member.Slug != context.BusinessId)) throw new AssistantDeniedException();
        if (member is not null) account = BusinessMemberships.Effective(account, member);
        if (account.AccountType != "erp" || !account.Permissions.Contains("chat.read")) throw new AssistantDeniedException();
        return context with { Permissions = account.Permissions, StoreIds = account.StoreIds, AllStores = account.AllStores };
    }

    private async Task<AssistantContext> PublicContext(AssistantContext context, CancellationToken ct)
    {
        if (portals is null) return context;
        var stamp = await portals.PublicStampAsync(ct) ?? throw new KeyNotFoundException();
        return context with { PublicPolicyVersion = stamp };
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Owner(AssistantContext c) => Hash(c.BusinessId + ":" + c.AssistantId + ":" + c.AccountType + ":" + c.SubjectId);
    public static string Fingerprint(AssistantContext c) => Hash(c.BusinessId + ":" + c.AccountType + ":" + c.AllStores + ":" +
        string.Join(',', c.Permissions.Order()) + ":" + string.Join(',', c.StoreIds.Order()) + ":" + string.Join(',', (c.KnowledgeCollections ?? []).Order()) + ":" + c.PublicPolicyVersion);
}
