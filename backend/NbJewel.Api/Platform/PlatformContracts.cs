// Role: Defines trusted assistant execution, tool, retrieval and conversation contracts shared across modules.
using System.Text.Json.Nodes;
using NbJewel.Api.Businesses;
using NbJewel.Api.Chat;
using NbJewel.Api.Models;

namespace NbJewel.Api.Platform;

public sealed record AssistantContext(string AssistantId, string SubjectId, string AccountType,
    string[] Permissions, int[] StoreIds, bool AllStores, string[]? KnowledgeCollections = null, string? ClientAddress = null, string BusinessId = BusinessRegistry.DemoSlug, string? PublicPolicyVersion = null)
{
    // ClientAddress is the server-derived network key of an anonymous caller. It is never part of the access fingerprint.
    public bool Has(string permission) => AccountType == "erp" && Permissions.Contains(permission);
}

public interface IAssistantTools
{
    IReadOnlyList<ToolDeclaration> Declarations(AssistantContext context);
    Task<JsonObject> RunAsync(AssistantContext context, string name, JsonObject args, CancellationToken ct);
}

public sealed record KnowledgeHit(string DocumentId, string VersionId, string ChunkId, string Title, string Text);
public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(AssistantContext context, string query, CancellationToken ct);
}

public interface IPublicationReader
{
    Task<IReadOnlySet<int>> PublishedItemIdsAsync(CancellationToken ct);
    // Public images require the exact authorized revision, not just a product ID.
    Task<IReadOnlyDictionary<int, string?>> PublishedItemRevisionsAsync(CancellationToken ct) => throw new AssistantUnavailableException();
}

public interface IKnowledgeEvidenceValidator
{
    Task<bool> CanUseAsync(AssistantContext context, string chunkId, CancellationToken ct);
}

public sealed record ConversationView(string Id, string AssistantId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
public sealed record MessageRequest(string Text, string ClientRequestId);
public sealed record AssistantReply(string Answer, List<ChatSource> Sources, bool Refused, string TraceId)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public List<ChatSource> Evidence { get; init; } = [];
}
public sealed class AssistantDeniedException : Exception;
public sealed class AssistantUnavailableException : Exception;
public sealed class AssistantBusyException : Exception;
public sealed class AssistantConflictException : Exception;
