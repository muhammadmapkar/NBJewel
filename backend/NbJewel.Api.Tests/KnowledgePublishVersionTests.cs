// Role: Proves a document is published by the exact version an admin approved, and that a concurrent save cannot slip into a publish.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Knowledge;
using Xunit;

namespace NbJewel.Api.Tests;

public class KnowledgePublishVersionTests
{
    private static KnowledgeInput Input(string text) => new("Guide", text, "public", "public", null, null, null);
    private static DbContextOptions<KnowledgeDatabase> Shared(string name) => new DbContextOptionsBuilder<KnowledgeDatabase>().UseInMemoryDatabase(name).Options;

    [Fact]
    public async Task Publishing_an_older_approved_version_after_a_newer_save_is_refused()
    {
        await using var db = new KnowledgeDatabase(Shared(Guid.NewGuid().ToString())); var service = new KnowledgeService(db);
        var doc = await service.SaveAsync(null, Input("first draft"), default);
        var approved = (await db.Documents.SingleAsync(x => x.Id == doc)).LatestVersionId;
        await service.SaveAsync(doc, Input("second draft nobody reviewed"), default);
        await Assert.ThrowsAsync<KnowledgeVersionConflictException>(() => service.SetPublishedAsync(doc, true, default, null, approved));
        Assert.Null((await db.Documents.SingleAsync(x => x.Id == doc)).PublishedVersionId);
    }

    [Fact]
    public async Task Publishing_the_current_version_works_and_publishes_exactly_that_version()
    {
        await using var db = new KnowledgeDatabase(Shared(Guid.NewGuid().ToString())); var service = new KnowledgeService(db);
        var doc = await service.SaveAsync(null, Input("first draft"), default);
        await service.SaveAsync(doc, Input("second draft"), default);
        var latest = (await db.Documents.SingleAsync(x => x.Id == doc)).LatestVersionId;
        await service.SetPublishedAsync(doc, true, default, null, latest);
        Assert.Equal(latest, (await db.Documents.SingleAsync(x => x.Id == doc)).PublishedVersionId);
    }

    [Fact]
    public async Task Publishing_without_naming_a_version_is_rejected()
    {
        await using var db = new KnowledgeDatabase(Shared(Guid.NewGuid().ToString())); var service = new KnowledgeService(db);
        var doc = await service.SaveAsync(null, Input("first draft"), default);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetPublishedAsync(doc, true, default));
        Assert.Null((await db.Documents.SingleAsync(x => x.Id == doc)).PublishedVersionId);
    }

    [Fact]
    public async Task Revoking_needs_no_version()
    {
        await using var db = new KnowledgeDatabase(Shared(Guid.NewGuid().ToString())); var service = new KnowledgeService(db);
        var doc = await service.SaveAsync(null, Input("first draft"), default);
        await service.SetPublishedAsync(doc, true, default, null, (await db.Documents.SingleAsync(x => x.Id == doc)).LatestVersionId);
        await service.SetPublishedAsync(doc, false, default);
        Assert.Null((await db.Documents.SingleAsync(x => x.Id == doc)).PublishedVersionId);
    }

    [Fact]
    public async Task A_save_that_lands_between_the_read_and_the_write_of_a_publish_is_detected()
    {
        var name = Guid.NewGuid().ToString();
        await using var admin = new KnowledgeDatabase(Shared(name)); await using var other = new KnowledgeDatabase(Shared(name));
        var doc = await new KnowledgeService(admin).SaveAsync(null, Input("first draft"), default);
        var stale = await admin.Documents.SingleAsync(x => x.Id == doc);   // the publisher has read the row
        await new KnowledgeService(other).SaveAsync(doc, Input("second draft"), default); // a concurrent save changes the latest version
        stale.PublishedVersionId = stale.LatestVersionId;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => admin.SaveChangesAsync());
    }

    [Fact]
    public async Task Http_publish_requires_the_version_and_answers_409_when_it_is_stale()
    {
        await using var factory = new KnowledgeHttpFactory(); await factory.Seed("erp", true, ["knowledge.manage"]);
        using var client = factory.CreateClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "PasswordExample123!" })).StatusCode);
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());

        object Body(string text) => new { title = "Guide", text, collection = "public", audience = "public", confirm = true };
        var create = await client.PostAsJsonAsync("/api/ai/knowledge/documents", Body("first draft")); Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        string Latest()
        {
            using var scope = factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<KnowledgeDatabase>().Documents.AsNoTracking().Single(x => x.Id == id).LatestVersionId;
        }
        var approved = Latest();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/ai/knowledge/documents/{id}", Body("second draft"))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{id}/publish", new { confirm = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{id}/publish", new { confirm = true, versionId = approved })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{id}/publish", new { confirm = true, versionId = Latest() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/ai/knowledge/documents/{id}/revoke", new { confirm = true })).StatusCode);
    }
}
