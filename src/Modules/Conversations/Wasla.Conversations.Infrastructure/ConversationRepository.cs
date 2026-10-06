using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application.Paging;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure;

public sealed class ConversationRepository(ConversationsDbContext dbContext) : IConversationRepository
{
    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken) =>
        await dbContext.Conversations.AddAsync(conversation, cancellationToken);

    public Task<Conversation?> GetByIdAsync(
        TenantId tenantId,
        ConversationId conversationId,
        CancellationToken cancellationToken) =>
        dbContext.Conversations
            .Include(conversation => conversation.Tags)
            .Include(conversation => conversation.Notes)
                .ThenInclude(note => note.Mentions)
            .Include(conversation => conversation.Timeline)
            .FirstOrDefaultAsync(
                conversation => conversation.TenantId == tenantId && conversation.Id == conversationId,
                cancellationToken);

    public async Task<(List<Conversation> Items, bool HasMore)> ListAsync(
        TenantId tenantId,
        ConversationListQuery query,
        CancellationToken cancellationToken)
    {
        var queryable = dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Tags)
            .Where(conversation => conversation.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ConversationStatus>(query.Status, ignoreCase: true, out var status))
        {
            queryable = queryable.Where(conversation => conversation.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority)
            && Enum.TryParse<ConversationPriority>(query.Priority, ignoreCase: true, out var priority))
        {
            queryable = queryable.Where(conversation => conversation.Priority == priority);
        }

        if (query.TeamId is { } teamId)
        {
            queryable = queryable.Where(conversation => conversation.AssignedTeamId == teamId);
        }

        if (query.AssignedUserId is { } assignedUserId)
        {
            queryable = queryable.Where(conversation => conversation.AssignedUserId == assignedUserId);
        }

        if (query.CustomerId is { } customerId)
        {
            queryable = queryable.Where(conversation => conversation.CustomerId == customerId);
        }

        if (query.ChannelId is { } channelId)
        {
            queryable = queryable.Where(conversation => conversation.ChannelId == channelId);
        }

        if (query.Unread == true)
        {
            queryable = queryable.Where(conversation => conversation.UnreadCount > 0);
        }

        if (query.TagIds is { Count: > 0 })
        {
            var tagIds = query.TagIds.Distinct().ToList();

            queryable = queryable.Where(conversation =>
                conversation.Tags.Any(tag => tagIds.Contains(tag.TagId)));
        }

        if (CursorCodec.TryDecode(query.Cursor, out var cursorTimestamp, out _))
        {
            queryable = queryable.Where(conversation =>
                (conversation.LastMessageAt ?? conversation.CreatedAt) < cursorTimestamp);
        }

        var limit = query.Limit is < 1 or > 100 ? 30 : query.Limit;

        var items = await queryable
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .ThenByDescending(conversation => conversation.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return (items, hasMore);
    }
}
