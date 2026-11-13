// Role: Enforces conversation ownership, encrypted history, durable admission/idempotency and evidence revocation.
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Chat;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Platform;

public sealed class ConversationService(PlatformDatabase db, AssistantAccess access, PlatformOrchestrator orchestrator,
    AssistantRegistry registry, IDataProtectionProvider protection, AssistantEvidence evidence, IConfiguration configuration)
{
    // This lock reduces contention locally; SQL concurrency tokens and unique keys enforce cross-instance safety.
    private static readonly SemaphoreSlim Admission = new(1, 1);
    private readonly IDataProtector cipher = protection.CreateProtector("nbjewel.ai.history.v1");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private int Option(string key, int fallback, int min, int max) => int.TryParse(configuration["AiPlatform:" + key], out var n)
        ? Math.Clamp(n, min, max) : fallback;
    private static ConversationView View(PlatformConversation c) => new(c.Id, c.AssistantId, c.CreatedAt, c.ExpiresAt);

    public async Task<ConversationView> CreateAsync(AssistantContext ctx, CancellationToken ct)
    {
        _ = registry.Get(ctx.AssistantId);
        var owner = AssistantAccess.Owner(ctx);
        var now = DateTimeOffset.UtcNow;
        if (await db.Conversations.CountAsync(x => x.OwnerHash == owner && x.ExpiresAt > now, ct) >= 20) throw new AssistantBusyException();
        // A visitor cookie is free to discard, so anonymous creation is also limited per network address.
        if (ctx.AccountType == "visitor") await ChargeAddress("ipnew:", ctx, Option("AnonymousIpConversationsPerDay", 30, 1, 100000), 0, now, ct);
        var days = ctx.AccountType == "visitor" ? Option("VisitorRetentionDays", 7, 1, 30) : Option("UserRetentionDays", 30, 1, 365);
        var row = new PlatformConversation { AssistantId = ctx.AssistantId, OwnerHash = owner,
            AccessFingerprint = AssistantAccess.Fingerprint(ctx), CreatedAt = now, ExpiresAt = now.AddDays(days) };
        db.Conversations.Add(row);
        Audit(ctx, "conversation.create", "allowed");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new AssistantBusyException(); }
        return View(row);
    }

    // requireCurrentAccess is false only for deletion: the owner may always remove their own transcript, even after access changed.
    private async Task<PlatformConversation> Owned(AssistantContext ctx, string id, CancellationToken ct, bool requireCurrentAccess = true)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new KeyNotFoundException();
        var row = await db.Conversations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || row.AssistantId != ctx.AssistantId || row.OwnerHash != AssistantAccess.Owner(ctx) ||
            row.ExpiresAt <= DateTimeOffset.UtcNow) throw new KeyNotFoundException();
        if (!requireCurrentAccess) return row;
        var fresh = await access.RefreshAsync(ctx, ct);
        if (row.AccessFingerprint != AssistantAccess.Fingerprint(fresh)) throw new AssistantDeniedException();
        return row;
    }

    public async Task<IReadOnlyList<object>> MessagesAsync(AssistantContext ctx, string id, CancellationToken ct)
    {
        await Owned(ctx, id, ct);
        var rows = await db.Messages.AsNoTracking().Where(x => x.ConversationId == id).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var result = new List<object>();
        var checks = new EvidenceCheckCache(); // one history read: a source cited by several messages is revalidated once
        foreach (var row in rows.OrderBy(x => x.CreatedAt))
        {
            var sources = JsonSerializer.Deserialize<List<ChatSource>>(cipher.Unprotect(row.ProtectedSources), Json) ?? [];
            var allEvidence = JsonSerializer.Deserialize<List<ChatSource>>(cipher.Unprotect(row.ProtectedEvidence), Json) ?? [];
            var allowed = row.Role == "user" || await evidence.CanUseAsync(ctx, allEvidence, ct, checks);
            result.Add(new { row.Id, row.Role, Text = allowed ? cipher.Unprotect(row.ProtectedText) : "Previously cited information is no longer available.",
                Sources = allowed ? sources : [], Refused = row.Refused || !allowed, row.CreatedAt });
        }
        return result;
    }

    public async Task DeleteAsync(AssistantContext ctx, string id, CancellationToken ct)
    {
        var row = await Owned(ctx, id, ct, requireCurrentAccess: false);
        if (row.LeaseUntil > DateTimeOffset.UtcNow) throw new AssistantConflictException();
        row.Revision++;
        db.Messages.RemoveRange(await db.Messages.Where(x => x.ConversationId == id).ToListAsync(ct));
        db.Turns.RemoveRange(await db.Turns.Where(x => x.ConversationId == id).ToListAsync(ct));
        db.Conversations.Remove(row);
        Audit(ctx, "conversation.delete", "allowed");
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new AssistantConflictException(); }
    }

    public async Task<AssistantReply> SendAsync(AssistantContext ctx, string id, MessageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 500 ||
            !Guid.TryParse(request.ClientRequestId, out _)) throw new ArgumentException("A question and UUID clientRequestId are required.");
        var text = request.Text.Trim();
        var hash = AssistantAccess.Hash(AssistantAccess.Owner(ctx) + ":" + text);
        var clientId = Guid.Parse(request.ClientRequestId).ToString("N");
        PlatformConversation conversation;
        PlatformTurn turn;
        var lease = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await Admission.WaitAsync(ct);
        try
        {
            conversation = await Owned(ctx, id, ct);
            var existing = await db.Turns.SingleOrDefaultAsync(x => x.ConversationId == id && x.ClientRequestId == clientId, ct);
            if (existing is not null)
            {
                if (existing.QuestionHash != hash) throw new AssistantConflictException();
                if (existing.State == "complete")
                {
                    var replay = JsonSerializer.Deserialize<AssistantReply>(cipher.Unprotect(existing.ProtectedReply!), Json)!;
                    var allEvidence = JsonSerializer.Deserialize<List<ChatSource>>(cipher.Unprotect(existing.ProtectedEvidence!), Json) ?? [];
                    return await evidence.CanUseAsync(ctx, allEvidence, ct) ? replay : new("Previously cited information is no longer available.", [], true, replay.TraceId);
                }
            }
            if (conversation.LeaseUntil > now) throw new AssistantConflictException();
            var owner = AssistantAccess.Owner(ctx);
            if (await db.Conversations.CountAsync(x => x.OwnerHash == owner && x.LeaseUntil > now, ct) >= 2 ||
                await db.Conversations.CountAsync(x => x.AssistantId == ctx.AssistantId && x.LeaseUntil > now, ct) >= Option("AssistantConcurrency", 8, 1, 100))
                throw new AssistantBusyException();
            var definition = registry.Get(ctx.AssistantId);
            // Upper-bound reservation per question: each generation reads at most MaxInputCharacters (about 4 characters per token) and writes at most MaxOutputTokens.
            var reserve = (long)definition.MaxModelCalls * (definition.MaxInputCharacters / 4L + definition.MaxOutputTokens);
            if (ctx.AccountType == "visitor") await ChargeAddress("ip:", ctx, Option("AnonymousIpRequestsPerDay", 500, 1, 1000000), reserve, now, ct);
            await Charge("subject:" + owner, Option("RequestsPerMinute", 6, 1, 1000), Option("UserRequestsPerDay", 200, 1, 100000), reserve, now, ct);
            await Charge("assistant:" + ctx.AssistantId, Option("AssistantRequestsPerMinute", 60, 1, 10000), Option("AssistantRequestsPerDay", 1000, 1, 1000000), reserve, now, ct);
            turn = existing ?? new PlatformTurn { ConversationId = id, ClientRequestId = clientId, QuestionHash = hash, CreatedAt = now };
            if (existing is null) db.Turns.Add(turn);
            turn.State = "pending"; turn.ReservedTokens = reserve;
            conversation.LeaseOwner = lease; conversation.LeaseUntil = now.AddMinutes(1); conversation.Revision++;
            Audit(ctx, "message.admit", "allowed");
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { throw new AssistantBusyException(); }
        }
        finally { Admission.Release(); }

        var trace = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            var previous = await db.Messages.AsNoTracking().Where(x => x.ConversationId == id && x.Role == "user")
                .OrderByDescending(x => x.CreatedAt).Take(6).ToListAsync(deadline.Token);
            var questions = previous.OrderBy(x => x.CreatedAt).Select(x => cipher.Unprotect(x.ProtectedText)).ToArray();
            var reply = await orchestrator.AskAsync(ctx, text, questions, trace, deadline.Token);
            var fresh = await access.RefreshAsync(ctx, deadline.Token);
            if (conversation.AccessFingerprint != AssistantAccess.Fingerprint(fresh)) throw new AssistantDeniedException();
            if (!await evidence.CanUseAsync(fresh, reply.Evidence, deadline.Token)) reply = new("I could not verify the cited information.", [], true, trace);
            conversation.LeaseOwner = null; conversation.LeaseUntil = null; conversation.Revision++;
            turn.State = "complete"; turn.ProtectedReply = cipher.Protect(JsonSerializer.Serialize(reply, Json));
            turn.ProtectedEvidence = cipher.Protect(JsonSerializer.Serialize(reply.Evidence, Json));
            db.Messages.Add(Message(id, "user", text, [], [], false, now));
            db.Messages.Add(Message(id, "assistant", reply.Answer, reply.Sources, reply.Evidence, reply.Refused, DateTimeOffset.UtcNow));
            Audit(ctx, "message.complete", reply.Refused ? "refused" : "allowed", trace);
            await db.SaveChangesAsync(deadline.Token);
            return reply;
        }
        catch
        {
            // Always clear the lease, even after request cancellation; no failed answer/history is persisted.
            db.ChangeTracker.Clear();
            var failed = await db.Conversations.SingleOrDefaultAsync(x => x.Id == id, CancellationToken.None);
            var failedTurn = await db.Turns.SingleOrDefaultAsync(x => x.Id == turn.Id, CancellationToken.None);
            if (failed?.LeaseOwner == lease)
            {
                failed.LeaseOwner = null; failed.LeaseUntil = null; failed.Revision++;
                if (failedTurn is not null) failedTurn.State = "failed";
                Audit(ctx, "message.failed", "error", trace);
                try { await db.SaveChangesAsync(CancellationToken.None); } catch (DbUpdateConcurrencyException) { }
            }
            throw;
        }
    }

    // Counts an anonymous caller against its network address (pseudonymized) with the same durable gate used for subjects.
    private Task ChargeAddress(string kind, AssistantContext ctx, int dayLimit, long reserve, DateTimeOffset now, CancellationToken ct) =>
        Charge(kind + AssistantAccess.Hash(ctx.ClientAddress ?? "unknown"), Option("AnonymousIpRequestsPerMinute", 30, 1, 10000), dayLimit, reserve, now, ct);

    private async Task Charge(string key, int minuteLimit, int dayLimit, long reserve, DateTimeOffset now, CancellationToken ct)
    {
        var gate = await db.Gates.SingleOrDefaultAsync(x => x.Id == key, ct);
        if (gate is null) { gate = new PlatformGate { Id = key, MinuteStart = now, DayStart = now }; db.Gates.Add(gate); }
        if (now - gate.MinuteStart >= TimeSpan.FromMinutes(1)) { gate.MinuteStart = now; gate.MinuteCount = 0; }
        if (gate.DayStart.UtcDateTime.Date != now.UtcDateTime.Date) { gate.DayStart = now; gate.DayCount = 0; gate.DayTokens = 0; }
        if (gate.MinuteCount >= minuteLimit || gate.DayCount >= dayLimit || gate.DayTokens + reserve > Option("DailyReservedTokenBudget", 50000000, 1000, int.MaxValue)) throw new AssistantBusyException();
        gate.MinuteCount++; gate.DayCount++; gate.DayTokens += reserve; gate.Revision++;
    }

    private PlatformMessage Message(string id, string role, string text, List<ChatSource> sources, List<ChatSource> allEvidence, bool refused, DateTimeOffset at) => new()
    { ConversationId = id, Role = role, ProtectedText = cipher.Protect(text), ProtectedSources = cipher.Protect(JsonSerializer.Serialize(sources, Json)), ProtectedEvidence = cipher.Protect(JsonSerializer.Serialize(allEvidence, Json)), Refused = refused, CreatedAt = at };
    private void Audit(AssistantContext c, string operation, string outcome, string trace = "") => db.Audit.Add(new PlatformAudit
    { AssistantId = c.AssistantId, SubjectHash = AssistantAccess.Owner(c), TraceId = trace, Operation = operation, Outcome = outcome, CreatedAt = DateTimeOffset.UtcNow });

}
