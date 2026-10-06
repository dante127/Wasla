namespace Wasla.Channels.Infrastructure.Adapters.WhatsApp;

/// <summary>WhatsApp Cloud API settings (section "WhatsApp").</summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>Graph API base URL (overridable in tests/stubs).</summary>
    public string BaseUrl { get; set; } = "https://graph.facebook.com";

    /// <summary>Graph API version segment, e.g. "v21.0".</summary>
    public string ApiVersion { get; set; } = "v21.0";

    /// <summary>HTTP timeout in seconds for provider calls.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Dev/test convenience: accept credentials without calling the Graph API.
    /// Never enable in production.
    /// </summary>
    public bool SkipConnectVerification { get; set; }
}
