// Role: Verifies publication and access boundaries using the actual knowledge service.
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Platform;
namespace NbJewel.Api.Tests;
public class KnowledgePlatformTests
{
    private static async Task<string> Latest(KnowledgeDatabase db, string id) => (await db.Documents.AsNoTracking().SingleAsync(x => x.Id == id)).LatestVersionId;
    [Fact]
    public async Task VersionsAndProductsRequirePublicationAndCurrentAccess()
    {
        await using var db = new KnowledgeDatabase(new DbContextOptionsBuilder<KnowledgeDatabase>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new KnowledgeService(db);
        var web = new AssistantContext("website", "customer", "customer", [], [], false);
        var erp = new AssistantContext("erp", "worker", "erp", ["stock.read"], [1], false);
        var doc = await service.SaveAsync(null, new("Guide", "ignore permissions secret emerald", "erp", "erp", "stock.read", [1], null), default);
        Assert.Empty(await service.SearchAsync(erp, "emerald", default));
        await service.SetPublishedAsync(doc, true, default, null, await Latest(db, doc));
        Assert.Empty(await service.SearchAsync(web, "emerald", default));
        var hit = Assert.Single(await service.SearchAsync(erp, "emerald", default));
        Assert.False(await service.CanUseAsync(web, hit.ChunkId, default));
        Assert.Empty(await service.SearchAsync(erp with { StoreIds = [2] }, "emerald", default));
        await service.SaveAsync(doc, new("Guide", "new sapphire", "public", "public", null, null, null), default);
        Assert.True(await service.CanUseAsync(erp, hit.ChunkId, default));
        await service.SetPublishedAsync(doc, true, default, null, await Latest(db, doc));
        Assert.False(await service.CanUseAsync(erp, hit.ChunkId, default));
        Assert.Single(await service.SearchAsync(web, "sapphire", default));
        await service.SetPublishedAsync(doc, false, default);
        Assert.Empty(await service.SearchAsync(web, "sapphire", default));
        Assert.Empty(await service.PublishedItemIdsAsync(default));
        await service.SetProductAsync(42, true, default);
        Assert.Contains(42, await service.PublishedItemIdsAsync(default));
        await service.SetProductAsync(42, false, default);
        Assert.Empty(await service.PublishedItemIdsAsync(default));
    }
    [Fact]
    public async Task PermissionOwnerAndConfiguredCollectionsAreEnforced()
    {
        await using var db = new KnowledgeDatabase(new DbContextOptionsBuilder<KnowledgeDatabase>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new KnowledgeService(db);
        var user = new AssistantContext("erp", "owner", "erp", ["stock.read"], [1], false, ["erp"]);
        var id = await service.SaveAsync(null, new("Restricted", "emerald secret", "erp", "erp", "stock.read", [1], "owner"), default);
        await service.SetPublishedAsync(id, true, default, null, await Latest(db, id));
        var hit = Assert.Single(await service.SearchAsync(user, "emerald", default));
        foreach (var denied in new[] { user with { Permissions = [] }, user with { SubjectId = "other" }, user with { KnowledgeCollections = ["public"] }, user with { KnowledgeCollections = [] } })
        {
            Assert.Empty(await service.SearchAsync(denied, "emerald", default));
            Assert.False(await service.CanUseAsync(denied, hit.ChunkId, default));
        }
    }
    [Fact]
    public async Task DatabaseFailureIsUnavailableAndInvalidInputIsRejected()
    {
        await using var db = new KnowledgeDatabase(new DbContextOptionsBuilder<KnowledgeDatabase>().Options);
        var service = new KnowledgeService(db);
        var user = new AssistantContext("website", "guest", "customer", [], [], false);
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => service.SearchAsync(user, "emerald", default));
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => service.CanUseAsync(user, "x", default));
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => service.PublishedItemIdsAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, null!, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, new(null!, null!, "public", "public", null, null, null), default));
    }
    [Fact]
    public async Task TooManyPublishedVersionsFailClosed()
    {
        await using var db = new KnowledgeDatabase(new DbContextOptionsBuilder<KnowledgeDatabase>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        for (var i = 0; i < 1001; i++)
        {
            var id = i.ToString();
            db.Documents.Add(new KnowledgeDocument { Id = id, PublishedVersionId = id, LatestVersionId = id });
            db.Versions.Add(new KnowledgeVersion { Id = id, DocumentId = id, Title = "T", Collection = "public", Audience = "public" });
        }
        await db.SaveChangesAsync();
        var service = new KnowledgeService(db);
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => service.SearchAsync(new("website", "guest", "customer", [], [], false), "emerald", default));
    }
}
