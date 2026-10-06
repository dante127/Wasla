using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Application.Contracts;

/// <summary>Public, provider-neutral view of a conversation for other modules.</summary>
public sealed record ConversationInfo(
    Guid Id,
    Guid CustomerId,
    Guid ChannelId,
    string Status,
    Guid? AssignedUserId,
    Guid? AssignedTeamId);

/// <summary>Module contract: read conversation data without touching Conversations internals.</summary>
public interface IConversationInfoProvider
{
    Task<ConversationInfo?> GetAsync(TenantId tenantId, Guid conversationId, CancellationToken cancellationToken);
}

/// <summary>
/// Module contract: apply message-derived effects (counters, previews, timeline) to a
/// conversation. Used by the Messages module; internal notes never flow through this path.
/// </summary>
public interface IConversationWriter
{
    Task RecordOutboundMessageAsync(
        TenantId tenantId,
        Guid conversationId,
        Guid messageId,
        string? preview,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    Task RecordInboundMessageAsync(
        TenantId tenantId,
        Guid conversationId,
        Guid messageId,
        string? preview,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
