using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;
using Wasla.Messages.Domain;

namespace Wasla.UnitTests;

public sealed class ConversationDomainTests
{
    [Fact]
    public void Create_raises_event_and_adds_timeline_entry()
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = Conversation.Create(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), now);

        Assert.Equal(ConversationStatus.Open, conversation.Status);
        Assert.Equal(ConversationPriority.Normal, conversation.Priority);
        Assert.Single(conversation.DomainEvents);
        Assert.IsType<ConversationCreated>(conversation.DomainEvents.First());
        Assert.Single(conversation.Timeline);
    }

    [Fact]
    public void Status_transitions_follow_the_state_machine()
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = Conversation.Create(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), now);

        conversation.ChangeStatus(ConversationStatus.Pending, null, now);
        conversation.ChangeStatus(ConversationStatus.Open, null, now);

        conversation.ChangeStatus(ConversationStatus.Resolved, null, now);
        Assert.NotNull(conversation.ResolvedAt);

        conversation.ChangeStatus(ConversationStatus.Open, null, now);
        Assert.Null(conversation.ResolvedAt);

        conversation.ChangeStatus(ConversationStatus.Resolved, null, now);
        conversation.ChangeStatus(ConversationStatus.Closed, null, now);
        conversation.ChangeStatus(ConversationStatus.Archived, null, now);

        Assert.Throws<InvalidOperationException>(() =>
            conversation.ChangeStatus(ConversationStatus.Open, null, now));

        var fresh = Conversation.Create(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), now);
        Assert.Throws<InvalidOperationException>(() =>
            fresh.ChangeStatus(ConversationStatus.Closed, null, now));
    }

    [Fact]
    public void Assignment_clears_the_other_target()
    {
        var conversation = Conversation.Create(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        conversation.AssignToUser(userId, null, DateTimeOffset.UtcNow);
        Assert.Equal(userId, conversation.AssignedUserId);
        Assert.Null(conversation.AssignedTeamId);

        conversation.AssignToTeam(teamId, null, DateTimeOffset.UtcNow);
        Assert.Equal(teamId, conversation.AssignedTeamId);
        Assert.Null(conversation.AssignedUserId);
    }

    [Fact]
    public void Tags_notes_and_mentions_behave_correctly()
    {
        var conversation = Conversation.Create(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var tagId = Guid.NewGuid();

        conversation.AddTag(tagId, null, DateTimeOffset.UtcNow);
        conversation.AddTag(tagId, null, DateTimeOffset.UtcNow);
        Assert.Single(conversation.Tags);
        Assert.True(conversation.RemoveTag(tagId, null, DateTimeOffset.UtcNow));
        Assert.False(conversation.RemoveTag(tagId, null, DateTimeOffset.UtcNow));

        var mentionUser = Guid.NewGuid();
        var note = conversation.AddNote(Guid.NewGuid(), "Please review.", [mentionUser, mentionUser], [], DateTimeOffset.UtcNow);

        Assert.Single(note.Mentions);
        Assert.Equal(mentionUser, note.Mentions[0].TargetId);
        Assert.Throws<ArgumentException>(() =>
            conversation.AddNote(Guid.NewGuid(), "  ", [], [], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RecordMessage_updates_counters_and_markread_clears_unread()
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = Conversation.Create(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), now);

        conversation.RecordMessage(Guid.NewGuid(), inbound: true, "Hello there", now);

        Assert.Equal(1, conversation.UnreadCount);
        Assert.Equal("Hello there", conversation.LastMessagePreview);

        conversation.RecordMessage(Guid.NewGuid(), inbound: false, "Reply", now);
        Assert.Equal(1, conversation.UnreadCount);
        Assert.NotNull(conversation.FirstResponseAt);

        conversation.MarkRead(now);
        Assert.Equal(0, conversation.UnreadCount);
        Assert.NotNull(conversation.LastReadAt);
    }
}

public sealed class MessageDomainTests
{
    [Fact]
    public void Outbound_message_advances_monotonically_and_ignores_regressions()
    {
        var now = DateTimeOffset.UtcNow;
        var message = Message.CreateOutbound(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "Hi", null, now);

        Assert.Equal(MessageStatus.Pending, message.Status);

        message.MarkSent("provider-1", now);
        message.MarkDelivered(now);
        message.MarkRead(now);
        Assert.Equal(MessageStatus.Read, message.Status);

        message.MarkDelivered(now); // regression ignored
        Assert.Equal(MessageStatus.Read, message.Status);
    }

    [Fact]
    public void Failed_is_terminal()
    {
        var message = Message.CreateOutbound(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "Hi", null, DateTimeOffset.UtcNow);
        message.MarkFailed("blocked", DateTimeOffset.UtcNow);

        Assert.Equal(MessageStatus.Failed, message.Status);
        message.MarkSent("late", DateTimeOffset.UtcNow);
        Assert.Equal(MessageStatus.Failed, message.Status);
    }

    [Fact]
    public void Inbound_message_is_created_as_sent()
    {
        var message = Message.CreateInbound(
            TenantId.New(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageType.Text,
            "Hello",
            "seed-1",
            DateTimeOffset.UtcNow);

        Assert.Equal(MessageDirection.Inbound, message.Direction);
        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.NotNull(message.SentAt);
        Assert.Equal("seed-1", message.ProviderMessageId);
    }

    [Fact]
    public void Text_requires_body_and_system_type_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Message.CreateOutbound(
            TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "  ", null, DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => Message.CreateOutbound(
            TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), MessageType.System, "x", null, DateTimeOffset.UtcNow));
    }
}

public sealed class QuickReplyRendererTests
{
    [Fact]
    public void Render_replaces_allowlisted_variables_case_insensitively()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["customer.name"] = "Layla",
            ["agent.name"] = "Agent Smith",
        };

        var rendered = QuickReplyRenderer.Render(
            "Hello {{ customer.name }}, this is {{AGENT.NAME}}.",
            values);

        Assert.Equal("Hello Layla, this is Agent Smith.", rendered);
    }

    [Fact]
    public void Render_turns_missing_values_into_empty_strings()
    {
        var rendered = QuickReplyRenderer.Render("Hi {{customer.name}}!", new Dictionary<string, string?>());
        Assert.Equal("Hi !", rendered);
    }

    [Fact]
    public void Unknown_variables_are_rejected_at_validation_and_creation()
    {
        Assert.Throws<ArgumentException>(() => QuickReplyRenderer.ValidateTemplate("{{system.exec}}"));
        Assert.Throws<ArgumentException>(() => QuickReplyRenderer.ValidateTemplate("{{customer.password}}"));

        Assert.Throws<ArgumentException>(() => QuickReply.Create(
            TenantId.New(), "evil", "Try {{hacker.code}}", DateTimeOffset.UtcNow));
    }
}
