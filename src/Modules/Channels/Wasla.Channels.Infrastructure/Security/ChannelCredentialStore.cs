using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure.Security;

/// <summary>
/// Encrypted credential storage backed by the Channels module database. Values are
/// protected with <see cref="IStringEncryptor"/> and are never exposed through APIs.
/// </summary>
public sealed class ChannelCredentialStore(
    ChannelsDbContext dbContext,
    IStringEncryptor encryptor,
    IClock clock) : IChannelCredentialStore
{
    public async Task<string?> GetAsync(
        TenantId tenantId,
        Guid channelId,
        string key,
        CancellationToken cancellationToken)
    {
        var channel = new ChannelId(channelId);

        var credential = await dbContext.ChannelCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenantId && entity.ChannelId == channel && entity.Key == key,
                cancellationToken);

        if (credential is null)
        {
            return null;
        }

        try
        {
            return encryptor.Unprotect(credential.ProtectedValue);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            // Undecryptable value (e.g. key rotated): behave as unconfigured.
            return null;
        }
    }

    public async Task SetAsync(
        TenantId tenantId,
        Guid channelId,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        var channel = new ChannelId(channelId);
        var protectedValue = encryptor.Protect(value);

        var existing = await dbContext.ChannelCredentials
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenantId && entity.ChannelId == channel && entity.Key == key,
                cancellationToken);

        if (existing is null)
        {
            await dbContext.ChannelCredentials.AddAsync(
                ChannelCredential.Create(tenantId, channel, key, protectedValue, clock.UtcNow),
                cancellationToken);
        }
        else
        {
            existing.Rotate(protectedValue, clock.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        TenantId tenantId,
        Guid channelId,
        string key,
        CancellationToken cancellationToken)
    {
        var channel = new ChannelId(channelId);

        var existing = await dbContext.ChannelCredentials
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenantId && entity.ChannelId == channel && entity.Key == key,
                cancellationToken);

        if (existing is null)
        {
            return;
        }

        dbContext.ChannelCredentials.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
