using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application.Abstractions;

public interface IConversationRepository
{
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken);

    Task<Conversation?> GetByIdAsync(TenantId tenantId, ConversationId conversationId, CancellationToken cancellationToken);

    Task<(List<Conversation> Items, bool HasMore)> ListAsync(
        TenantId tenantId,
        ConversationListQuery query,
        CancellationToken cancellationToken);
}
