using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Domain;

public sealed class Customer : AggregateRoot<CustomerId>, ITenantOwned
{
    private Customer()
    {
        DisplayName = string.Empty;
    }

    private Customer(CustomerId id, TenantId tenantId, string displayName, DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        DisplayName = displayName;
        Status = CustomerStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public string DisplayName { get; private set; }

    public CustomerStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public List<CustomerIdentity> Identities { get; private set; } = [];

    public List<CustomerContact> Contacts { get; private set; } = [];

    public List<CustomerNote> Notes { get; private set; } = [];

    public List<CustomerTag> Tags { get; private set; } = [];

    public List<CustomerActivity> Activities { get; private set; } = [];

    public static Customer Create(TenantId tenantId, string displayName, DateTimeOffset now)
    {
        var customer = new Customer(CustomerId.New(), tenantId, NormalizeDisplayName(displayName), now);
        customer.RaiseDomainEvent(new CustomerCreated(Guid.NewGuid(), now, customer.Id, tenantId));
        customer.RecordActivity("customer.created", null, null, now);

        return customer;
    }

    public void Rename(string displayName, DateTimeOffset now)
    {
        DisplayName = NormalizeDisplayName(displayName);
        UpdatedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status == CustomerStatus.Archived)
        {
            return;
        }

        Status = CustomerStatus.Archived;
        UpdatedAt = now;
        RaiseDomainEvent(new CustomerArchived(Guid.NewGuid(), now, Id, TenantId));
        RecordActivity("customer.archived", null, null, now);
    }

    public CustomerIdentity AddIdentity(
        ChannelType channelType,
        string externalId,
        string? displayValue,
        DateTimeOffset now)
    {
        var normalizedExternalId = (externalId ?? string.Empty).Trim();

        if (normalizedExternalId.Length == 0)
        {
            throw new ArgumentException("Identity external id is required.", nameof(externalId));
        }

        var duplicate = Identities.Any(identity =>
            identity.ChannelType == channelType
            && string.Equals(identity.ExternalId, normalizedExternalId, StringComparison.Ordinal));

        if (duplicate)
        {
            throw new InvalidOperationException("This identity is already linked to the customer.");
        }

        var identity = new CustomerIdentity(
            CustomerIdentityId.New(),
            TenantId,
            Id,
            channelType,
            normalizedExternalId,
            string.IsNullOrWhiteSpace(displayValue) ? normalizedExternalId : displayValue.Trim(),
            now);

        Identities.Add(identity);
        UpdatedAt = now;
        RaiseDomainEvent(new CustomerIdentityLinked(Guid.NewGuid(), now, Id, TenantId, channelType, normalizedExternalId));
        RecordActivity("identity.linked", $"{channelType}:{normalizedExternalId}", null, now);

        return identity;
    }

    public CustomerContact AddContact(ContactType type, string value, DateTimeOffset now)
    {
        var normalizedValue = NormalizeContactValue(type, value);

        if (Contacts.Any(contact =>
            contact.Type == type
            && string.Equals(contact.Value, normalizedValue, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("This contact already exists on the customer.");
        }

        var contact = new CustomerContact(CustomerContactId.New(), TenantId, Id, type, normalizedValue, now);
        Contacts.Add(contact);
        UpdatedAt = now;
        RaiseDomainEvent(new CustomerContactAdded(Guid.NewGuid(), now, Id, TenantId, type));

        return contact;
    }

    public CustomerNote AddNote(UserId? authorUserId, string body, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Note body is required.", nameof(body));
        }

        var note = new CustomerNote(CustomerNoteId.New(), Id, authorUserId, body.Trim(), now);
        Notes.Add(note);
        UpdatedAt = now;
        RaiseDomainEvent(new CustomerNoteAdded(Guid.NewGuid(), now, Id, TenantId));
        RecordActivity("note.added", null, authorUserId, now);

        return note;
    }

    public void AddTag(Guid tagId, DateTimeOffset now)
    {
        if (Tags.Any(tag => tag.TagId == tagId))
        {
            return;
        }

        Tags.Add(new CustomerTag(Id, tagId));
        UpdatedAt = now;
        RaiseDomainEvent(new CustomerTagAdded(Guid.NewGuid(), now, Id, TenantId, tagId));
        RecordActivity("tag.added", tagId.ToString(), null, now);
    }

    public bool RemoveTag(Guid tagId, DateTimeOffset now)
    {
        var removed = Tags.RemoveAll(tag => tag.TagId == tagId) > 0;

        if (removed)
        {
            UpdatedAt = now;
            RaiseDomainEvent(new CustomerTagRemoved(Guid.NewGuid(), now, Id, TenantId, tagId));
            RecordActivity("tag.removed", tagId.ToString(), null, now);
        }

        return removed;
    }

    public void TouchIdentity(ChannelType channelType, string externalId, DateTimeOffset now)
    {
        var identity = Identities.FirstOrDefault(item =>
            item.ChannelType == channelType
            && string.Equals(item.ExternalId, externalId.Trim(), StringComparison.Ordinal));

        identity?.Touch(now);
        UpdatedAt = now;
    }

    public void RecordActivity(string type, string? data, UserId? actorUserId, DateTimeOffset now)
    {
        Activities.Add(new CustomerActivity(CustomerActivityId.New(), Id, type, data, actorUserId, now));
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        return displayName.Trim();
    }

    private static string NormalizeContactValue(ContactType type, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Contact value is required.", nameof(value));
        }

        var trimmed = value.Trim();

        return type == ContactType.Email ? trimmed.ToLowerInvariant() : trimmed;
    }
}
