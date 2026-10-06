using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Changes the conversation priority.</summary>
public sealed class SetConversationPriorityHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IConversationRepository conversations,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(
        Guid conversationId,
        SetConversationPriorityRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure(new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if (!Enum.TryParse<ConversationPriority>(request.Priority, ignoreCase: true, out var priority))
        {
            return Result.Failure(
                new Error("conversations.invalid", $"Unknown priority '{request.Priority}'."));
        }

        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure(new Error("conversations.not_found", "Conversation not found."));
        }

        conversation.SetPriority(priority, currentUser.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
