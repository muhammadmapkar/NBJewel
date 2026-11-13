// Role: Verifies real SQL schemas, bulk/scoped reads, durable protected identity/history, and concurrency in explicitly isolated local test databases.
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NbJewel.Api.Chat;
using NbJewel.Api.Identity;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Platform;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests;

public sealed class PlatformSqlFactAttribute : FactAttribute
{
    public PlatformSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NBJEWEL_PLATFORM_TEST_CONNECTION")))
            Skip = "Set the approved isolated NBJEWEL_PLATFORM_TEST_CONNECTION to run real SQL platform checks.";
    }
}

public sealed class PlatformSqlIntegrationTests
{
    [Fact]
    public void Repository_root_includes_source_schema_files()
    {
        var root = SampleRepositoryFactory.FindRoot();
        Assert.True(File.Exists(Path.Combine(root, "docs", "item_catalog_schema.sql")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "platform-schema.sql")));
    }

    private static bool initialized;
    private static string Connection
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NBJEWEL_PLATFORM_TEST_CONNECTION"));
            if (builder.InitialCatalog != "NBJewelPlatformTest" || builder.DataSource != "127.0.0.1,15433")
                throw new InvalidOperationException("Platform SQL tests require the approved local disposable target on port 15433 and database NBJewelPlatformTest.");
            return builder.ConnectionString;
        }
    }
    private static PlatformDatabase Platform() => new(new DbContextOptionsBuilder<PlatformDatabase>().UseSqlServer(Connection).Options);
    private static KnowledgeDatabase Knowledge() => new(new DbContextOptionsBuilder<KnowledgeDatabase>().UseSqlServer(Connection).Options);
    private static async Task Prepare()
    {
        if (initialized) return;
        await SqlDatabaseSetup.DropDatabaseAsync(Connection);
        await SqlDatabaseSetup.CreateDatabaseAsync(Connection);
        await using var connection = new SqlConnection(Connection); await connection.OpenAsync();
        var docs = Path.Combine(SampleRepositoryFactory.FindRoot(), "docs");
        await SqlDatabaseSetup.ApplySchemaAsync(connection, Path.Combine(docs, "identity-schema.sql"));
        await SqlDatabaseSetup.ApplySchemaAsync(connection, Path.Combine(docs, "platform-schema.sql"));
        initialized = true;
    }

    private sealed class SqlTestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "SqlPlatformIntegrationTests";
        public string ContentRootPath { get; set; } = SampleRepositoryFactory.FindRoot();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static ServiceProvider Services(string secret)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["AI_DATA_PROTECTION_SECRET"] = secret, ["AI_PLATFORM_CONNECTION"] = Connection }).Build();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddPlatformKeyProtection(configuration, new SqlTestEnvironment());
        services.AddDbContext<IdentityDatabase>(o => o.UseSqlServer(Connection));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<IdentityDatabase>().AddDefaultTokenProviders();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [PlatformSqlFact]
    public async Task Sql_key_ring_identity_confirmation_and_history_survive_provider_restart()
    {
        await Prepare();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string userId, token, protectedText;
        var conversationId = Guid.NewGuid().ToString("N");
        using (var first = Services(secret))
        {
            using var scope = first.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "sql-sample-" + conversationId + "@example.test", Email = "sql-sample-" + conversationId + "@example.test", AccountType = "customer" };
            Assert.True((await users.CreateAsync(user, "SampleOnly!Password123")).Succeeded); userId = user.Id;
            token = await users.GenerateEmailConfirmationTokenAsync(user);
            protectedText = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("nbjewel.ai.history.v1").Protect("sample protected history");
            await using var db = Platform();
            db.Conversations.Add(new PlatformConversation { Id = conversationId, AssistantId = "website", OwnerHash = "sample-owner", AccessFingerprint = "sample-scope", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
            db.Messages.Add(new PlatformMessage { ConversationId = conversationId, Role = "user", ProtectedText = protectedText,
                ProtectedSources = "[]", ProtectedEvidence = "[]", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        using (var restarted = Services(secret))
        {
            using var scope = restarted.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId); Assert.NotNull(user);
            Assert.True((await users.ConfirmEmailAsync(user!, token)).Succeeded);
            Assert.True(await users.IsEmailConfirmedAsync(user!));
            await using var db = Platform();
            var message = await db.Messages.SingleAsync(x => x.ConversationId == conversationId);
            Assert.Equal("sample protected history", restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("nbjewel.ai.history.v1").Unprotect(message.ProtectedText));
            var rows = await db.DataProtectionKeys.ToListAsync(); Assert.NotEmpty(rows);
            Assert.All(rows, row => { Assert.StartsWith("<encryptedKey>", row.ProtectedXml); Assert.DoesNotContain("masterKey", row.ProtectedXml); });
        }
        Assert.ThrowsAny<CryptographicException>(() => Services(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
    }

    [PlatformSqlFact]
    public async Task Sql_lease_revision_and_idempotency_constraints_reject_concurrent_updates()
    {
        await Prepare(); var id = Guid.NewGuid().ToString("N");
        await using (var seed = Platform())
        { seed.Gates.Add(new PlatformGate { Id = id, MinuteStart = DateTimeOffset.UtcNow, DayStart = DateTimeOffset.UtcNow }); await seed.SaveChangesAsync(); }
        await using var first = Platform(); await using var second = Platform();
        var a = await first.Gates.SingleAsync(x => x.Id == id); var b = await second.Gates.SingleAsync(x => x.Id == id);
        a.Revision++; a.MinuteCount++; await first.SaveChangesAsync();
        b.Revision++; b.MinuteCount++; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        var conversationId = Guid.NewGuid().ToString("N");
        await using (var seed = Platform())
        {
            seed.Conversations.Add(new PlatformConversation { Id = conversationId, AssistantId = "website", OwnerHash = "owner", AccessFingerprint = "scope", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            seed.Turns.Add(new PlatformTurn { ConversationId = conversationId, ClientRequestId = "same-request", QuestionHash = "sample", CreatedAt = DateTimeOffset.UtcNow });
            await seed.SaveChangesAsync();
        }
        await using var duplicate = Platform(); duplicate.Turns.Add(new PlatformTurn { ConversationId = conversationId, ClientRequestId = "same-request", QuestionHash = "sample", CreatedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        Assert.Equal(1, await first.Turns.CountAsync(x => x.ConversationId == conversationId));
    }

    [PlatformSqlFact]
    public async Task Sql_knowledge_retrieval_revocation_and_publication_concurrency_match_policy()
    {
        await Prepare(); await using var first = Knowledge(); var service = new KnowledgeService(first);
        var id = await service.SaveAsync(null, new("SQL sample policy", "Silver sample care guidance.", "public", "public", null, null, null, true), default);
        var document = await first.Documents.SingleAsync(x => x.Id == id); var approvedVersion = document.LatestVersionId;
        await service.SetPublishedAsync(id, true, default, versionId: approvedVersion);
        var visitor = new AssistantContext("website", "sample-visitor", "visitor", [], [], false, ["public"]);
        Assert.Contains(await service.SearchAsync(visitor, "Silver", default), hit => hit.DocumentId == id);
        await using var other = Knowledge();
        await new KnowledgeService(other).SaveAsync(id, new("Updated SQL sample", "Updated Silver guidance.", "public", "public", null, null, null, true), default);
        await Assert.ThrowsAsync<KnowledgeVersionConflictException>(() => service.SetPublishedAsync(id, true, default, versionId: approvedVersion));
        first.ChangeTracker.Clear(); await service.SetPublishedAsync(id, false, default);
        Assert.DoesNotContain(await service.SearchAsync(visitor, "Silver", default), hit => hit.DocumentId == id);
    }

    [PlatformSqlFact]
    public async Task Sql_bulk_stock_and_scoped_totals_match_sample_without_parameter_limit_failures()
    {
        await Prepare(); IItemRepository sql = Repos.Create("Sql"); IItemRepository sample = SampleRepositoryFactory.Create();
        var ids = Enumerable.Range(1, 2200).Concat([1, 2]).ToArray();
        static string[] Describe(IEnumerable<NbJewel.Api.Models.StockRecord> rows) => rows.Select(x => $"{x.ItemId}:{x.VariantId}:{x.StoreId}:{x.OnHand}:{x.Reserved}").Order().ToArray();
        Assert.Equal(Describe(await sample.GetStockForItemsAsync(ids, default)), Describe(await sql.GetStockForItemsAsync(ids, default)));
        Assert.Empty(await sql.GetStockForItemsAsync([], default));
        var context = new AssistantContext("erp", "sample-employee", "erp", [], [1], false);
        var sqlReader = new ScopedChatDataReader(sql, context); var sampleReader = new ScopedChatDataReader(sample, context);
        var actual = await sqlReader.SearchItemsAsync(null, null, null, null, null, 1, 100, default);
        var expected = await sampleReader.SearchItemsAsync(null, null, null, null, null, 1, 100, default);
        Assert.Equal(expected.TotalCount, actual.TotalCount); Assert.Equal(expected.Items.Select(x => x.Id), actual.Items.Select(x => x.Id));
        var args = JsonNode.Parse("{\"inStockOnly\":true}")!.AsObject();
        var sqlResult = await new ChatTools(sqlReader).RunAsync("search_items", args, default);
        var sampleResult = await new ChatTools(sampleReader).RunAsync("search_items", args, default);
        Assert.True(JsonNode.DeepEquals(sampleResult, sqlResult));
    }
}
