using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Contracts;

namespace Wasla.Conversations.Infrastructure;

/// <summary>Module contract implementation: conversation facts for the analytics module.</summary>
public sealed class ConversationAnalyticsSource(ConversationsDbContext dbContext) : IConversationAnalyticsSource
{
    public async Task<IReadOnlyList<ConversationAnalyticsRecord>> ListAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        // Project to primitives server-side, then map in memory (enum-to-string
        // conversion stays out of the SQL translation path).
        var rows = await dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.TenantId == tenantId)
            .Select(conversation => new
            {
                ConversationId = conversation.Id.Value,
                conversation.ChannelId,
                conversation.CustomerId,
                conversation.AssignedUserId,
                conversation.AssignedTeamId,
                conversation.Status,
                conversation.CreatedAt,
                conversation.LastMessageAt,
                conversation.FirstResponseAt,
                conversation.ResolvedAt,
                conversation.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ConversationAnalyticsRecord(
                row.ConversationId,
                row.ChannelId,
                row.CustomerId,
                row.AssignedUserId,
                row.AssignedTeamId,
                row.Status.ToString(),
                row.CreatedAt,
                row.LastMessageAt,
                row.FirstResponseAt,
                row.ResolvedAt,
                row.UpdatedAt))
            .ToList();
    }
}
