using Wasla.BuildingBlocks.Domain;

namespace Wasla.Channels.Application.Contracts;

/// <summary>Public, provider-neutral view of a channel for other modules.</summary>
public sealed record ChannelInfo(Guid Id, string Type, string DisplayName, string Status);

/// <summary>Module contract: read channel information without touching Channels internals.</summary>
public interface IChannelInfoProvider
{
    Task<ChannelInfo?> GetAsync(TenantId tenantId, Guid channelId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ChannelInfo>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> channelIds,
        CancellationToken cancellationToken);
}
