using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application.Abstractions;

public interface IQuickReplyRepository
{
    Task AddAsync(QuickReply quickReply, CancellationToken cancellationToken);

    Task<QuickReply?> GetByIdAsync(TenantId tenantId, QuickReplyId quickReplyId, CancellationToken cancellationToken);

    Task<List<QuickReply>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken cancellationToken);
}
