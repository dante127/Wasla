using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure;

/// <summary>Module contract implementation: routing facts for outbound dispatch.</summary>
public sealed class ChannelRoutingProvider(IChannelRepository channels) : IChannelRoutingProvider
{
    public async Task<ChannelRoutingInfo?> GetRoutingAsync(
        TenantId tenantId,
        Guid channelId,
        CancellationToken cancellationToken)
    {
        var channel = await channels.GetByIdAsync(tenantId, new ChannelId(channelId), cancellationToken);

        return channel is null
            ? null
            : new ChannelRoutingInfo(
                channel.Type,
                channel.DisplayName,
                channel.Status.ToString(),
                channel.ExternalAccountId);
    }
}
