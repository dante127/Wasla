using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure.Repositories;

/// <summary>
/// Inbox storage. Claiming uses the one sanctioned raw system query
/// (FOR UPDATE SKIP LOCKED — docs/webhooks.md §6); stale in-flight rows are reclaimable
/// so crashes never strand events.
/// </summary>
public sealed class InboxRepository(ChannelsDbContext dbContext) : IInboxRepository
{
    private const string ClaimSql = """
        UPDATE channels."InboxEvents"
        SET "Status" = 'Processing', "Attempts" = "Attempts" + 1
        WHERE "Id" IN (
            SELECT "Id" FROM channels."InboxEvents"
            WHERE ("Status" = 'Pending' AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= now()))
               OR ("Status" = 'Processing' AND "ProcessedAt" IS NULL
                   AND "ReceivedAt" < now() - interval '10 minutes')
            ORDER BY "ReceivedAt"
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED)
        RETURNING "Id", "TenantId";
        """;

    public async Task AddAsync(InboxEvent inboxEvent, CancellationToken cancellationToken) =>
        await dbContext.InboxEvents.AddAsync(inboxEvent, cancellationToken);

    public Task<InboxEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.InboxEvents.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public Task<bool> ExistsByBodyHashAsync(Guid channelId, string bodyHash, CancellationToken cancellationToken) =>
        dbContext.InboxEvents.AnyAsync(
            entity => entity.ChannelId == channelId && entity.BodyHash == bodyHash,
            cancellationToken);

    public async Task<List<InboxClaim>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var claims = new List<InboxClaim>();
        var connection = dbContext.Database.GetDbConnection();

        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = ClaimSql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@batchSize";
        parameter.Value = batchSize;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            claims.Add(new InboxClaim(reader.GetGuid(0), new TenantId(reader.GetGuid(1))));
        }

        return claims;
    }
}
