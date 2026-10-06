using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Infrastructure.Repositories;

/// <summary>
/// Outbox storage. Claiming uses the one sanctioned raw system query
/// (FOR UPDATE SKIP LOCKED — docs/events.md, ADR-0004); stale in-flight rows are
/// reclaimable so crashes never strand sends.
/// </summary>
public sealed class OutboxRepository(MessagesDbContext dbContext) : IOutboxRepository
{
    private const string ClaimSql = """
        UPDATE messages."OutboxMessages"
        SET "Status" = 'Processing', "Attempts" = "Attempts" + 1
        WHERE "Id" IN (
            SELECT "Id" FROM messages."OutboxMessages"
            WHERE ("Status" = 'Pending' AND "NextAttemptAt" <= now())
               OR ("Status" = 'Processing' AND "ProcessedAt" IS NULL
                   AND "CreatedAt" < now() - interval '10 minutes')
            ORDER BY "NextAttemptAt"
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED)
        RETURNING "Id", "TenantId";
        """;

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);

    public Task<OutboxMessage?> GetByIdAsync(TenantId tenantId, Guid id, CancellationToken cancellationToken) =>
        dbContext.OutboxMessages.FirstOrDefaultAsync(
            entity => entity.TenantId == tenantId && entity.Id == id,
            cancellationToken);

    public async Task<List<OutboxClaim>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var claims = new List<OutboxClaim>();
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
            claims.Add(new OutboxClaim(reader.GetGuid(0), new TenantId(reader.GetGuid(1))));
        }

        return claims;
    }
}
