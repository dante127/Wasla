using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure;

/// <summary>Module contract implementation: conversation reads for other modules.</summary>
public sealed class ConversationInfoProvider(IConversationRepository conversations) : IConversationInfoProvider
{
    public async Task<ConversationInfo?> GetAsync(
        TenantId tenantId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        return conversation is null ? null : ToInfo(conversation);
    }

    private static ConversationInfo ToInfo(Conversation conversation) =>
        new(
            conversation.Id.Value,
            conversation.CustomerId,
            conversation.ChannelId,
            conversation.Status.ToString(),
            conversation.AssignedUserId,
            conversation.AssignedTeamId);
}

/// <summary>
/// Module contract implementation: applies message-derived effects (counters, previews,
/// timeline) to conversations. Used by the Messages module.
/// </summary>
public sealed class ConversationWriter(
    IConversationRepository conversations,
    ConversationsDbContext dbContext) : IConversationWriter
{
    public Task RecordOutboundMessageAsync(
        TenantId tenantId,
        Guid conversationId,
        Guid messageId,
        string? preview,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        RecordAsync(tenantId, conversationId, messageId, preview, at, inbound: false, cancellationToken);

    public Task RecordInboundMessageAsync(
        TenantId tenantId,
        Guid conversationId,
        Guid messageId,
        string? preview,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        RecordAsync(tenantId, conversationId, messageId, preview, at, inbound: true, cancellationToken);

    private async Task RecordAsync(
        TenantId tenantId,
        Guid conversationId,
        Guid messageId,
        string? preview,
        DateTimeOffset at,
        bool inbound,
        CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        if (conversation is null)
        {
            return;
        }

        if (messageId != Guid.Empty
            && conversation.Timeline.Any(entry => entry.Data == messageId.ToString()))
        {
            // Retry-safe: this message's counters were already applied.
            return;
        }

        conversation.RecordMessage(messageId, inbound, preview, at);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
