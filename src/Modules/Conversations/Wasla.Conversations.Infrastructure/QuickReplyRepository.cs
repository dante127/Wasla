using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure;

public sealed class QuickReplyRepository(ConversationsDbContext dbContext) : IQuickReplyRepository
{
    public async Task AddAsync(QuickReply quickReply, CancellationToken cancellationToken) =>
        await dbContext.QuickReplies.AddAsync(quickReply, cancellationToken);

    public Task<QuickReply?> GetByIdAsync(
        TenantId tenantId,
        QuickReplyId quickReplyId,
        CancellationToken cancellationToken) =>
        dbContext.QuickReplies.FirstOrDefaultAsync(
            quickReply => quickReply.TenantId == tenantId && quickReply.Id == quickReplyId,
            cancellationToken);

    public Task<List<QuickReply>> ListAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.QuickReplies
            .Where(quickReply => quickReply.TenantId == tenantId)
            .OrderBy(quickReply => quickReply.Key)
            .ToListAsync(cancellationToken);

    public Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken cancellationToken) =>
        dbContext.QuickReplies.AnyAsync(
            quickReply => quickReply.TenantId == tenantId && quickReply.Key == key,
            cancellationToken);
}
