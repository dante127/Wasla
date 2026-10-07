using Wasla.BuildingBlocks.Domain;

namespace Wasla.Messages.Application.Contracts;

public sealed record ConversationMessageCounts(Guid ConversationId, int Inbound, int Outbound);

public sealed record DailyMessageCount(DateOnly Date, int Inbound, int Outbound);

/// <summary>Module contract: message aggregates for analytics (Phase 8, read-only).</summary>
public interface IMessageAnalyticsSource
{
    Task<IReadOnlyDictionary<Guid, ConversationMessageCounts>> GetConversationCountsAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyMessageCount>> GetDailyCountsAsync(
        TenantId tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
