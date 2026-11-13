// Role: Purges expired conversation content, stale admission gates and retained telemetry without recording private message bodies.
using Microsoft.EntityFrameworkCore;

namespace NbJewel.Api.Platform;

public sealed record PurgeResult(int Conversations, int Gates, int Usage, int Audit);

public sealed class PlatformMaintenance(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<PlatformMaintenance> logger) : BackgroundService
{
    private const int BatchSize = 500;
    private const int MaxBatchesPerRun = 40;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<PlatformDatabase>();
                if (database.Database.ProviderName is not null)
                {
                    var days = int.TryParse(configuration["AiPlatform:AuditRetentionDays"], out var value) ? Math.Clamp(value, 1, 3650) : 90;
                    await PurgeAsync(database, days, DateTimeOffset.UtcNow, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning("Assistant maintenance failed: {ErrorType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    // Loops in bounded batches until nothing is left, so a backlog from sustained creation cannot outgrow the purge rate.
    public static async Task<PurgeResult> PurgeAsync(PlatformDatabase database, int auditRetentionDays, DateTimeOffset now, CancellationToken ct)
    {
        int conversations = 0, gates = 0, usage = 0, audit = 0;
        for (var batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            var rows = await database.Conversations.Where(x => x.ExpiresAt <= now && (x.LeaseUntil == null || x.LeaseUntil <= now)).Take(BatchSize).ToListAsync(ct);
            if (rows.Count == 0) break;
            var ids = rows.Select(x => x.Id).ToArray();
            database.Messages.RemoveRange(await database.Messages.Where(x => ids.Contains(x.ConversationId)).ToListAsync(ct));
            database.Turns.RemoveRange(await database.Turns.Where(x => ids.Contains(x.ConversationId)).ToListAsync(ct));
            database.Conversations.RemoveRange(rows);
            await database.SaveChangesAsync(ct);
            conversations += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        // A gate counts per minute and per UTC day, so a row untouched for over a day carries no live limit.
        var gateCutoff = now.AddDays(-2);
        for (var batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            var rows = await database.Gates.Where(x => x.MinuteStart < gateCutoff && x.DayStart < gateCutoff).Take(BatchSize).ToListAsync(ct);
            if (rows.Count == 0) break;
            database.Gates.RemoveRange(rows);
            await database.SaveChangesAsync(ct);
            gates += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        var cutoff = now.AddDays(-auditRetentionDays);
        for (var batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            var old = await database.Usage.Where(x => x.CreatedAt < cutoff).Take(BatchSize).ToListAsync(ct);
            if (old.Count == 0) break;
            database.Usage.RemoveRange(old); await database.SaveChangesAsync(ct); usage += old.Count;
            if (old.Count < BatchSize) break;
        }
        for (var batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            var old = await database.Audit.Where(x => x.CreatedAt < cutoff).Take(BatchSize).ToListAsync(ct);
            if (old.Count == 0) break;
            database.Audit.RemoveRange(old); await database.SaveChangesAsync(ct); audit += old.Count;
            if (old.Count < BatchSize) break;
        }
        return new PurgeResult(conversations, gates, usage, audit);
    }
}
