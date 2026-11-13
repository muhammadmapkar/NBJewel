// Role: Verifies trusted assistant isolation and gateway failure boundaries with deterministic providers.
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Identity;
using NbJewel.Api.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NbJewel.Api.Chat;
using NbJewel.Api.Tests.Chat;
using static NbJewel.Api.Tests.Chat.ChatTestKit;

namespace NbJewel.Api.Tests;

public sealed class AiGatewayTests
{
    [Fact]
    public async Task CustomSystemInstructionsAreCheckedForLeaks()
    {
        const string instruction = "Private orchard instruction must remain confidential";
        var model = new FakeLlm(Call("search_items", new { query = "zork" }),
            Say("Zork Ring. " + instruction, ("search_items", "1")));
        var registry = Registry(new() { ["Assistants:erp:Instructions"] = instruction });
        var result = await Gateway(registry, model).AskAsync("erp", "Find zork", default);
        Assert.True(result.Refused);
    }

    [Fact]
    public async Task InvalidSourceTextDoesNotEnterPersistedTelemetry()
    {
        const string plantedText = "private-customer-material";
        var model = new FakeLlm(Call("search_items", new { query = "zork" }), Say("Zork Ring", (plantedText, "1")));
        var gateway = Gateway(Registry(), model);
        var result = await gateway.AskAsync("erp", "Find zork", default);
        Assert.True(result.Refused);
        Assert.NotEmpty(gateway.Database.Usage);
        Assert.DoesNotContain(plantedText, System.Text.Json.JsonSerializer.Serialize(gateway.Database.Usage.ToList()));
        Assert.DoesNotContain(plantedText, System.Text.Json.JsonSerializer.Serialize(gateway.Database.Audit.ToList()));
    }

    [Fact]
    public async Task UnknownProviderFinishMetadataDoesNotEnterPersistedTelemetry()
    {
        const string plantedText = "private-finish-payload";
        var gateway = Gateway(Registry(), new FakeLlm(Say("No evidence") with { FinishReason = plantedText }));
        await gateway.AskAsync("erp", "Find zork", default);
        Assert.NotEmpty(gateway.Database.Usage);
        Assert.DoesNotContain(plantedText, System.Text.Json.JsonSerializer.Serialize(gateway.Database.Usage.ToList()));
    }

    [Fact]
    public void RegistrySeparatesWebsiteAndErpAndRejectsUnknownIds()
    {
        var registry = Registry();
        Assert.NotEmpty(registry.Get("erp").AllowedTools);
        Assert.Equal(new[] { "get_public_product", "search_knowledge", "search_public_products" }, registry.Get("website").AllowedTools.Order());
        Assert.Equal(new[] { "public" }, registry.Get("website").KnowledgeCollections);
        Assert.DoesNotContain("get_stock_by_store", registry.Get("website").AllowedTools.ToArray());
        Assert.Throws<LlmException>(() => registry.Get("unknown"));
    }

    [Fact]
    public async Task UnsupportedAssistantOrProviderNeverCallsModel()
    {
        var model = new FakeLlm(Say("x"));
        var registry = Registry(new() { ["Assistants:erp:Provider"] = "unknown" });
        var gateway = Gateway(registry, model);
        await Assert.ThrowsAsync<LlmException>(() => gateway.AskAsync("erp", "Find ring", default));

        await Assert.ThrowsAsync<LlmException>(() => gateway.AskAsync("forged", "Find ring", default));
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task HallucinatedToolIsDeniedBeforeRepositoryExecution()
    {
        var model = new FakeLlm(Call("get_item_details", new { itemId = 1 }));
        var registry = Registry(new() { ["Assistants:erp:Tools"] = "search_items" });
        var result = await Gateway(registry, model).AskAsync("erp", "Find ring", default);
        Assert.True(result.Refused);
        Assert.Single(model.Calls);
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedWithoutRetry()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Gateway(Registry(), new FakeLlm(Say("x")))
            .AskAsync("erp", "Find ring", cancellation.Token));
    }

    private static AssistantRegistry Registry(Dictionary<string, string?>? values = null)
        => new(new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build());

