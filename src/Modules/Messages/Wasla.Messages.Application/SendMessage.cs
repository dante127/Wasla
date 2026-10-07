using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Contracts;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Application;

/// <summary>
/// Creates an outbound message (Pending) with optional idempotency. Provider dispatch is
/// wired in Phases 5-6 via the channel adapters; until then messages remain Pending.
/// </summary>
public sealed class SendMessageHandler(
    ITenantContext tenantContext,
    IConversationInfoProvider conversations,
    IMessageRepository messages,
    IConversationWriter conversationWriter,
    IOutboxRepository outbox,
    IMessagesUnitOfWork unitOfWork,
    IClock clock,
    IRealtimePublisher realtime)
{
    public async Task<Result<MessageItem>> HandleAsync(
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<MessageItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var typeName = string.IsNullOrWhiteSpace(request.Type) ? nameof(MessageType.Text) : request.Type;

        if (!Enum.TryParse<MessageType>(typeName, ignoreCase: true, out var messageType)
            || messageType == MessageType.System)
        {
            return Result.Failure<MessageItem>(
                new Error("messages.invalid_type", $"Unknown message type '{request.Type}'."));
        }

        var conversation = await conversations.GetAsync(tenantId, conversationId, cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<MessageItem>(
                new Error("conversations.not_found", "Conversation not found."));
        }

        if (conversation.Status is "Closed" or "Archived")
        {
            return Result.Failure<MessageItem>(
                new Error("messages.conversation_conflict", "This conversation is closed for new messages."));
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await messages.GetByIdempotencyKeyAsync(
                tenantId,
                request.IdempotencyKey.Trim(),
                cancellationToken);

            if (existing is not null)
            {
                var existingItems = await MapAsync(tenantId, [existing], cancellationToken);

                return Result.Success(existingItems[0]);
            }
        }

        var mediaIds = (request.MediaFileIds ?? []).Distinct().Select(id => new MediaFileId(id)).ToList();
        var mediaFiles = new List<MediaFile>();

        if (mediaIds.Count > 0)
        {
            mediaFiles = await messages.GetMediaFilesAsync(tenantId, mediaIds, cancellationToken);

            if (mediaFiles.Count != mediaIds.Count)
            {
                return Result.Failure<MessageItem>(
                    new Error("media.not_found", "One or more media files were not found."));
            }
        }

        var now = clock.UtcNow;

        Message message;

        try
        {
            message = Message.CreateOutbound(
                tenantId,
                conversationId,
                conversation.ChannelId,
                messageType,
                request.Body,
                request.IdempotencyKey,
                now);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<MessageItem>(new Error("messages.invalid", exception.Message));
        }

        foreach (var mediaFile in mediaFiles)
        {
            message.AddAttachment(mediaFile.Id, now);
        }

        await messages.AddAsync(message, cancellationToken);

        var outboxPayload = System.Text.Json.JsonSerializer.Serialize(
            new OutboxSendPayload(
                message.Id.Value,
                conversationId,
                messageType.ToString(),
                message.Body,
                mediaIds.Select(id => id.Value).ToList()),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        await outbox.AddAsync(
            OutboxMessage.Create(tenantId, OutboxMessage.MessageQueuedForSendKind, outboxPayload, now),
            cancellationToken);

        var preview = message.Body ?? $"({messageType})";
        await conversationWriter.RecordOutboundMessageAsync(
            tenantId,
            conversationId,
            message.Id.Value,
            preview,
            now,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await realtime.PublishToTenantAsync(
            tenantId,
            RealtimeEvents.NewMessage,
            new NewMessageEvent(conversationId, message.Id.Value, "Outbound", preview, now),
            cancellationToken);

        var items = await MapAsync(tenantId, [message], cancellationToken);

        return Result.Success(items[0]);
    }

    private async Task<List<MessageItem>> MapAsync(
        BuildingBlocks.Domain.TenantId tenantId,
        IReadOnlyCollection<Message> items,
        CancellationToken cancellationToken)
    {
        var mediaIds = items
            .SelectMany(message => message.Attachments.Select(attachment => attachment.MediaFileId))
            .Distinct()
            .ToList();

        var mediaLookup = (await messages.GetMediaFilesAsync(tenantId, mediaIds, cancellationToken))
            .ToDictionary(media => media.Id.Value);

        return items
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
    }
}
