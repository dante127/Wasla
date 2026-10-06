using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Marks the conversation as read for the current agent (MVP: single read marker).</summary>
public sealed class MarkConversationReadHandler(
    ITenantContext tenantContext,
    IConversationRepository conversations,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure(new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure(new Error("conversations.not_found", "Conversation not found."));
        }

        conversation.MarkRead(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
