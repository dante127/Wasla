namespace Wasla.Conversations.Application;

public sealed record ConversationTagItem(Guid Id, string Key, string Name, string? Color);

public sealed record ConversationCustomerRef(Guid Id, string DisplayName);

public sealed record ConversationChannelRef(Guid Id, string Type, string DisplayName);

public sealed record ConversationListItem(
    Guid Id,
    string Status,
    string Priority,
    ConversationCustomerRef Customer,
    ConversationChannelRef Channel,
    Guid? AssignedUserId,
    Guid? AssignedTeamId,
    IReadOnlyList<ConversationTagItem> Tags,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    int UnreadCount,
    DateTimeOffset UpdatedAt);

public sealed record NoteMentionItem(string Type, Guid TargetId);

public sealed record InternalNoteItem(
    Guid Id,
    Guid AuthorUserId,
    string Body,
    IReadOnlyList<NoteMentionItem> Mentions,
    DateTimeOffset CreatedAt);

public sealed record ConversationDetail(
    Guid Id,
    string Status,
    string Priority,
    ConversationCustomerRef Customer,
    ConversationChannelRef Channel,
    Guid? AssignedUserId,
    Guid? AssignedTeamId,
    IReadOnlyList<ConversationTagItem> Tags,
    IReadOnlyList<InternalNoteItem> Notes,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    int UnreadCount,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? SlaDueAt,
    DateTimeOffset? SlaBreachedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ConversationListQuery(
    string? Cursor,
    int Limit,
    Guid? TeamId,
    Guid? AssignedUserId,
    string? Status,
    string? Priority,
    IReadOnlyList<Guid>? TagIds,
    bool? Unread,
    Guid? CustomerId,
    Guid? ChannelId);

public sealed record ConversationListResponse(IReadOnlyList<ConversationListItem> Items, string? NextCursor);

public sealed record CreateConversationRequest(Guid CustomerId, Guid ChannelId);

public sealed record AssignConversationRequest(Guid? UserId, Guid? TeamId);

public sealed record SetConversationPriorityRequest(string Priority);

public sealed record AddConversationTagsRequest(IReadOnlyList<Guid> TagIds);

public sealed record AddConversationNoteRequest(
    string Body,
    IReadOnlyList<Guid>? MentionUserIds,
    IReadOnlyList<Guid>? MentionTeamIds);

public sealed record ConversationTimelineItem(
    Guid Id,
    string Type,
    string? Data,
    Guid? ActorUserId,
    DateTimeOffset OccurredAt);

public sealed record ConversationTimelineResponse(
    IReadOnlyList<ConversationTimelineItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record QuickReplyItem(Guid Id, string Key, string Text);

public sealed record CreateQuickReplyRequest(string Key, string Text);

public sealed record RenderQuickReplyRequest(Guid CustomerId);

public sealed record RenderQuickReplyResponse(string Text);
