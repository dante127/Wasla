using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Domain;

/// <summary>
/// The unified conversation aggregate: lifecycle, priority, assignment, tags, internal
/// notes and its timeline. Message data itself lives in the Messages module; this
/// aggregate keeps denormalized counters for the inbox list.
/// </summary>
public sealed class Conversation : AggregateRoot<ConversationId>, ITenantOwned
{
    private static readonly Dictionary<ConversationStatus, ConversationStatus[]> AllowedTransitions = new()
    {
        [ConversationStatus.Open] = [ConversationStatus.Pending, ConversationStatus.Resolved],
        [ConversationStatus.Pending] = [ConversationStatus.Open, ConversationStatus.Resolved],
        [ConversationStatus.Resolved] = [ConversationStatus.Open, ConversationStatus.Closed, ConversationStatus.Archived],
        [ConversationStatus.Closed] = [ConversationStatus.Open, ConversationStatus.Archived],
        [ConversationStatus.Archived] = [],
    };

    private Conversation()
    {
    }

    private Conversation(
        ConversationId id,
        TenantId tenantId,
        Guid customerId,
        Guid channelId,
        DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        ChannelId = channelId;
        Status = ConversationStatus.Open;
        Priority = ConversationPriority.Normal;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ChannelId { get; private set; }

    public ConversationStatus Status { get; private set; }

    public ConversationPriority Priority { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public Guid? AssignedTeamId { get; private set; }

    public DateTimeOffset? LastMessageAt { get; private set; }

    public string? LastMessagePreview { get; private set; }

    public int UnreadCount { get; private set; }

    public DateTimeOffset? FirstResponseAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset? LastReadAt { get; private set; }

    public DateTimeOffset? SlaDueAt { get; private set; }

    public DateTimeOffset? SlaBreachedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public List<ConversationTag> Tags { get; private set; } = [];

    public List<InternalNote> Notes { get; private set; } = [];

    public List<ConversationTimelineEntry> Timeline { get; private set; } = [];

    public static Conversation Create(
        TenantId tenantId,
        Guid customerId,
        Guid channelId,
        DateTimeOffset now)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (channelId == Guid.Empty)
        {
            throw new ArgumentException("Channel id is required.", nameof(channelId));
        }

        var conversation = new Conversation(ConversationId.New(), tenantId, customerId, channelId, now);
        conversation.RaiseDomainEvent(new ConversationCreated(Guid.NewGuid(), now, conversation.Id, tenantId, customerId, channelId));
        conversation.AddTimelineEntry("conversation.created", null, null, now);

        return conversation;
    }

    public void AssignToUser(Guid userId, Guid? actorUserId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        AssignedUserId = userId;
        AssignedTeamId = null;
        UpdatedAt = now;
        RaiseDomainEvent(new ConversationAssigned(Guid.NewGuid(), now, Id, TenantId, userId, null, actorUserId));
        AddTimelineEntry("conversation.assigned", $"user:{userId}", actorUserId, now);
    }

    public void AssignToTeam(Guid teamId, Guid? actorUserId, DateTimeOffset now)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Team id is required.", nameof(teamId));
        }

        AssignedTeamId = teamId;
        AssignedUserId = null;
        UpdatedAt = now;
        RaiseDomainEvent(new ConversationAssigned(Guid.NewGuid(), now, Id, TenantId, null, teamId, actorUserId));
        AddTimelineEntry("conversation.assigned", $"team:{teamId}", actorUserId, now);
    }

    public void SetPriority(ConversationPriority priority, Guid? actorUserId, DateTimeOffset now)
    {
        if (Priority == priority)
        {
            return;
        }

        Priority = priority;
        UpdatedAt = now;
        AddTimelineEntry("priority.changed", priority.ToString(), actorUserId, now);
    }

    public void ChangeStatus(ConversationStatus target, Guid? actorUserId, DateTimeOffset now)
    {
        if (Status == target)
        {
            return;
        }

        if (!AllowedTransitions[Status].Contains(target))
        {
            throw new InvalidOperationException($"Cannot change conversation status from {Status} to {target}.");
        }

        var previous = Status;
        Status = target;
        UpdatedAt = now;

        if (target == ConversationStatus.Resolved)
        {
            ResolvedAt = now;
        }
        else if (target == ConversationStatus.Open && previous is ConversationStatus.Resolved or ConversationStatus.Closed)
        {
            ResolvedAt = null;
        }

        RaiseDomainEvent(new ConversationStatusChanged(Guid.NewGuid(), now, Id, TenantId, previous, target, actorUserId));
        AddTimelineEntry("status.changed", $"{previous} -> {target}", actorUserId, now);
    }

    public void AddTag(Guid tagId, Guid? actorUserId, DateTimeOffset now)
    {
        if (Tags.Any(tag => tag.TagId == tagId))
        {
            return;
        }

        Tags.Add(new ConversationTag(Id, tagId));
        UpdatedAt = now;
        RaiseDomainEvent(new ConversationTagged(Guid.NewGuid(), now, Id, TenantId, tagId));
        AddTimelineEntry("tag.added", tagId.ToString(), actorUserId, now);
    }

    public bool RemoveTag(Guid tagId, Guid? actorUserId, DateTimeOffset now)
    {
        var removed = Tags.RemoveAll(tag => tag.TagId == tagId) > 0;

        if (removed)
        {
            UpdatedAt = now;
            RaiseDomainEvent(new ConversationUntagged(Guid.NewGuid(), now, Id, TenantId, tagId));
            AddTimelineEntry("tag.removed", tagId.ToString(), actorUserId, now);
        }

        return removed;
    }

    public InternalNote AddNote(
        Guid authorUserId,
        string body,
        IReadOnlyCollection<Guid> mentionUserIds,
        IReadOnlyCollection<Guid> mentionTeamIds,
        DateTimeOffset now)
    {
        var note = InternalNote.Create(Id, authorUserId, body, mentionUserIds, mentionTeamIds, now);
        Notes.Add(note);
        UpdatedAt = now;
        RaiseDomainEvent(new InternalNoteAdded(Guid.NewGuid(), now, Id, TenantId, note.Id));
        AddTimelineEntry("note.added", null, authorUserId, now);

        return note;
    }

    /// <summary>
    /// Applies message-derived counters (called by the Messages module via the module
    /// writer contract). Internal notes never flow through this path.
    /// </summary>
    public void RecordMessage(Guid? messageId, bool inbound, string? preview, DateTimeOffset at)
    {
        LastMessageAt = at;
        LastMessagePreview = TruncatePreview(preview);
        UpdatedAt = at;

        if (inbound)
        {
            UnreadCount += 1;
            AddTimelineEntry("message.received", messageId?.ToString(), null, at);
        }
        else
        {
            FirstResponseAt ??= at;
            AddTimelineEntry("message.sent", messageId?.ToString(), null, at);
        }
    }

    public void MarkRead(DateTimeOffset now)
    {
        UnreadCount = 0;
        LastReadAt = now;
    }

    public void AddTimelineEntry(string type, string? data, Guid? actorUserId, DateTimeOffset at)
    {
        Timeline.Add(new ConversationTimelineEntry(TimelineEntryId.New(), Id, type, data, actorUserId, at));

        if (Timeline.Count > 500)
        {
            Timeline.RemoveAt(0);
        }
    }

    private static string? TruncatePreview(string? preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            return null;
        }

        var flattened = preview.ReplaceLineEndings(" ").Trim();

        return flattened.Length <= 300 ? flattened : flattened[..300];
    }
}
