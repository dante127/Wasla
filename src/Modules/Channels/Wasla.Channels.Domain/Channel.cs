using Wasla.BuildingBlocks.Domain;

namespace Wasla.Channels.Domain;

/// <summary>
/// A tenant's configured account on a communication provider (e.g. a WhatsApp number,
/// a Telegram bot). Adapter wiring/verification arrives in Phases 5-6; this aggregate owns
/// identity, display metadata and lifecycle.
/// </summary>
public sealed class Channel : AggregateRoot<ChannelId>, ITenantOwned
{
    private Channel()
    {
        DisplayName = string.Empty;
    }

    private Channel(
        ChannelId id,
        TenantId tenantId,
        ChannelType type,
        string displayName,
        string? externalAccountId,
        DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        Type = type;
        DisplayName = displayName;
        ExternalAccountId = externalAccountId;
        Status = ChannelStatus.Draft;
        ConnectedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public ChannelType Type { get; private set; }

    public string DisplayName { get; private set; }

    public string? ExternalAccountId { get; private set; }

    public ChannelStatus Status { get; private set; }

    public DateTimeOffset ConnectedAt { get; private set; }

    public DateTimeOffset? LastHealthCheckAt { get; private set; }

    public static Channel Create(
        TenantId tenantId,
        ChannelType type,
        string displayName,
        string? externalAccountId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Channel display name is required.", nameof(displayName));
        }

        var channel = new Channel(
            ChannelId.New(),
            tenantId,
            type,
            displayName.Trim(),
            externalAccountId?.Trim(),
            now);

        channel.RaiseDomainEvent(new ChannelCreated(Guid.NewGuid(), now, channel.Id, tenantId, type));

        return channel;
    }

    public void Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Channel display name is required.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
    }

    public void Activate() => Status = ChannelStatus.Active;

    public void Disable() => Status = ChannelStatus.Disabled;

    public void SetExternalAccountId(string externalAccountId) => ExternalAccountId = externalAccountId.Trim();

    public void Degrade()
{
        if (Status == ChannelStatus.Active)
        {
            Status = ChannelStatus.Degraded;
        }
    }

    public void Recover()
{
        if (Status == ChannelStatus.Degraded)
        {
            Status = ChannelStatus.Active;
        }
    }

    public void Reconnect()
{
        if (Status == ChannelStatus.Disabled)
        {
            Status = ChannelStatus.Draft;
        }
    }

    public void RecordHealthCheck(DateTimeOffset now) => LastHealthCheckAt = now;
}

public sealed record ChannelCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ChannelId ChannelId,
    TenantId TenantId,
    ChannelType Type) : IDomainEvent;
