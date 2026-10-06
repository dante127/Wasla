using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Tenancy.Application.Abstractions;
using Wasla.Tenancy.Domain;

namespace Wasla.Tenancy.Application;

public sealed record CreateTenantCommand(
    string Name,
    string Slug,
    string DefaultCulture,
    string TimeZone);

/// <summary>Creates a new tenant. Reserved for operator/seed flows (no public signup in MVP).</summary>
public sealed class CreateTenantHandler(
    ITenantRepository tenants,
    ITenancyUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<TenantId>> HandleAsync(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        TenantSlug slug;

        try
        {
            slug = TenantSlug.Create(command.Slug);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<TenantId>(new Error("tenant.slug_invalid", exception.Message));
        }

        if (await tenants.SlugExistsAsync(slug.Value, cancellationToken))
        {
            return Result.Failure<TenantId>(
                new Error("tenant.slug_conflict", "This slug is already in use."));
        }

        var tenant = Tenant.Create(
            command.Name,
            slug,
            string.IsNullOrWhiteSpace(command.DefaultCulture) ? "en" : command.DefaultCulture.Trim(),
            string.IsNullOrWhiteSpace(command.TimeZone) ? "UTC" : command.TimeZone.Trim(),
            clock.UtcNow);

        await tenants.AddAsync(tenant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tenant.Id);
    }
}
