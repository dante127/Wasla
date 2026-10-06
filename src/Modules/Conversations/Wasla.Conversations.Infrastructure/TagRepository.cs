using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure;

public sealed class TagRepository(ConversationsDbContext dbContext) : ITagRepository
{
    public async Task AddAsync(Tag tag, CancellationToken cancellationToken) =>
        await dbContext.Tags.AddAsync(tag, cancellationToken);

    public Task<Tag?> GetByIdAsync(TenantId tenantId, TagId tagId, CancellationToken cancellationToken) =>
        dbContext.Tags.FirstOrDefaultAsync(
            tag => tag.TenantId == tenantId && tag.Id == tagId,
            cancellationToken);

    public async Task<List<Tag>> GetByIdsAsync(
        TenantId tenantId,
        IReadOnlyCollection<TagId> tagIds,
        CancellationToken cancellationToken)
    {
        var ids = tagIds.Distinct().ToList();

        return await dbContext.Tags
            .Where(tag => tag.TenantId == tenantId && ids.Contains(tag.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Tag>> ListAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Tags
            .Where(tag => tag.TenantId == tenantId)
            .OrderBy(tag => tag.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken cancellationToken) =>
        dbContext.Tags.AnyAsync(
            tag => tag.TenantId == tenantId && tag.Key == key,
            cancellationToken);

    public async Task<bool> AllExistAsync(
        TenantId tenantId,
        IReadOnlyCollection<TagId> tagIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = tagIds.Distinct().ToList();

        if (distinctIds.Count == 0)
        {
            return true;
        }

        var count = await dbContext.Tags
            .Where(tag => tag.TenantId == tenantId && distinctIds.Contains(tag.Id))
            .CountAsync(cancellationToken);

        return count == distinctIds.Count;
    }
}
