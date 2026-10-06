using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Conversations.Domain;
using Wasla.Conversations.Application.Contracts;

namespace Wasla.Conversations.Application;

/// <summary>Maps conversation aggregates to DTOs using batched cross-module lookups.</summary>
internal static class ConversationMapper
{
    public static ConversationListItem ToListItem(
        Conversation conversation,
        IReadOnlyDictionary<Guid, CustomerSummary> customers,
        IReadOnlyDictionary<Guid, ChannelInfo> channels,
        IReadOnlyDictionary<Guid, TagInfo> tags) =>
        new(
            conversation.Id.Value,
            conversation.Status.ToString(),
            conversation.Priority.ToString(),
            CustomerRef(conversation.CustomerId, customers),
            ChannelRef(conversation.ChannelId, channels),
            conversation.AssignedUserId,
            conversation.AssignedTeamId,
            MapTags(conversation, tags),
            conversation.LastMessageAt,
            conversation.LastMessagePreview,
            conversation.UnreadCount,
            conversation.UpdatedAt);

    public static ConversationDetail ToDetail(
        Conversation conversation,
        IReadOnlyDictionary<Guid, CustomerSummary> customers,
        IReadOnlyDictionary<Guid, ChannelInfo> channels,
        IReadOnlyDictionary<Guid, TagInfo> tags) =>
        new(
            conversation.Id.Value,
            conversation.Status.ToString(),
            conversation.Priority.ToString(),
            CustomerRef(conversation.CustomerId, customers),
            ChannelRef(conversation.ChannelId, channels),
            conversation.AssignedUserId,
            conversation.AssignedTeamId,
            MapTags(conversation, tags),
            conversation.Notes
                .OrderByDescending(note => note.CreatedAt)
                .Select(note => new InternalNoteItem(
                    note.Id.Value,
                    note.AuthorUserId,
                    note.Body,
                    note.Mentions
                        .Select(mention => new NoteMentionItem(mention.MentionType.ToString(), mention.TargetId))
                        .ToList(),
                    note.CreatedAt))
                .ToList(),
            conversation.LastMessageAt,
            conversation.LastMessagePreview,
            conversation.UnreadCount,
            conversation.FirstResponseAt,
            conversation.ResolvedAt,
            conversation.SlaDueAt,
            conversation.SlaBreachedAt,
            conversation.CreatedAt,
            conversation.UpdatedAt);

    private static ConversationCustomerRef CustomerRef(
        Guid customerId,
        IReadOnlyDictionary<Guid, CustomerSummary> customers) =>
        customers.TryGetValue(customerId, out var summary)
            ? new ConversationCustomerRef(customerId, summary.DisplayName)
            : new ConversationCustomerRef(customerId, "Unknown");

    private static ConversationChannelRef ChannelRef(
        Guid channelId,
        IReadOnlyDictionary<Guid, ChannelInfo> channels) =>
        channels.TryGetValue(channelId, out var info)
            ? new ConversationChannelRef(channelId, info.Type, info.DisplayName)
            : new ConversationChannelRef(channelId, "Unknown", "Unknown");

    private static IReadOnlyList<ConversationTagItem> MapTags(
        Conversation conversation,
        IReadOnlyDictionary<Guid, TagInfo> tags) =>
        conversation.Tags
            .Where(tag => tags.ContainsKey(tag.TagId))
            .Select(tag =>
            {
                var info = tags[tag.TagId];
                return new ConversationTagItem(info.Id, info.Key, info.Name, info.Color);
            })
            .ToList();
}