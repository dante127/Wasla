using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Domain;

/// <summary>An agent note attached to a customer profile.</summary>
public sealed class CustomerNote : Entity<CustomerNoteId>
{
    private CustomerNote()
    {
        Body = string.Empty;
    }

    internal CustomerNote(
        CustomerNoteId id,
        CustomerId customerId,
        UserId? authorUserId,
        string body,
        DateTimeOffset now)
        : base(id)
    {
        CustomerId = customerId;
        AuthorUserId = authorUserId;
        Body = body;
        CreatedAt = now;
    }

    public CustomerId CustomerId { get; private set; }

    public UserId? AuthorUserId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>A link assigning a catalog tag to a customer.</summary>
public sealed class CustomerTag
{
    private CustomerTag()
    {
    }

    internal CustomerTag(CustomerId customerId, Guid tagId)
    {
        CustomerId = customerId;
        TagId = tagId;
    }

    public CustomerId CustomerId { get; private set; }

    public Guid TagId { get; private set; }
}
