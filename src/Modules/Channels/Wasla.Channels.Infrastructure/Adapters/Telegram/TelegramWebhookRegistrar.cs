using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;

namespace Wasla.Channels.Infrastructure.Adapters.Telegram;

/// <summary>
/// Registers/unregisters the Telegram provider webhook at connect/disconnect time
/// (docs/webhooks.md §9). No-ops when no public base URL is configured (local development).
/// </summary>
public sealed class TelegramWebhookRegistrar(
    HttpClient httpClient,
    IOptions<TelegramOptions> options,
    IChannelCredentialStore credentials) : IChannelWebhookRegistrar
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ChannelType ChannelType => ChannelType.Telegram;

    public async Task RegisterAsync(
        TenantId tenantId,
        Guid channelId,
        ChannelCredentialDraft draft,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl))
        {
            return;
        }

        if (!draft.Values.TryGetValue(ChannelCredentialKeys.AccessToken, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        draft.Values.TryGetValue(ChannelCredentialKeys.VerifyToken, out var secret);

        var payload = new Dictionary<string, object?>
        {
            ["url"] = options.Value.PublicBaseUrl.TrimEnd('/') + $"/api/v1/webhooks/telegram/{channelId}",
            ["allowed_updates"] = new[] { "message", "callback_query" },
        };

        if (!string.IsNullOrWhiteSpace(secret))
        {
            payload["secret_token"] = secret;
        }

        await PostAsync(token, "setWebhook", payload, cancellationToken);
    }

    public async Task UnregisterAsync(TenantId tenantId, Guid channelId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl))
        {
            return;
        }

        var token = await credentials.GetAsync(
            tenantId, channelId, ChannelCredentialKeys.AccessToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        await PostAsync(
            token,
            "deleteWebhook",
            new Dictionary<string, object?> { ["drop_pending_updates"] = false },
            cancellationToken);
    }

    private async Task PostAsync(
        string token,
        string method,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"bot{token}/{method}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
            {
                return;
            }

            var description = document.RootElement.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString()
                : null;

            throw new InvalidOperationException($"Telegram {method} failed: {description ?? "unknown error"}");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Telegram {method} returned an unparseable response.");
        }
    }
}

/// <summary>Resolves webhook registrars by channel type (optional adapter capability).</summary>
public sealed class ChannelWebhookRegistrarRegistry : IChannelWebhookRegistrarRegistry
{
    private readonly Dictionary<ChannelType, IChannelWebhookRegistrar> _registrars;

    public ChannelWebhookRegistrarRegistry(IEnumerable<IChannelWebhookRegistrar> registrars) =>
        _registrars = registrars
            .GroupBy(registrar => registrar.ChannelType)
            .ToDictionary(group => group.Key, group => group.Last());

    public IChannelWebhookRegistrar? Resolve(ChannelType channelType) =>
        _registrars.TryGetValue(channelType, out var registrar) ? registrar : null;
}
