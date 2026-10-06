using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Customers.Domain;

namespace Wasla.Customers.Infrastructure.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "customers");

        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Id).HasConversion(id => id.Value, value => new CustomerId(value));
        builder.Property(customer => customer.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(customer => customer.DisplayName).IsRequired().HasMaxLength(400);
        builder.Property(customer => customer.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(customer => customer.CreatedAt).IsRequired();
        builder.Property(customer => customer.UpdatedAt).IsRequired();

        builder.HasIndex(customer => customer.DisplayName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        builder.HasIndex(customer => new { customer.TenantId, customer.Status });

        builder.HasMany(customer => customer.Identities)
            .WithOne()
            .HasForeignKey(identity => identity.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(customer => customer.Contacts)
            .WithOne()
            .HasForeignKey(contact => contact.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(customer => customer.Notes)
            .WithOne()
            .HasForeignKey(note => note.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(customer => customer.Tags)
            .WithOne()
            .HasForeignKey(tag => tag.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(customer => customer.Activities)
            .WithOne()
            .HasForeignKey(activity => activity.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
