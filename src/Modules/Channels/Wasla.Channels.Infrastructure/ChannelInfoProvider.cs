using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure;

public sealed class ChannelInfoProvider(IChannelRepository channels) : IChannelInfoProvider
{
    public async Task<ChannelInfo?> GetAsync(TenantId tenantId, Guid channelId, CancellationToken cancellationToken)
    {
        var channel = await channels.GetByIdAsync(tenantId, new ChannelId(channelId), cancellationToken);

        return channel is null ? null : ToInfo(channel);
    }

    public async Task<IReadOnlyDictionary<Guid, ChannelInfo>> GetManyAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> channelIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = channelIds.Distinct().ToList();

        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, ChannelInfo>();
        }

        var all = await channels.ListAsync(tenantId, cancellationToken);

        return all
            .Where(channel => distinctIds.Contains(channel.Id.Value))
            .ToDictionary(channel => channel.Id.Value, ToInfo);
    }

    private static ChannelInfo ToInfo(Channel channel) =>
        new(channel.Id.Value, channel.Type.ToString(), channel.DisplayName, channel.Status.ToString());
}
