// Role: Proves assumed editor choices and private preview branch data require portal management and the matching business membership.
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Businesses;
using NbJewel.Api.Identity;
namespace NbJewel.Api.Tests;
public sealed class PortalEditorChoicesTests
{
    [Fact]
    public async Task EditorChoicesAndPreviewStoresResolveOnlyTheMembersBusiness()
    {
        await using var factory = new KnowledgeHttpFactory(); using var staff = await PortalLifecycleHttpTests.Admin(factory, false);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IdentityDatabase>(); (await db.Set<BusinessMembership>().SingleAsync()).BusinessId = "sample-wholesale"; await db.SaveChangesAsync(); }
        var root = "/api/businesses/sample-wholesale/portal";
        var choices = await staff.GetAsync(root + "/choices"); Assert.Equal(HttpStatusCode.OK, choices.StatusCode); var body = await choices.Content.ReadAsStringAsync(); Assert.Contains("Assumed wholesale branch", body); Assert.DoesNotContain("Mumbai", body);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync(root + "/stores")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.GetAsync(root + "/preview?storeId=2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/api/businesses/nb-jewel-demo/portal/choices")).StatusCode);
        using var guest = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(root + "/choices")).StatusCode);
    }
}
