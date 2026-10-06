using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Domain;

/// <summary>An append-only activity feed entry for a customer (the Customer 360 timeline).</summary>
public sealed class CustomerActivity : Entity<CustomerActivityId>
{
    private CustomerActivity()
    {
        Type = string.Empty;
    }

    internal CustomerActivity(
        CustomerActivityId id,
        CustomerId customerId,
        string type,
        string? data,
        UserId? actorUserId,
        DateTimeOffset occurredAt)
        : base(id)
    {
        CustomerId = customerId;
        Type = type;
        Data = data;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
    }

    public CustomerId CustomerId { get; private set; }

    public string Type { get; private set; }

    public string? Data { get; private set; }

    public UserId? ActorUserId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
