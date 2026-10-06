using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Starts a new conversation for a customer on a channel (agent-initiated outbound).</summary>
public sealed class CreateConversationHandler(
    ITenantContext tenantContext,
    IConversationRepository conversations,
    ICustomerInfoProvider customers,
    IChannelInfoProvider channels,
    IConversationsUnitOfWork unitOfWork,
    ITagInfoProvider tags,
    IClock clock)
{
    public async Task<Result<ConversationDetail>> HandleAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ConversationDetail>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var customer = await customers.GetAsync(tenantId, request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<ConversationDetail>(new Error("customers.not_found", "Customer not found."));
        }

        var channel = await channels.GetAsync(tenantId, request.ChannelId, cancellationToken);

        if (channel is null)
        {
            return Result.Failure<ConversationDetail>(new Error("channels.not_found", "Channel not found."));
        }

        var conversation = Conversation.Create(tenantId, request.CustomerId, request.ChannelId, clock.UtcNow);

        await conversations.AddAsync(conversation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var customerLookup = new Dictionary<Guid, CustomerSummary> { [customer.Id] = customer };
        var channelLookup = new Dictionary<Guid, ChannelInfo> { [channel.Id] = channel };
        var tagLookup = await tags.GetManyAsync(tenantId, [], cancellationToken);

        return Result.Success(ConversationMapper.ToDetail(conversation, customerLookup, channelLookup, tagLookup));
    }
}