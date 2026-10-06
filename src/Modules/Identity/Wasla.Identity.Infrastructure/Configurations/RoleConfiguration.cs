using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");

        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasConversion(id => id.Value, value => new RoleId(value));
        builder.Property(role => role.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(role => role.Name).IsRequired().HasMaxLength(100);
        builder.Property(role => role.IsSystem).IsRequired();
        builder.Property(role => role.Permissions).HasColumnType("text[]");

        builder.HasIndex(role => new { role.TenantId, role.Name }).IsUnique();
    }
}
