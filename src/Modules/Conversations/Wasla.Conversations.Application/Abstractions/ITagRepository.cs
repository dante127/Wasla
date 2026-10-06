using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application.Abstractions;

public interface ITagRepository
{
    Task AddAsync(Tag tag, CancellationToken cancellationToken);

    Task<Tag?> GetByIdAsync(TenantId tenantId, TagId tagId, CancellationToken cancellationToken);

    Task<List<Tag>> GetByIdsAsync(TenantId tenantId, IReadOnlyCollection<TagId> tagIds, CancellationToken cancellationToken);

    Task<List<Tag>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken cancellationToken);

    Task<bool> AllExistAsync(TenantId tenantId, IReadOnlyCollection<TagId> tagIds, CancellationToken cancellationToken);
}
