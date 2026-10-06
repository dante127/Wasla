using Wasla.BuildingBlocks.Domain;

namespace Wasla.Channels.Domain;

/// <summary>
/// A provider credential for a channel, stored encrypted at rest (IStringEncryptor).
/// Values are never logged; rotations overwrite in place.
/// </summary>
public sealed class ChannelCredential : ITenantOwned
{
    private ChannelCredential()
    {
        Key = string.Empty;
        ProtectedValue = string.Empty;
    }

    private ChannelCredential(TenantId tenantId, ChannelId channelId, string key, string protectedValue, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ChannelId = channelId;
        Key = key;
        ProtectedValue = protectedValue;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public ChannelId ChannelId { get; private set; }

    public string Key { get; private set; }

    public string ProtectedValue { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ChannelCredential Create(
        TenantId tenantId,
        ChannelId channelId,
        string key,
        string protectedValue,
        DateTimeOffset now) =>
        new(tenantId, channelId, key, protectedValue, now);

    public void Rotate(string protectedValue, DateTimeOffset now)
    {
        ProtectedValue = protectedValue;
        UpdatedAt = now;
    }
}