    [Fact]
    public void InvalidBudgetsAndWebsiteErpToolsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Registry(new() { ["Assistants:erp:MaxModelCalls"] = "9" }));
        Assert.Throws<InvalidOperationException>(() => Registry(new() { ["Assistants:website:Tools"] = "search_items" }));
        Assert.Throws<InvalidOperationException>(() => Registry(new() { ["Assistants:erp:MaxOutputTokens"] = "999999" }));
    }

    [Fact]
    public async Task TrustedModelAndBudgetAreForwardedAndToolsAreFiltered()
    {
        var model = new SettingsClient();
        var registry = Registry(new() { ["Assistants:erp:Model"] = "configured", ["Assistants:erp:MaxOutputTokens"] = "512", ["Assistants:erp:Tools"] = "search_items" });
        await Gateway(registry, model).AskAsync("erp", "Find ring", default);
        Assert.Equal("configured", model.Settings!.Model);
        Assert.Equal(512, model.Settings.MaxOutputTokens);
        Assert.NotNull(model.Settings.RetryBudget);
        Assert.Single(model.Tools!);
        Assert.Equal("search_items", model.Tools![0].Name);
    }

    [Fact]
    public async Task PerModelTimeoutStopsProviderWithoutRetry()
    {
        var model = new SettingsClient { Hang = true };
        var registry = Registry(new() { ["Assistants:erp:ModelTimeoutSeconds"] = "1" });
        await Assert.ThrowsAsync<LlmException>(() => Gateway(registry, model).AskAsync("erp", "Find ring", default));
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task LowerModelCallBudgetIsEnforced()
    {
        var model = new FakeLlm(Call("search_items", new { query = "ring" }), Say("x"));
        var result = await Gateway(Registry(new() { ["Assistants:erp:MaxModelCalls"] = "1" }), model)
            .AskAsync("erp", "Find ring", default);
        Assert.True(result.Refused);
        Assert.Single(model.Calls);
    }

    [Fact]
    public async Task ProviderForwardsSharedRetryBudget()
    {
        var model = new SettingsClient();
        var budget = new ModelRetryBudget();
        await new GatewayProvider(model).GenerateAsync(Registry().Get("erp"), [], [], 0.1, default, budget);
        Assert.Same(budget, model.Settings!.RetryBudget);
    }

    private sealed class SettingsClient : ILlmClient
    {
        public LlmRequestSettings? Settings { get; private set; }
        public IReadOnlyList<ToolDeclaration>? Tools { get; private set; }
        public bool Hang { get; init; }
        public int Calls { get; private set; }
        public Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Trusted settings must be supplied.");
        public async Task<LlmResponse> GenerateAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDeclaration> tools, double temperature, LlmRequestSettings settings, CancellationToken cancellationToken)
        {
            Settings = settings;
            Tools = tools;
            Calls++;
            if (Hang) await Task.Delay(Timeout.Infinite, cancellationToken);
            return Say("x");
        }
    }

    private sealed class Permissions : IPermissionAdapter
    {
        public Task<AccountAccess?> ResolveAsync(string userId, CancellationToken ct) => Task.FromResult<AccountAccess?>(new("u", "", "erp", Grants, [], true));
    }
    private sealed class Knowledge : IKnowledgeRetriever, IKnowledgeEvidenceValidator
    {
        public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(AssistantContext context, string query, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
        public Task<bool> CanUseAsync(AssistantContext context, string chunkId, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class Publications : IPublicationReader
    {
        public Task<IReadOnlySet<int>> PublishedItemIdsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
    }
    private static readonly string[] Grants = ["chat.read", "items.read", "prices.read", "status.read", "stock.read", "reference.read"];
    private sealed class Pipeline(AssistantRegistry registry, ILlmClient client)
    {
        public PlatformDatabase Database { get; } = new(new DbContextOptionsBuilder<PlatformDatabase>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public async Task<AssistantReply> AskAsync(string id, string question, CancellationToken ct)
        {
            var permissions = new Permissions();
            var repo = SampleRepositoryFactory.Create();
            var knowledge = new Knowledge();
            var publications = new Publications();
            var tools = new AssistantTools(permissions, repo, knowledge, publications);
            var access = new AssistantAccess(permissions, new EphemeralDataProtectionProvider(), registry, null!);
            var evidence = new AssistantEvidence(knowledge, publications, tools, repo, registry);
            var orchestrator = new PlatformOrchestrator(registry, new GatewayProvider(client), tools, access, Database, new ConfigurationBuilder().Build(), evidence);
            return await orchestrator.AskAsync(new AssistantContext(id, "u", "erp", Grants, [], true), question, [], "test", ct);
        }
    }
    private static Pipeline Gateway(AssistantRegistry registry, ILlmClient client) => new(registry, client);
}
