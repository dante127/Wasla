using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Applies a lifecycle transition (resolve / reopen / close) to a conversation.</summary>
public sealed class ChangeConversationStatusHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IConversationRepository conversations,
    ICustomerInfoProvider customers,
    IChannelInfoProvider channels,
    ITagInfoProvider tags,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<ConversationDetail>> HandleAsync(
        Guid conversationId,
        ConversationStatus targetStatus,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ConversationDetail>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<ConversationDetail>(
                new Error("conversations.not_found", "Conversation not found."));
        }

        try
        {
            conversation.ChangeStatus(targetStatus, currentUser.UserId, clock.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<ConversationDetail>(
                new Error("conversations.status_conflict", exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var customerLookup = await customers.GetManyAsync(tenantId, [conversation.CustomerId], cancellationToken);
        var channelLookup = await channels.GetManyAsync(tenantId, [conversation.ChannelId], cancellationToken);
        var tagLookup = await tags.GetManyAsync(
            tenantId,
            conversation.Tags.Select(tag => tag.TagId).Distinct().ToList(),
            cancellationToken);

        return Result.Success(ConversationMapper.ToDetail(conversation, customerLookup, channelLookup, tagLookup));
    }
}