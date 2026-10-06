using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Application;

/// <summary>
/// Disconnect flow: drop credential material and disable the channel. Historical
/// conversations and messages stay intact (docs/channels.md §3).
/// </summary>
public sealed class DisconnectChannelHandler(
    ITenantContext tenantContext,
    IChannelRepository channels,
    IChannelCredentialStore credentials,
    IChannelsUnitOfWork unitOfWork)
{
    private static readonly string[] CredentialKeys =
    [
        ChannelCredentialKeys.AccessToken,
        ChannelCredentialKeys.AppSecret,
        ChannelCredentialKeys.VerifyToken,
        ChannelCredentialKeys.PhoneNumberId,
    ];

    public async Task<Result<ChannelListItem>> HandleAsync(Guid channelId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ChannelListItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var channel = await channels.GetByIdAsync(tenantId, new ChannelId(channelId), cancellationToken);

        if (channel is null)
        {
            return Result.Failure<ChannelListItem>(new Error("channels.not_found", "Channel not found."));
        }

        foreach (var key in CredentialKeys)
        {
            await credentials.RemoveAsync(tenantId, channelId, key, cancellationToken);
        }

        channel.Disable();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ChannelListItem(
            channel.Id.Value,
            channel.Type.ToString(),
            channel.DisplayName,
            channel.Status.ToString()));
    }
}
