using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;
using Wasla.Identity.Application.Contracts;

namespace Wasla.Conversations.Application;

/// <summary>Assigns a conversation to a user or a team.</summary>
public sealed class AssignConversationHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IConversationRepository conversations,
    ICustomerInfoProvider customers,
    IChannelInfoProvider channels,
    ITagInfoProvider tags,
    IMembershipVerifier membershipVerifier,
    IConversationsUnitOfWork unitOfWork,
    IClock clock,
    IRealtimePublisher realtime)
{
    public async Task<Result<ConversationDetail>> HandleAsync(
        Guid conversationId,
        AssignConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ConversationDetail>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if (request.UserId is null && request.TeamId is null)
        {
            return Result.Failure<ConversationDetail>(
                new Error("conversations.invalid", "Provide either userId or teamId."));
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

        var actorUserId = currentUser.UserId;
        var now = clock.UtcNow;

        if (request.UserId is { } userId)
        {
            if (!await membershipVerifier.IsActiveMemberAsync(tenantId, new BuildingBlocks.Domain.UserId(userId), cancellationToken))
            {
                return Result.Failure<ConversationDetail>(
                    new Error("users.not_found", "The target user is not an active member of this tenant."));
            }

            conversation.AssignToUser(userId, actorUserId, now);
        }
        else
        {
            conversation.AssignToTeam(request.TeamId!.Value, actorUserId, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await realtime.PublishToTenantAsync(
            tenantId,
            RealtimeEvents.ConversationAssigned,
            new ConversationAssignedEvent(conversationId, conversation.AssignedUserId, conversation.AssignedTeamId, now),
            cancellationToken);

        var customerLookup = await customers.GetManyAsync(tenantId, [conversation.CustomerId], cancellationToken);
        var channelLookup = await channels.GetManyAsync(tenantId, [conversation.ChannelId], cancellationToken);
        var tagLookup = await tags.GetManyAsync(
            tenantId,
            conversation.Tags.Select(tag => tag.TagId).Distinct().ToList(),
            cancellationToken);

        return Result.Success(ConversationMapper.ToDetail(conversation, customerLookup, channelLookup, tagLookup));
    }
}