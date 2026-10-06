using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Adds catalog tags to a conversation.</summary>
public sealed class AddConversationTagsHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IConversationRepository conversations,
    ITagInfoProvider tags,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<ConversationTagItem>>> HandleAsync(
        Guid conversationId,
        AddConversationTagsRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<ConversationTagItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var tagIds = request.TagIds.Distinct().ToList();

        if (tagIds.Count == 0)
        {
            return Result.Failure<IReadOnlyList<ConversationTagItem>>(
                new Error("conversations.invalid", "At least one tag id is required."));
        }

        if (!await tags.AllExistAsync(tenantId, tagIds, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ConversationTagItem>>(
                new Error("tags.not_found", "One or more tags were not found."));
        }

        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<IReadOnlyList<ConversationTagItem>>(
                new Error("conversations.not_found", "Conversation not found."));
        }

        foreach (var tagId in tagIds)
        {
            conversation.AddTag(tagId, currentUser.UserId, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var lookup = await tags.GetManyAsync(tenantId, tagIds, cancellationToken);

        return Result.Success<IReadOnlyList<ConversationTagItem>>(
            lookup.Values.Select(info => new ConversationTagItem(info.Id, info.Key, info.Name, info.Color)).ToList());
    }
}

/// <summary>Removes a tag from a conversation.</summary>
public sealed class RemoveConversationTagHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IConversationRepository conversations,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(
        Guid conversationId,
        Guid tagId,
        CancellationToken cancellationToken)
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

        if (!conversation.RemoveTag(tagId, currentUser.UserId, clock.UtcNow))
        {
            return Result.Failure(new Error("conversations.tag_not_found", "The conversation does not have this tag."));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
