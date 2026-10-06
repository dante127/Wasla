using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Application;

/// <summary>
/// Creates a channel in Draft status. Provider verification/connection flows arrive with
/// the channel adapters (Phases 5-6).
/// </summary>
public sealed class CreateChannelHandler(
    ITenantContext tenantContext,
    IChannelRepository channels,
    IChannelsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<ChannelListItem>> HandleAsync(
        CreateChannelRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ChannelListItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if (!Enum.TryParse<ChannelType>(request.Type, ignoreCase: true, out var channelType))
        {
            return Result.Failure<ChannelListItem>(
                new Error("channels.invalid_type", $"Unknown channel type '{request.Type}'."));
        }

        Channel channel;

        try
        {
            channel = Channel.Create(tenantId, channelType, request.DisplayName, null, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<ChannelListItem>(new Error("channels.invalid", exception.Message));
        }

        await channels.AddAsync(channel, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ChannelListItem(
            channel.Id.Value,
            channel.Type.ToString(),
            channel.DisplayName,
            channel.Status.ToString()));
    }
}
