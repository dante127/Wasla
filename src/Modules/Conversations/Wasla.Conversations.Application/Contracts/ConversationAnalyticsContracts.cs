using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Application.Contracts;

/// <summary>Per-conversation facts consumed by the analytics recompute (Phase 8).</summary>
public sealed record ConversationAnalyticsRecord(
    Guid ConversationId,
    Guid ChannelId,
    Guid CustomerId,
    Guid? AssignedUserId,
    Guid? AssignedTeamId,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Module contract: stream conversation facts for analytics (read-only).</summary>
public interface IConversationAnalyticsSource
{
    Task<IReadOnlyList<ConversationAnalyticsRecord>> ListAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);
}
