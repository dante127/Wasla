using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for a module. Applies per-module entity configurations, tenant query
/// filters (via <see cref="ApplyTenantFilter{TEntity}"/>) and blocks cross-tenant writes.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    protected ModuleDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected ModuleDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    private bool IsTenantFilterEnabled => _tenantContext?.TenantId is not null;

    private TenantId TenantFilterValue => _tenantContext?.TenantId ?? default;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Computed domain-event collections are never persisted.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.GetProperty("DomainEvents") is not null)
            {
                modelBuilder.Entity(entityType.ClrType).Ignore("DomainEvents");
            }
        }
    }

    /// <summary>
    /// Applies the tenant isolation query filter to a tenant-owned entity. When no tenant is
    /// resolved (system operations, seeding, design-time tooling) the filter is disabled —
    /// resolved contexts only ever see their own tenant's rows.
    /// </summary>
    protected void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
            !IsTenantFilterEnabled || entity.TenantId == TenantFilterValue);
    }

    private void EnforceTenantWriteGuard()
    {
        var currentTenant = _tenantContext?.TenantId;

        if (currentTenant is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity.TenantId.Value != currentTenant.Value.Value)
            {
                throw new InvalidOperationException(
                    $"Cross-tenant write blocked: {entry.Metadata.ClrType.Name} belongs to tenant " +
                    $"{entry.Entity.TenantId.Value} but the current tenant context is {currentTenant.Value.Value}.");
            }
        }
    }

    public override int SaveChanges()
    {
        EnforceTenantWriteGuard();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantWriteGuard();
        return base.SaveChangesAsync(cancellationToken);
    }
}