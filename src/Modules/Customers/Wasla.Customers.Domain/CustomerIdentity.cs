using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Domain;

/// <summary>
/// A channel-specific identity of a customer (e.g. a WhatsApp phone number, a Telegram
/// chat id, an email address). Uniqueness is enforced per tenant + channel + external id.
/// </summary>
public sealed class CustomerIdentity : Entity<CustomerIdentityId>, ITenantOwned
{
    private CustomerIdentity()
    {
        ExternalId = string.Empty;
        DisplayValue = string.Empty;
    }

    internal CustomerIdentity(
        CustomerIdentityId id,
        TenantId tenantId,
        CustomerId customerId,
        ChannelType channelType,
        string externalId,
        string displayValue,
        DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        ChannelType = channelType;
        ExternalId = externalId;
        DisplayValue = displayValue;
        FirstSeenAt = now;
        LastSeenAt = now;
    }

    public TenantId TenantId { get; private set; }

    public CustomerId CustomerId { get; private set; }

    public ChannelType ChannelType { get; private set; }

    public string ExternalId { get; private set; }

    public string DisplayValue { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public void Touch(DateTimeOffset now) => LastSeenAt = now;
}
