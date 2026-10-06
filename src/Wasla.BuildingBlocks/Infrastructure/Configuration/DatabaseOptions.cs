namespace Wasla.BuildingBlocks.Infrastructure.Configuration;

/// <summary>PostgreSQL connection settings (section "Database").</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
}
