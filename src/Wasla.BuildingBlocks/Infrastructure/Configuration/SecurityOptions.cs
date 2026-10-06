namespace Wasla.BuildingBlocks.Infrastructure.Configuration;

/// <summary>Security settings (section "Security"). The encryption key is a base64-encoded 32-byte key.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Master key for credential encryption at rest. Never commit a production value.</summary>
    public string EncryptionKey { get; set; } = string.Empty;
}
