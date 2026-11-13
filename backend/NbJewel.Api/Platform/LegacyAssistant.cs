// Role: Routes the legacy ERP chat contract through the same owned, authorized, quota-controlled conversation pipeline.
using NbJewel.Api.Models;

namespace NbJewel.Api.Platform;

public interface ILegacyAssistant
{
    Task<ChatResponse> AskAsync(HttpContext http, string question, CancellationToken ct);
}

public sealed class LegacyAssistant(AssistantAccess access, ConversationService conversations, ILogger<LegacyAssistant> logger) : ILegacyAssistant
{
    public async Task<ChatResponse> AskAsync(HttpContext http, string question, CancellationToken ct)
    {
        var actor = await access.ResolveAsync("erp", http, ct);
        var conversation = await conversations.CreateAsync(actor, ct);
        try
        {
            var reply = await conversations.SendAsync(actor, conversation.Id, new(question, Guid.NewGuid().ToString()), ct);
            return new(reply.Answer, reply.Sources, reply.Refused);
        }
        finally
        {
            // Legacy calls have no conversation identifier, so do not accumulate inaccessible transcripts.
            // A failed cleanup must not replace the real result or error; the transcript then expires with normal retention.
            try { await conversations.DeleteAsync(actor, conversation.Id, CancellationToken.None); }
            catch (Exception error) { logger.LogWarning("Legacy transcript cleanup failed: {ErrorType}", error.GetType().Name); }
        }
    }
}
