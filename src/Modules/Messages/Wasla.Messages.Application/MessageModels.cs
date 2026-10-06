namespace Wasla.Messages.Application;

public sealed record AttachmentItem(
    Guid Id,
    Guid MediaFileId,
    string FileName,
    string ContentType,
    long Size);

public sealed record MessageItem(
    Guid Id,
    Guid ConversationId,
    string Direction,
    string Type,
    string? Body,
    string Status,
    string? ProviderMessageId,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    IReadOnlyList<AttachmentItem> Attachments);

public sealed record SendMessageRequest(
    string? Type,
    string? Body,
    IReadOnlyList<Guid>? MediaFileIds,
    string? IdempotencyKey);

public sealed record MessagesPage(IReadOnlyList<MessageItem> Items, string? NextCursor);

public sealed record UploadMediaResult(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string Hash);
