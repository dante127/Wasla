using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application.Services;

/// <summary>
/// Inbound conversation resolution policy: a new customer message reopens a resolved
/// conversation, reuses an open/pending one, and otherwise starts a new conversation.
/// Closed/archived conversations are never resurrected.
/// </summary>
public sealed class ConversationResolver(
    IConversationRepository conversations,
    IConversationsUnitOfWork unitOfWork) : IConversationResolver
{
    public async Task<Guid> ResolveOrCreateForInboundAsync(
        TenantId tenantId,
        Guid customerId,
        Guid channelId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var existing = await conversations.GetLatestActiveForCustomerChannelAsync(
            tenantId,
            customerId,
            channelId,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == ConversationStatus.Resolved)
            {
                existing.ChangeStatus(ConversationStatus.Open, actorUserId: null, at);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return existing.Id.Value;
        }

        var created = Conversation.Create(tenantId, customerId, channelId, at);

        await conversations.AddAsync(created, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return created.Id.Value;
    }
}
