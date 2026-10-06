namespace Wasla.Identity.Application.Contracts;

/// <summary>JWT issuing/validation settings (section "Jwt").</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "wasla";

    public string Audience { get; set; } = "wasla-api";

    /// <summary>HS256 signing secret (MVP). Production value comes from the secret store, never source control.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}
