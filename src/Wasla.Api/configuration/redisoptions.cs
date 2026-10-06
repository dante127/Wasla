namespace Wasla.Api.Configuration;

/// <summary>Redis connection settings.</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
}
