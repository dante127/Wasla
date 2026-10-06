using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Adds an internal note (with optional user/team mentions) to a conversation.</summary>
public sealed class AddConversationNoteHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IConversationRepository conversations,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<InternalNoteItem>> HandleAsync(
        Guid conversationId,
        AddConversationNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<InternalNoteItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if (currentUser.UserId is not { } authorUserId)
        {
            return Result.Failure<InternalNoteItem>(
                new Error("auth.unauthorized", "Not authenticated."));
        }

        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<InternalNoteItem>(
                new Error("conversations.not_found", "Conversation not found."));
        }

        InternalNote note;

        try
        {
            note = conversation.AddNote(
                authorUserId,
                request.Body,
                request.MentionUserIds ?? [],
                request.MentionTeamIds ?? [],
                clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<InternalNoteItem>(new Error("conversations.invalid", exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new InternalNoteItem(
            note.Id.Value,
            note.AuthorUserId,
            note.Body,
            note.Mentions
                .Select(mention => new NoteMentionItem(mention.MentionType.ToString(), mention.TargetId))
                .ToList(),
            note.CreatedAt));
    }
}
