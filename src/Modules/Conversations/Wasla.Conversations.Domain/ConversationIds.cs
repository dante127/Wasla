using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Domain;

public readonly record struct ConversationId(Guid Value)
{
    public static ConversationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct InternalNoteId(Guid Value)
{
    public static InternalNoteId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct TimelineEntryId(Guid Value)
{
    public static TimelineEntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct QuickReplyId(Guid Value)
{
    public static QuickReplyId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
