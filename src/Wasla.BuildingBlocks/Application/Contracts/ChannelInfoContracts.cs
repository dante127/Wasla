using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Application.Contracts;

/// <summary>Public, provider-neutral view of a channel for other modules.</summary>
public sealed record ChannelInfo(Guid Id, string Type, string DisplayName, string Status);

/// <summary>
/// Shared module contract: read channel information without touching Channels internals.
/// Hoisted to BuildingBlocks in Phase 5 (see ADR-0011) so Conversations can render channel
/// references without referencing the Channels module directly.
/// </summary>
public interface IChannelInfoProvider
{
    Task<ChannelInfo?> GetAsync(TenantId tenantId, Guid channelId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ChannelInfo>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> channelIds,
        CancellationToken cancellationToken);
}
