namespace Wasla.Conversations.Domain;

/// <summary>Lifecycle status of a conversation (see docs/domain-model.md §8 for transitions).</summary>
public enum ConversationStatus
{
    Open = 0,
    Pending = 1,
    Resolved = 2,
    Closed = 3,
    Archived = 4,
}

/// <summary>Conversation priority.</summary>
public enum ConversationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3,
}

/// <summary>Kind of entity a note mention targets.</summary>
public enum MentionType
{
    User = 0,
    Team = 1,
}
