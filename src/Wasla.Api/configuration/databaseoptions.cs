namespace Wasla.Api.Configuration;

/// <summary>PostgreSQL connection settings.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
}
