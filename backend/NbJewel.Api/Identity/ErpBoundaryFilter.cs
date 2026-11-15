// Role: Enforces deterministic permissions and denies endpoints whose queries cannot honor store scope.
using System.Security.Claims;
using NbJewel.Api.Businesses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NbJewel.Api.Identity;

public sealed class ErpBoundaryFilter(BusinessContext business) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var action = context.ActionDescriptor.RouteValues;
        action.TryGetValue("controller", out var controller);
        action.TryGetValue("action", out var name);
        string[] required = (controller, name) switch
        {
            ("ItemImages", "Review") => ["items.read", "items.write", "prices.read", "status.read", "knowledge.manage"],
            ("ItemImages", "Upload" or "Analyze") => ["items.read", "items.write", "prices.read", "status.read"],
            ("ItemImages", _) => ["items.read", "prices.read", "status.read"],
            ("Items", "GetStock") => ["items.read", "stock.read"],
            ("Items", "AddStock") => ["items.read", "stock.write", "stock.read"],
            ("Items", "GetPrice" or "GetVariants") => ["items.read", "prices.read"],
            ("Items", "UpdateStatus") => ["items.read", "prices.read", "status.read", "status.write"],
            ("Items", "Create" or "Update") => ["items.read", "items.write", "prices.read", "status.read"],
            ("Items", _) => ["items.read", "prices.read", "status.read"],
            ("ReferenceData", "GetLowStock") => ["reference.read", "stock.read"],
            ("ReferenceData", "GetLatestRates") => ["reference.read", "prices.read"],
            ("ReferenceData", _) => ["reference.read"],
            ("Chat", _) => ["chat.read"],
            ("Jewelry", _) when name != "GetActive" => ["jewelry.manage"],
            _ => []
        };
        if (required.Length == 0)
        {
            // These endpoints perform their own identity/resource checks, or are explicitly public.
            if (controller is "CustomerPortal" or "Auth" or "Knowledge" or "AiConversations" || (controller == "Jewelry" && name == "GetActive")) return;
            if (controller == "PublicCatalog" && HttpMethods.IsGet(context.HttpContext.Request.Method) &&
                name is "GetStores" or "GetSpotRates" or "GetProducts" or "GetProduct" or "GetMetalRates" or "GetPreview") return;
            context.Result = new ForbidResult();
            return;
        }
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (!await business.ResolveErpAsync(context.HttpContext, context.HttpContext.RequestAborted))
        {
            context.Result = business.Membership is not null && context.HttpContext.Request.RouteValues.ContainsKey("slug") ? new NotFoundResult() : new ForbidResult();
            return;
        }
        var access = business.Access;
        var requiresAllStores = controller is "Jewelry" or "ItemImages" || (controller == "Items" &&
            !HttpMethods.IsGet(context.HttpContext.Request.Method) && name != "AddStock");
        if (access is null || access.AccountType != "erp" || (requiresAllStores && !access.AllStores) || !required.All(access.Permissions.Contains))
            context.Result = new ForbidResult();
        else context.HttpContext.Items["ErpAccess"] = access;
    }
}
