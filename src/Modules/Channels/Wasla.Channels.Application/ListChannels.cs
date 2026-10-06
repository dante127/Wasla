using Wasla.BuildingBlocks.Application;
using Wasla.Channels.Application.Abstractions;

namespace Wasla.Channels.Application;

/// <summary>Lists the channels of the current tenant.</summary>
public sealed class ListChannelsHandler(
    ITenantContext tenantContext,
    IChannelRepository channels)
{
    public async Task<Result<IReadOnlyList<ChannelListItem>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<ChannelListItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var channelList = await channels.ListAsync(tenantId, cancellationToken);

        var items = channelList
            .OrderBy(channel => channel.DisplayName, StringComparer.Ordinal)
            .Select(channel => new ChannelListItem(
                channel.Id.Value,
                channel.Type.ToString(),
                channel.DisplayName,
                channel.Status.ToString()))
            .ToList();

        return Result.Success<IReadOnlyList<ChannelListItem>>(items);
    }
}
