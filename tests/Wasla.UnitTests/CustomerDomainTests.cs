using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;
using Wasla.Customers.Domain;

namespace Wasla.UnitTests;

public sealed class CustomerDomainTests
{
    [Fact]
    public void Create_sets_fields_records_activity_and_raises_event()
    {
        var now = DateTimeOffset.UtcNow;
        var customer = Customer.Create(TenantId.New(), "  Alice  ", now);

        Assert.Equal("Alice", customer.DisplayName);
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Single(customer.DomainEvents);
        Assert.IsType<CustomerCreated>(customer.DomainEvents.First());
        Assert.Single(customer.Activities);
        Assert.Equal("customer.created", customer.Activities[0].Type);
    }

    [Fact]
    public void Rename_trims_and_updates_timestamp()
    {
        var customer = Customer.Create(TenantId.New(), "Alice", DateTimeOffset.UtcNow);
        var later = DateTimeOffset.UtcNow.AddMinutes(5);

        customer.Rename("  Alice Johnson ", later);

        Assert.Equal("Alice Johnson", customer.DisplayName);
        Assert.Equal(later, customer.UpdatedAt);
    }

    [Fact]
    public void AddIdentity_rejects_duplicates_on_the_same_customer()
    {
        var customer = Customer.Create(TenantId.New(), "Alice", DateTimeOffset.UtcNow);
        customer.AddIdentity(ChannelType.WhatsApp, "+963111", "+963111", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            customer.AddIdentity(ChannelType.WhatsApp, "+963111", "+963111", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AddContact_deduplicates_normalized_values()
    {
        var customer = Customer.Create(TenantId.New(), "Alice", DateTimeOffset.UtcNow);
        customer.AddContact(ContactType.Email, "Alice@Example.COM", DateTimeOffset.UtcNow);

        Assert.Equal("alice@example.com", customer.Contacts[0].Value);
        Assert.Throws<InvalidOperationException>(() =>
            customer.AddContact(ContactType.Email, "alice@example.com", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AddTag_is_idempotent_and_remove_reports_missing_tag()
    {
        var customer = Customer.Create(TenantId.New(), "Alice", DateTimeOffset.UtcNow);
        var tagId = Guid.NewGuid();

        customer.AddTag(tagId, DateTimeOffset.UtcNow);
        customer.AddTag(tagId, DateTimeOffset.UtcNow);
        Assert.Single(customer.Tags);

        Assert.True(customer.RemoveTag(tagId, DateTimeOffset.UtcNow));
        Assert.False(customer.RemoveTag(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Archive_sets_status_and_raises_event()
    {
        var customer = Customer.Create(TenantId.New(), "Alice", DateTimeOffset.UtcNow);
        customer.ClearDomainEvents();

        customer.Archive(DateTimeOffset.UtcNow);

        Assert.Equal(CustomerStatus.Archived, customer.Status);
        Assert.Single(customer.DomainEvents);
        Assert.IsType<CustomerArchived>(customer.DomainEvents.First());
    }

    [Fact]
    public void AddNote_requires_body()
    {
        var customer = Customer.Create(TenantId.New(), "Alice", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => customer.AddNote(null, "   ", DateTimeOffset.UtcNow));
    }
}

public sealed class TagDomainTests
{
    [Fact]
    public void Create_normalizes_key_and_raises_event()
    {
        var tag = Tag.Create(TenantId.New(), "  VIP  ", "VIP", "#7c3aed", DateTimeOffset.UtcNow);

        Assert.Equal("vip", tag.Key);
        Assert.Equal("VIP", tag.Name);
        Assert.Single(tag.DomainEvents);
        Assert.IsType<TagCreated>(tag.DomainEvents.First());
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("a")]
    [InlineData("with space")]
    [InlineData("-lead")]
    public void Create_rejects_invalid_keys(string key)
    {
        Assert.Throws<ArgumentException>(() => Tag.Create(TenantId.New(), key, "Name", null, DateTimeOffset.UtcNow));
    }
}
