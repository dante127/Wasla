using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Infrastructure.Configurations;

public sealed class CustomerIdentityConfiguration : IEntityTypeConfiguration<CustomerIdentity>
{
    public void Configure(EntityTypeBuilder<CustomerIdentity> builder)
    {
        builder.ToTable("CustomerIdentities", "customers");

        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Id)
            .HasConversion(id => id.Value, value => new CustomerIdentityId(value));
        builder.Property(identity => identity.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(identity => identity.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));
        builder.Property(identity => identity.ChannelType).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(identity => identity.ExternalId).IsRequired().HasMaxLength(320);
        builder.Property(identity => identity.DisplayValue).IsRequired().HasMaxLength(400);
        builder.Property(identity => identity.FirstSeenAt).IsRequired();
        builder.Property(identity => identity.LastSeenAt).IsRequired();

        builder.HasIndex(identity => new { identity.TenantId, identity.ChannelType, identity.ExternalId }).IsUnique();
    }
}

public sealed class CustomerContactConfiguration : IEntityTypeConfiguration<CustomerContact>
{
    public void Configure(EntityTypeBuilder<CustomerContact> builder)
    {
        builder.ToTable("CustomerContacts", "customers");

        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id)
            .HasConversion(id => id.Value, value => new CustomerContactId(value));
        builder.Property(contact => contact.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(contact => contact.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));
        builder.Property(contact => contact.Type).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(contact => contact.Value).IsRequired().HasMaxLength(400);
        builder.Property(contact => contact.IsVerified).IsRequired();
        builder.Property(contact => contact.CreatedAt).IsRequired();

        builder.HasIndex(contact => new { contact.TenantId, contact.Type, contact.Value }).IsUnique();
    }
}

public sealed class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> builder)
    {
        builder.ToTable("CustomerNotes", "customers");

        builder.HasKey(note => note.Id);
        builder.Property(note => note.Id)
            .HasConversion(id => id.Value, value => new CustomerNoteId(value));
        builder.Property(note => note.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));
        builder.Property(note => note.AuthorUserId)
            .HasConversion(
                userId => userId.HasValue ? userId.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : (UserId?)null);
        builder.Property(note => note.Body).IsRequired().HasMaxLength(4000);
        builder.Property(note => note.CreatedAt).IsRequired();
    }
}

public sealed class CustomerActivityConfiguration : IEntityTypeConfiguration<CustomerActivity>
{
    public void Configure(EntityTypeBuilder<CustomerActivity> builder)
    {
        builder.ToTable("CustomerActivities", "customers");

        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Id)
            .HasConversion(id => id.Value, value => new CustomerActivityId(value));
        builder.Property(activity => activity.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));
        builder.Property(activity => activity.Type).IsRequired().HasMaxLength(100);
        builder.Property(activity => activity.Data).HasMaxLength(2000);
        builder.Property(activity => activity.ActorUserId)
            .HasConversion(
                userId => userId.HasValue ? userId.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : (UserId?)null);
        builder.Property(activity => activity.OccurredAt).IsRequired();

        builder.HasIndex(activity => new { activity.CustomerId, activity.OccurredAt });
    }
}

public sealed class CustomerTagConfiguration : IEntityTypeConfiguration<CustomerTag>
{
    public void Configure(EntityTypeBuilder<CustomerTag> builder)
    {
        builder.ToTable("CustomerTags", "customers");

        builder.HasKey(tag => new { tag.CustomerId, tag.TagId });
        builder.Property(tag => tag.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));

        builder.HasIndex(tag => tag.TagId);
    }
}
