namespace Wasla.Channels.Infrastructure.Adapters.Telegram;

/// <summary>Telegram Bot API settings (section "Telegram").</summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Bot API base URL (overridable in tests/stubs).</summary>
    public string BaseUrl { get; set; } = "https://api.telegram.org";

    /// <summary>HTTP timeout in seconds for provider calls.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Public HTTPS base URL of this deployment; when set, the connect flow registers the
    /// provider webhook (setWebhook) with a secret token (docs/webhooks.md §9). Empty in
    /// local development — webhooks are registered manually via a tunnel.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Dev/test convenience: accept credentials without calling the Bot API.
    /// Never enable in production.
    /// </summary>
    public bool SkipConnectVerification { get; set; }
}
