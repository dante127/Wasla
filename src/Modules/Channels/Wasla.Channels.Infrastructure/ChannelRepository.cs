using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure;

public sealed class ChannelRepository(ChannelsDbContext dbContext) : IChannelRepository
{
    public async Task AddAsync(Channel channel, CancellationToken cancellationToken) =>
        await dbContext.Channels.AddAsync(channel, cancellationToken);

    public Task<Channel?> GetByIdAsync(TenantId tenantId, ChannelId channelId, CancellationToken cancellationToken) =>
        dbContext.Channels.FirstOrDefaultAsync(
            channel => channel.TenantId == tenantId && channel.Id == channelId,
            cancellationToken);

    public Task<List<Channel>> ListAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Channels
            .Where(channel => channel.TenantId == tenantId)
            .OrderBy(channel => channel.DisplayName)
            .ToListAsync(cancellationToken);
}
