namespace Wasla.Conversations.Domain;

/// <summary>A link assigning a catalog tag to a conversation.</summary>
public sealed class ConversationTag
{
    private ConversationTag()
    {
    }

    internal ConversationTag(ConversationId conversationId, Guid tagId)
    {
        ConversationId = conversationId;
        TagId = tagId;
    }

    public ConversationId ConversationId { get; private set; }

    public Guid TagId { get; private set; }
}

/// <summary>An append-only timeline entry for a conversation.</summary>
public sealed class ConversationTimelineEntry
{
    private ConversationTimelineEntry()
    {
        Type = string.Empty;
    }

    internal ConversationTimelineEntry(
        TimelineEntryId id,
        ConversationId conversationId,
        string type,
        string? data,
        Guid? actorUserId,
        DateTimeOffset occurredAt)
    {
        Id = id;
        ConversationId = conversationId;
        Type = type;
        Data = data;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
    }

    public TimelineEntryId Id { get; private set; }

    public ConversationId ConversationId { get; private set; }

    public string Type { get; private set; }

    public string? Data { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
