using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Application.Contracts;

namespace Wasla.Messages.Infrastructure;

/// <summary>Module contract implementation: message aggregates for analytics.</summary>
public sealed class MessageAnalyticsSource(MessagesDbContext dbContext) : IMessageAnalyticsSource
{
    public async Task<IReadOnlyDictionary<Guid, ConversationMessageCounts>> GetConversationCountsAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var ids = conversationIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ConversationMessageCounts>();
        }

        var rows = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.TenantId == tenantId && ids.Contains(message.ConversationId))
            .GroupBy(message => message.ConversationId)
            .Select(group => new
            {
                ConversationId = group.Key,
                Inbound = group.Count(message => message.Direction == MessageDirection.Inbound),
                Outbound = group.Count(message => message.Direction == MessageDirection.Outbound),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.ConversationId,
            row => new ConversationMessageCounts(row.ConversationId, row.Inbound, row.Outbound));
    }

    public async Task<IReadOnlyList<DailyMessageCount>> GetDailyCountsAsync(
        TenantId tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        // Two-column projection keeps volumes bounded for the MVP; daily grouping
        // happens in memory to avoid provider-specific date translations.
        var rows = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.TenantId == tenantId
                && message.CreatedAt >= from
                && message.CreatedAt <= to)
            .Select(message => new { message.CreatedAt, message.Direction })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => DateOnly.FromDateTime(row.CreatedAt.UtcDateTime))
            .Select(group => new DailyMessageCount(
                group.Key,
                group.Count(row => row.Direction == MessageDirection.Inbound),
                group.Count(row => row.Direction == MessageDirection.Outbound)))
            .OrderBy(day => day.Date)
            .ToList();
    }
}
