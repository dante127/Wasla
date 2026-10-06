using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Paging;
using Wasla.Conversations.Application.Contracts;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Application;

/// <summary>
/// Cursor-paginated message history: newest first; use <c>before</c> for older pages,
/// <c>after</c> to fetch newer activity.
/// </summary>
public sealed class ListMessagesHandler(
    ITenantContext tenantContext,
    IConversationInfoProvider conversations,
    IMessageRepository messages)
{
    public async Task<Result<MessagesPage>> HandleAsync(
        Guid conversationId,
        string? before,
        string? after,
        int limit,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<MessagesPage>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if ((!string.IsNullOrWhiteSpace(before) && !CursorCodec.TryDecode(before, out _, out _))
            || (!string.IsNullOrWhiteSpace(after) && !CursorCodec.TryDecode(after, out _, out _)))
        {
            return Result.Failure<MessagesPage>(
                new Error("messages.invalid_cursor", "The pagination cursor is invalid."));
        }

        var conversation = await conversations.GetAsync(tenantId, conversationId, cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<MessagesPage>(
                new Error("conversations.not_found", "Conversation not found."));
        }

        var normalizedLimit = limit is < 1 or > 100 ? 50 : limit;

        var (items, hasMore) = await messages.ListByConversationAsync(
            tenantId,
            conversationId,
            before,
            after,
            normalizedLimit,
            cancellationToken);

        var mediaIds = items
            .SelectMany(message => message.Attachments.Select(attachment => attachment.MediaFileId))
            .Distinct()
            .ToList();

        var mediaLookup = (await messages.GetMediaFilesAsync(tenantId, mediaIds, cancellationToken))
            .ToDictionary(media => media.Id.Value);

        var mapped = items
            .Select(message => new MessageItem(
                message.Id.Value,
                message.ConversationId,
                message.Direction.ToString(),
                message.Type.ToString(),
                message.Body,
                message.Status.ToString(),
                message.ProviderMessageId,
                message.FailureReason,
                message.CreatedAt,
                message.SentAt,
                message.Attachments
                    .Select(attachment =>
                    {
                        var media = mediaLookup[attachment.MediaFileId.Value];

                        return new AttachmentItem(
                            attachment.Id.Value,
                            media.Id.Value,
                            media.FileName,
                            media.ContentType,
                            media.Size);
                    })
                    .ToList()))
            .ToList();

        string? nextCursor = null;

        if (hasMore && items.Count > 0)
        {
            var oldest = items[^1];
            nextCursor = CursorCodec.Encode(oldest.CreatedAt, oldest.Id.Value);
        }

        return Result.Success(new MessagesPage(mapped, nextCursor));
    }
}
