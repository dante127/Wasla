using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure;

public sealed class TagInfoProvider(ITagRepository tags) : ITagInfoProvider
{
    public async Task<TagInfo?> GetAsync(TenantId tenantId, Guid tagId, CancellationToken cancellationToken)
    {
        var tag = await tags.GetByIdAsync(tenantId, new TagId(tagId), cancellationToken);

        return tag is null ? null : ToInfo(tag);
    }

    public async Task<IReadOnlyDictionary<Guid, TagInfo>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = tagIds.Distinct().Select(id => new TagId(id)).ToList();

        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, TagInfo>();
        }

        var found = await tags.GetByIdsAsync(tenantId, distinctIds, cancellationToken);

        return found.ToDictionary(tag => tag.Id.Value, ToInfo);
    }

    public async Task<bool> AllExistAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = tagIds.Distinct().Select(id => new TagId(id)).ToList();

        return await tags.AllExistAsync(tenantId, distinctIds, cancellationToken);
    }

    private static TagInfo ToInfo(Tag tag) => new(tag.Id.Value, tag.Key, tag.Name, tag.Color);
}
