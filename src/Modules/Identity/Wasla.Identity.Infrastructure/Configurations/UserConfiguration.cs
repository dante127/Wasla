using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "identity");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasConversion(id => id.Value, value => new UserId(value));

        builder.Property(user => user.Email)
            .HasConversion(email => email.Value, value => EmailAddress.FromTrusted(value))
            .IsRequired()
            .HasMaxLength(320);
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(user => user.PasswordHash).HasMaxLength(500);
        builder.Property(user => user.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.LastLoginAt);
    }
}
