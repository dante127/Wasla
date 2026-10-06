using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Domain;

/// <summary>
/// An agent-only note on a conversation. Deliberately a distinct type from messages so it
/// can never be dispatched to a customer channel (docs/domain-model.md §8).
/// </summary>
public sealed class InternalNote : Entity<InternalNoteId>
{
    private InternalNote()
    {
        Body = string.Empty;
    }

    private InternalNote(
        InternalNoteId id,
        ConversationId conversationId,
        Guid authorUserId,
        string body,
        DateTimeOffset now)
        : base(id)
    {
        ConversationId = conversationId;
        AuthorUserId = authorUserId;
        Body = body;
        CreatedAt = now;
    }

    public ConversationId ConversationId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public List<NoteMention> Mentions { get; private set; } = [];

    public static InternalNote Create(
        ConversationId conversationId,
        Guid authorUserId,
        string body,
        IReadOnlyCollection<Guid> mentionUserIds,
        IReadOnlyCollection<Guid> mentionTeamIds,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Note body is required.", nameof(body));
        }

        var note = new InternalNote(InternalNoteId.New(), conversationId, authorUserId, body.Trim(), now);

        foreach (var userId in mentionUserIds.Distinct())
        {
            if (userId != Guid.Empty && note.Mentions.All(mention => mention.TargetId != userId))
            {
                note.Mentions.Add(new NoteMention(note.Id, MentionType.User, userId));
            }
        }

        foreach (var teamId in mentionTeamIds.Distinct())
        {
            if (teamId != Guid.Empty && note.Mentions.All(mention => !(mention.MentionType == MentionType.Team && mention.TargetId == teamId)))
            {
                note.Mentions.Add(new NoteMention(note.Id, MentionType.Team, teamId));
            }
        }

        return note;
    }
}

/// <summary>A user or team mention inside an internal note.</summary>
public sealed class NoteMention
{
    private NoteMention()
    {
    }

    internal NoteMention(InternalNoteId noteId, MentionType mentionType, Guid targetId)
    {
        NoteId = noteId;
        MentionType = mentionType;
        TargetId = targetId;
    }

    public InternalNoteId NoteId { get; private set; }

    public MentionType MentionType { get; private set; }

    public Guid TargetId { get; private set; }
}
