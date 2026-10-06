using Wasla.BuildingBlocks.Application;
using Wasla.Channels.Application.Contracts;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;
using Wasla.BuildingBlocks.Application.Contracts;

namespace Wasla.Conversations.Application;

/// <summary>Returns the conversation detail view.</summary>
public sealed class GetConversationHandler(
    ITenantContext tenantContext,
    IConversationRepository conversations,
    ICustomerInfoProvider customers,
    IChannelInfoProvider channels,
    ITagInfoProvider tags)
{
    public async Task<Result<ConversationDetail>> HandleAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ConversationDetail>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var conversation = await conversations.GetByIdAsync(tenantId, new ConversationId(conversationId), cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<ConversationDetail>(
                new Error("conversations.not_found", "Conversation not found."));
        }

        var customerLookup = await customers.GetManyAsync(tenantId, [conversation.CustomerId], cancellationToken);
        var channelLookup = await channels.GetManyAsync(tenantId, [conversation.ChannelId], cancellationToken);
        var tagLookup = await tags.GetManyAsync(
            tenantId,
            conversation.Tags.Select(tag => tag.TagId).Distinct().ToList(),
            cancellationToken);

        return Result.Success(ConversationMapper.ToDetail(conversation, customerLookup, channelLookup, tagLookup));
    }
}
