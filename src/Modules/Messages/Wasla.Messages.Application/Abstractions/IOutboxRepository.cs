using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Application.Abstractions;

/// <summary>Minimal claim descriptor for an outbox row picked up by a worker.</summary>
public sealed record OutboxClaim(Guid Id, TenantId TenantId);

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);

    Task<OutboxMessage?> GetByIdAsync(TenantId tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims a batch of due rows (FOR UPDATE SKIP LOCKED; runs without a
    /// tenant filter because it crosses tenants). Stale in-flight rows are reclaimable.
    /// </summary>
    Task<List<OutboxClaim>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken);
}
