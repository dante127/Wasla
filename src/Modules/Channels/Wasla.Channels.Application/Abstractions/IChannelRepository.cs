using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Application.Abstractions;

public interface IChannelRepository
{
    Task AddAsync(Channel channel, CancellationToken cancellationToken);

    Task<Channel?> GetByIdAsync(TenantId tenantId, ChannelId channelId, CancellationToken cancellationToken);

    Task<List<Channel>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>Server-side resolution for webhooks (channel id is the opaque secret); no tenant filter.</summary>
    Task<Channel?> GetByIdForSystemAsync(ChannelId channelId, CancellationToken cancellationToken);
}
