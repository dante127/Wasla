using Wasla.BuildingBlocks.Domain;

namespace Wasla.Messages.Domain;

/// <summary>
/// The normalized message model. All channels map into this shape; provider-specific
/// extras live only in <see cref="ProviderMetadata"/> (owned by adapter code).
/// </summary>
public sealed class Message : AggregateRoot<MessageId>, ITenantOwned
{
    private Message()
    {
    }

    private Message(
        MessageId id,
        TenantId tenantId,
        Guid conversationId,
        Guid channelId,
        MessageDirection direction,
        MessageType type,
        string? body,
        string? idempotencyKey,
        DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        ConversationId = conversationId;
        ChannelId = channelId;
        Direction = direction;
        Type = type;
        Body = body;
        IdempotencyKey = idempotencyKey;
        Status = MessageStatus.Pending;
        CreatedAt = now;

        if (direction == MessageDirection.Inbound)
        {
            Status = MessageStatus.Sent;
            SentAt = now;
        }
    }

    public TenantId TenantId { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid ChannelId { get; private set; }

    public MessageDirection Direction { get; private set; }

    public MessageType Type { get; private set; }

    public string? Body { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? ProviderMetadata { get; private set; }

    public MessageStatus Status { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public string? FailureReason { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public List<Attachment> Attachments { get; private set; } = [];

    public static Message CreateInbound(
        TenantId tenantId,
        Guid conversationId,
        Guid channelId,
        MessageType type,
        string? body,
        string? providerMessageId,
        DateTimeOffset now)
    {
        var message = new Message(
            MessageId.New(),
            tenantId,
            conversationId,
            channelId,
            MessageDirection.Inbound,
            type,
            string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            null,
            now);

        message.ProviderMessageId = providerMessageId?.Trim();

        return message;
    }

    public static Message CreateOutbound(
        TenantId tenantId,
        Guid conversationId,
        Guid channelId,
        MessageType type,
        string? body,
        string? idempotencyKey,
        DateTimeOffset now)
    {
        if (type == MessageType.System)
        {
            throw new ArgumentException("System messages cannot be created through this path.", nameof(type));
        }

        if (type == MessageType.Text && string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Message body is required for text messages.", nameof(body));
        }

        if (body is { Length: > 8000 })
        {
            throw new ArgumentException("Message body must be at most 8000 characters.", nameof(body));
        }

        var message = new Message(
            MessageId.New(),
            tenantId,
            conversationId,
            channelId,
            MessageDirection.Outbound,
            type,
            string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            idempotencyKey?.Trim(),
            now);

        message.RaiseDomainEvent(new MessageQueuedForSend(Guid.NewGuid(), now, message.Id, tenantId, conversationId));

        return message;
    }

    public Attachment AddAttachment(MediaFileId mediaFileId, DateTimeOffset now)
    {
        var attachment = new Attachment(AttachmentId.New(), Id, mediaFileId, now);
        Attachments.Add(attachment);

        return attachment;
    }

    public void MarkSent(string? providerMessageId, DateTimeOffset now)
    {
        if (!Advance(MessageStatus.Sent, now))
        {
            return;
        }

        ProviderMessageId ??= providerMessageId;
        SentAt = now;
        RaiseDomainEvent(new MessageSent(Guid.NewGuid(), now, Id, TenantId, ConversationId, ProviderMessageId));
    }

    public void MarkDelivered(DateTimeOffset now)
    {
        if (Advance(MessageStatus.Delivered, now))
        {
            DeliveredAt = now;
        }
    }

    public void MarkRead(DateTimeOffset now)
    {
        if (Advance(MessageStatus.Read, now))
        {
            ReadAt = now;
        }
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        if (Status is MessageStatus.Failed or MessageStatus.Delivered or MessageStatus.Read)
        {
            return;
        }

        var previous = Status;
        Status = MessageStatus.Failed;
        FailureReason = reason;
        RaiseDomainEvent(new MessageStatusChanged(Guid.NewGuid(), now, Id, TenantId, ConversationId, previous, Status));
    }

    public void AttachProviderMetadata(string metadata)
    {
        ProviderMetadata = metadata;
    }

    private bool Advance(MessageStatus target, DateTimeOffset now)
    {
        if (Status is MessageStatus.Failed)
        {
            return false;
        }

        if (Status >= target)
        {
            return false; // monotonic: Pending -> Sent -> Delivered -> Read
        }

        var previous = Status;
        Status = target;
        RaiseDomainEvent(new MessageStatusChanged(Guid.NewGuid(), now, Id, TenantId, ConversationId, previous, target));

        return true;
    }
}

public sealed record MessageQueuedForSend(
    Guid EventId,
    DateTimeOffset OccurredAt,
    MessageId MessageId,
    TenantId TenantId,
    Guid ConversationId) : IDomainEvent;

public sealed record MessageSent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    MessageId MessageId,
    TenantId TenantId,
    Guid ConversationId,
    string? ProviderMessageId) : IDomainEvent;

public sealed record MessageStatusChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    MessageId MessageId,
    TenantId TenantId,
    Guid ConversationId,
    MessageStatus From,
    MessageStatus To) : IDomainEvent;
