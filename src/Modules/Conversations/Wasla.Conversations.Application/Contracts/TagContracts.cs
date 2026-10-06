using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Application.Contracts;

/// <summary>Public, provider-neutral view of a catalog tag for other modules.</summary>
public sealed record TagInfo(Guid Id, string Key, string Name, string? Color);

/// <summary>Module contract: read tag catalog data without touching Conversations internals.</summary>
public interface ITagInfoProvider
{
    Task<TagInfo?> GetAsync(TenantId tenantId, Guid tagId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, TagInfo>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> tagIds,
        CancellationToken cancellationToken);

    Task<bool> AllExistAsync(TenantId tenantId, IReadOnlyCollection<Guid> tagIds, CancellationToken cancellationToken);
}
