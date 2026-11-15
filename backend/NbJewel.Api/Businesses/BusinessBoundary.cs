// Role: Resolves trusted business context before any controller or repository is constructed (assumed v1 routing).
namespace NbJewel.Api.Businesses;
public static class BusinessBoundary
{
    public static async Task InvokeAsync(HttpContext http, RequestDelegate next)
    {
        var controller = http.Request.RouteValues["controller"]?.ToString();
        var action = http.Request.RouteValues["action"]?.ToString();
        var erp = controller is "Items" or "ItemImages" or "ReferenceData" or "Chat" or "Knowledge" or "Portal" ||
            (controller == "Jewelry" && action != "GetActive") || (controller == "AiConversations" && http.Request.RouteValues["assistant"]?.ToString() == "erp");
        var business = http.RequestServices.GetRequiredService<BusinessContext>();
        if (erp)
        {
            if (http.User.Identity?.IsAuthenticated != true) { http.Response.StatusCode = 401; return; }
            if (!await business.ResolveErpAsync(http, http.RequestAborted))
            { http.Response.StatusCode = business.Membership is not null && http.Request.RouteValues.ContainsKey("slug") ? 404 : 403; return; }
        }
        else
        {
            var slug = controller is "Shops" or "PublicCatalog" or "CustomerShops" or "CustomerPortal" ? http.Request.RouteValues["slug"]?.ToString() : null;
            if (!business.ResolvePublic(slug ?? BusinessRegistry.DemoSlug)) { http.Response.StatusCode = 404; return; }
        }
        await next(http);
    }
}
