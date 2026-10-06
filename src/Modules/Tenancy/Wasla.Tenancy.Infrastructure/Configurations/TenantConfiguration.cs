using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Tenancy.Domain;

namespace Wasla.Tenancy.Infrastructure.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", "tenancy");

        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(tenant => tenant.TenantId).HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(tenant => tenant.Name).IsRequired().HasMaxLength(200);

        builder.Property(tenant => tenant.Slug)
            .HasConversion(slug => slug.Value, value => TenantSlug.FromTrusted(value))
            .IsRequired()
            .HasMaxLength(64);
        builder.HasIndex(tenant => tenant.Slug).IsUnique();

        builder.Property(tenant => tenant.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(tenant => tenant.DefaultCulture).IsRequired().HasMaxLength(10);
        builder.Property(tenant => tenant.TimeZone).IsRequired().HasMaxLength(64);
        builder.Property(tenant => tenant.CreatedAt).IsRequired();
    }
}
