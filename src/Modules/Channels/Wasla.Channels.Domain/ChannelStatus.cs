namespace Wasla.Channels.Domain;

/// <summary>Lifecycle status of a tenant's channel connection.</summary>
public enum ChannelStatus
{
    Draft = 0,
    Active = 1,
    Degraded = 2,
    Disabled = 3,
}
