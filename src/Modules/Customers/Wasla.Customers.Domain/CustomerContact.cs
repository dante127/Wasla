using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Domain;

/// <summary>A typed, normalized contact point of a customer (phone, email, other).</summary>
public sealed class CustomerContact : Entity<CustomerContactId>, ITenantOwned
{
    private CustomerContact()
    {
        Value = string.Empty;
    }

    internal CustomerContact(
        CustomerContactId id,
        TenantId tenantId,
        CustomerId customerId,
        ContactType type,
        string value,
        DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Type = type;
        Value = value;
        CreatedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public CustomerId CustomerId { get; private set; }

    public ContactType Type { get; private set; }

    public string Value { get; private set; }

    public bool IsVerified { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkVerified() => IsVerified = true;
}
