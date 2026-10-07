namespace Wasla.BuildingBlocks.Infrastructure.Configuration;

/// <summary>Edge rate limiting settings (section "RateLimiting").</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Whether auth-endpoint rate limiting is enforced.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Login/refresh/logout attempts allowed per window per client.</summary>
    public int AuthPermitLimit { get; set; } = 30;

    /// <summary>Auth rate limit window in seconds.</summary>
    public int AuthWindowSeconds { get; set; } = 60;
}