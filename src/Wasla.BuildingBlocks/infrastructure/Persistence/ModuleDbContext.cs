using Microsoft.EntityFrameworkCore;

namespace Wasla.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for a module. Each module owns its own context, schema and migrations.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
