// Role: Selects trusted assistants and providers, bounds model calls, and logs metadata without conversation bodies.
using System.Collections.Frozen;
using NbJewel.Api.Models;

namespace NbJewel.Api.Chat;

public sealed record AssistantDefinition(string Id, string Name, string Purpose, string Instructions,
    string Style, string Provider, string Model, FrozenSet<string> AllowedTools,
    FrozenSet<string> KnowledgeCollections, int HistoryLimit, int MaxModelCalls, int MaxToolCalls,
    int MaxOutputTokens, int MaxInputCharacters, int ModelTimeoutSeconds);

public sealed class AssistantRegistry
{
    private readonly FrozenDictionary<string, AssistantDefinition> definitions;
    private static readonly string[] ErpTools = ["search_items", "get_item_details", "get_item_price_breakdown",
        "get_stock_by_store", "list_low_stock", "list_categories", "get_latest_metal_rate", "list_items_by_status", "search_knowledge"];
    private static readonly string[] WebsiteTools = ["search_public_products", "get_public_product", "search_knowledge"];

    public AssistantRegistry(IConfiguration configuration)
    {
        definitions = new[] { Build("erp", true), Build("website", false) }.ToFrozenDictionary(x => x.Id);

        AssistantDefinition Build(string id, bool erp)
        {
            var section = configuration.GetSection($"Assistants:{id}");
            var tools = section["Tools"] is { } configured
                ? configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : erp ? ErpTools : WebsiteTools;
            if (tools.Any(x => !(erp ? ErpTools : WebsiteTools).Contains(x)))
                throw new InvalidOperationException($"Assistant {id} contains an unsupported tool.");
            var collections = (section["KnowledgeCollections"] ?? (erp ? "public,erp" : "public"))
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (collections.Any(x => !(erp ? new[] { "public", "erp" } : new[] { "public" }).Contains(x)))
                throw new InvalidOperationException("Unsupported knowledge collection.");
            return new(id, section["Name"] ?? (erp ? "ERP assistant" : "Website assistant"),
                section["Purpose"] ?? (erp ? "Read authorized ERP data and approved documentation." : "Help customers using published products and approved public documentation."),
                section["Instructions"] ?? (erp ? SystemPrompt.Text : "You are the public website assistant. Answer only from approved tool results with sources. Never use model memory or private ERP data. Refuse unsupported questions. Return JSON with answer and sources containing tool and recordId."),
                section["Style"] ?? "Clear, concise and factual.", section["Provider"] ?? "gemini",
                section["Model"] ?? configuration["GEMINI_MODEL"] ?? "", tools.ToFrozenSet(),
                collections.ToFrozenSet(), Limit("HistoryLimit", 6, 0, 6),
                Limit("MaxModelCalls", 8, 1, 8), Limit("MaxToolCalls", 5, 0, 5),
                Limit("MaxOutputTokens", 2048, 128, 8192), Limit("MaxInputCharacters", 32000, 1000, 64000),
                Limit("ModelTimeoutSeconds", 20, 1, 30));

            int Limit(string name, int fallback, int min, int max)
            {
                if (section[name] is not { } text) return fallback;
                if (!int.TryParse(text, out var value) || value < min || value > max)
                    throw new InvalidOperationException($"Assistant {id} setting {name} must be between {min} and {max}.");
                return value;
            }
        }
    }

    public AssistantDefinition Get(string id) => definitions.TryGetValue(id, out var definition)
        ? definition : throw new LlmException("Unknown assistant.");
}

// Only Gemini is implemented. Provider validation happens before any client invocation.
public sealed class GatewayProvider(ILlmClient client)
{
    public void Validate(AssistantDefinition definition)
    {
        if (definition.Provider != "gemini") throw new LlmException("Unsupported model provider.");
    }

    public Task<LlmResponse> GenerateAsync(AssistantDefinition definition, IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDeclaration> tools, double temperature, CancellationToken cancellationToken, ModelRetryBudget? retryBudget = null)
    {
        Validate(definition);
        return client.GenerateAsync(messages, tools, temperature,
            new LlmRequestSettings(definition.Model, definition.MaxOutputTokens, retryBudget), cancellationToken);
    }
}
