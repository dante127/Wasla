using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;

namespace Wasla.Channels.Infrastructure.Adapters.WhatsApp;

/// <summary>
/// Official WhatsApp Cloud API adapter (no unofficial protocols — docs/channels.md §9).
/// Implements inbound webhook verification (X-Hub-Signature-256 / hub.verify_token),
/// payload normalization to the platform model, outbound text/media sends and credential
/// verification for the connect flow. Provider field names live only in this file.
/// </summary>
public sealed class WhatsAppCloudAdapter(
    HttpClient httpClient,
    IOptions<WhatsAppOptions> options,
    IChannelCredentialStore credentials,
    IFileStorage fileStorage,
    IClock clock) : IChannelAdapter, IChannelConnectionVerifier
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string SignatureHeader = "X-Hub-Signature-256";

    public ChannelType ChannelType => ChannelType.WhatsApp;

    public ChannelCapabilities Capabilities { get; } = new(
        SupportsDeliveryReceipts: true,
        SupportsReadReceipts: true,
        SupportsTyping: false,
        SupportsTemplates: true,
        SupportsMedia: true,
        MaxTextLength: 4096);

    // ------------------------------------------------------------------ webhook --

    public async Task<WebhookVerificationResult> VerifyWebhookAsync(
        ChannelWebhookContext context,
        CancellationToken cancellationToken)
    {
        if (string.Equals(context.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return await VerifySubscriptionAsync(context, cancellationToken);
        }

        var signature = GetHeader(context.Headers, SignatureHeader);

        if (string.IsNullOrWhiteSpace(signature))
        {
            return WebhookVerificationResult.Invalid("Missing signature header.");
        }

        var appSecret = await credentials.GetAsync(
            context.TenantId,
            context.ChannelId,
            ChannelCredentialKeys.AppSecret,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(appSecret))
        {
            return WebhookVerificationResult.Invalid("No app secret configured for this channel.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expected = "sha256=" + Convert.ToHexString(hmac.ComputeHash(context.RawBody)).ToLowerInvariant();

        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));

        return valid
            ? WebhookVerificationResult.Valid()
            : WebhookVerificationResult.Invalid("Signature mismatch.");
    }

    private async Task<WebhookVerificationResult> VerifySubscriptionAsync(
        ChannelWebhookContext context,
        CancellationToken cancellationToken)
    {
        var mode = GetQuery(context.Query, "hub.mode");
        var token = GetQuery(context.Query, "hub.verify_token");
        var challenge = GetQuery(context.Query, "hub.challenge");

        if (!string.Equals(mode, "subscribe", StringComparison.Ordinal) || string.IsNullOrEmpty(token))
        {
            return WebhookVerificationResult.Invalid("Unsupported subscription verification request.");
        }

        var verifyToken = await credentials.GetAsync(
            context.TenantId,
            context.ChannelId,
            ChannelCredentialKeys.VerifyToken,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(verifyToken))
        {
            return WebhookVerificationResult.Invalid("No verify token configured for this channel.");
        }

        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(verifyToken));

        return valid
            ? WebhookVerificationResult.Valid(challenge)
            : WebhookVerificationResult.Invalid("Verify token mismatch.");
    }

    // --------------------------------------------------------------- normalize --

    public Task<IReadOnlyList<NormalizedInboundEvent>> NormalizeInboundAsync(
        ChannelWebhookContext context,
        CancellationToken cancellationToken)
    {
        var events = new List<NormalizedInboundEvent>();

        try
        {
            using var document = JsonDocument.Parse(context.RawBody);

            if (!document.RootElement.TryGetProperty("entry", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                return Task.FromResult<IReadOnlyList<NormalizedInboundEvent>>(events);
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes))
                {
                    continue;
                }

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                    {
                        continue;
                    }

                    NormalizeValue(value, events);
                }
            }
        }
        catch (JsonException)
        {
            // Signed-but-unparseable payloads stay as raw events (docs/webhooks.md §2).
        }

        return Task.FromResult<IReadOnlyList<NormalizedInboundEvent>>(events);
    }

    private void NormalizeValue(JsonElement value, List<NormalizedInboundEvent> events)
    {
        string? displayName = null;

        if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Array)
        {
            displayName = contacts.EnumerateArray()
                .Select(contact => contact.TryGetProperty("profile", out var profile)
                    && profile.TryGetProperty("name", out var name)
                        ? name.GetString()
                        : null)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        }

        if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in messages.EnumerateArray())
            {
                var inbound = NormalizeMessage(message, displayName);

                if (inbound is not null)
                {
                    events.Add(inbound);
                }
            }
        }

        if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
        {
            foreach (var status in statuses.EnumerateArray())
            {
                var update = NormalizeStatus(status);

                if (update is not null)
                {
                    events.Add(update);
                }
            }
        }
    }

    private InboundMessageEvent? NormalizeMessage(JsonElement message, string? displayName)
    {
        var providerMessageId = message.TryGetProperty("id", out var id) ? id.GetString() : null;
        var from = message.TryGetProperty("from", out var fromElement) ? fromElement.GetString() : null;
        var type = message.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(providerMessageId) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var sentAt = message.TryGetProperty("timestamp", out var timestamp)
            && long.TryParse(timestamp.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : clock.UtcNow;

        var replyTo = message.TryGetProperty("context", out var context)
            && context.TryGetProperty("id", out var contextId)
                ? contextId.GetString()
                : null;

        var (messageType, body, media) = MapContent(message, type);

        var metadata = "{\"schemaVersion\":1,\"channel\":\"whatsapp\",\"message\":" + message.GetRawText() + "}";

        return new InboundMessageEvent(
            providerMessageId,
            from,
            displayName,
            messageType,
            body,
            sentAt,
            replyTo,
            metadata,
            media);
    }

    private static (MessageType Type, string? Body, IReadOnlyList<InboundMediaReference> Media) MapContent(
        JsonElement message,
        string type)
    {
        switch (type)
        {
            case "text":
                return (MessageType.Text, GetNested(message, "text", "body"), []);

            case "image":
            case "video":
            case "audio":
            case "document":
            case "sticker":
            {
                var mapped = type switch
                {
                    "image" => MessageType.Image,
                    "video" => MessageType.Video,
                    "audio" => MessageType.Audio,
                    "document" => MessageType.Document,
                    _ => MessageType.Sticker,
                };

                var media = BuildMediaReference(message, type, mapped);

                return (mapped, media?.Caption, media is null ? [] : [media]);
            }

            case "location":
                return (MessageType.Location, GetNested(message, "location", "name"), []);

            case "contacts":
                return (MessageType.Contact, null, []);

            case "button":
                return (MessageType.Interactive, GetNested(message, "button", "text"), []);

            case "interactive":
            {
                var buttonReply = GetNested(message, "interactive", "button_reply", "title");
                var listReply = GetNested(message, "interactive", "list_reply", "title");

                return (MessageType.Interactive, buttonReply ?? listReply, []);
            }

            default:
                return (MessageType.System, type, []);
        }
    }

    private static InboundMediaReference? BuildMediaReference(JsonElement message, string type, MessageType mapped)
    {
        if (!message.TryGetProperty(type, out var media) || media.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var providerMediaId = media.TryGetProperty("id", out var id) ? id.GetString() : null;

        if (string.IsNullOrWhiteSpace(providerMediaId))
        {
            return null;
        }

        return new InboundMediaReference(
            providerMediaId,
            mapped,
            media.TryGetProperty("mime_type", out var mime) ? mime.GetString() : null,
            media.TryGetProperty("filename", out var fileName) ? fileName.GetString() : null,
            media.TryGetProperty("caption", out var caption) ? caption.GetString() : null);
    }

    private static MessageStatusUpdateEvent? NormalizeStatus(JsonElement status)
    {
        var providerMessageId = status.TryGetProperty("id", out var id) ? id.GetString() : null;
        var state = status.TryGetProperty("status", out var stateElement) ? stateElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(providerMessageId) || string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var kind = state switch
        {
            "sent" => MessageStatusUpdateKind.Sent,
            "delivered" => MessageStatusUpdateKind.Delivered,
            "read" => MessageStatusUpdateKind.Read,
            "failed" => MessageStatusUpdateKind.Failed,
            _ => (MessageStatusUpdateKind?)null,
        };

        if (kind is null)
        {
            return null;
        }

        string? failureReason = null;

        if (kind == MessageStatusUpdateKind.Failed
            && status.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array)
        {
            failureReason = errors.EnumerateArray()
                .Select(error => error.TryGetProperty("title", out var title)
                    ? title.GetString()
                    : error.TryGetProperty("message", out var message)
                        ? message.GetString()
                        : null)
                .FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason));
        }
        else if (kind == MessageStatusUpdateKind.Failed)
        {
            failureReason = "Provider reported a failure.";
        }

        var occurredAt = status.TryGetProperty("timestamp", out var timestamp)
            && long.TryParse(timestamp.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : DateTimeOffset.UtcNow;

        return new MessageStatusUpdateEvent(providerMessageId, kind.Value, failureReason, occurredAt);
    }

    // ------------------------------------------------------------------ outbound --

    public async Task<ChannelSendResult> SendMessageAsync(
        ChannelOutboundMessage message,
        CancellationToken cancellationToken)
    {
        var accessToken = await credentials.GetAsync(
            message.TenantId, message.ChannelId, ChannelCredentialKeys.AccessToken, cancellationToken);
        var phoneNumberId = await credentials.GetAsync(
            message.TenantId, message.ChannelId, ChannelCredentialKeys.PhoneNumberId, cancellationToken);

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Invalid, "Channel credentials are incomplete.");
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = message.RecipientExternalId,
            type = "text",
            text = new { preview_url = false, body = message.Body },
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.Value.ApiVersion}/{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await SendAsync(request, cancellationToken);
    }

    public async Task<ChannelSendResult> SendMediaAsync(
        ChannelOutboundMediaMessage message,
        CancellationToken cancellationToken)
    {
        var accessToken = await credentials.GetAsync(
            message.TenantId, message.ChannelId, ChannelCredentialKeys.AccessToken, cancellationToken);
        var phoneNumberId = await credentials.GetAsync(
            message.TenantId, message.ChannelId, ChannelCredentialKeys.PhoneNumberId, cancellationToken);

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Invalid, "Channel credentials are incomplete.");
        }

        var waType = message.Type switch
        {
            MessageType.Image => "image",
            MessageType.Video => "video",
            MessageType.Audio => "audio",
            MessageType.Document => "document",
            _ => null,
        };

        if (waType is null)
        {
            return ChannelSendResult.Failure(
                ChannelSendFailureKind.Invalid,
                $"WhatsApp cannot send message type '{message.Type}'.");
        }

        var bytes = await ReadMediaAsync(message, cancellationToken);

        if (bytes is null)
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Invalid, "Media file could not be read.");
        }

        var mediaId = await UploadMediaAsync(
            phoneNumberId,
            accessToken,
            bytes,
            message.ContentType,
            message.FileName,
            cancellationToken);

        if (!mediaId.IsSuccess)
        {
            return mediaId.Result!;
        }

        object mediaObject = waType == "document"
            ? new { id = mediaId.MediaId, caption = message.Caption, filename = message.FileName }
            : new { id = mediaId.MediaId, caption = message.Caption };

        var payload = new Dictionary<string, object>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = message.RecipientExternalId,
            ["type"] = waType,
            [waType] = mediaObject,
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.Value.ApiVersion}/{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await SendAsync(request, cancellationToken);
    }

    private async Task<byte[]?> ReadMediaAsync(ChannelOutboundMediaMessage message, CancellationToken cancellationToken)
    {
        await using var stream = await fileStorage.OpenReadAsync(message.StorageKey, cancellationToken);

        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    private async Task<(bool IsSuccess, string? MediaId, ChannelSendResult? Result)> UploadMediaAsync(
        string phoneNumberId,
        string accessToken,
        byte[] bytes,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent(contentType), "type");
        form.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.Value.ApiVersion}/{phoneNumberId}/media")
        {
            Content = form,
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return (false, null, ChannelSendResult.Failure(ChannelSendFailureKind.Transient, $"Network error: {exception.Message}"));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                return (false, null, MapError(response.StatusCode, body));
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var mediaId = TryReadMessageId(json, "id");

            return mediaId is null
                ? (false, null, ChannelSendResult.Failure(ChannelSendFailureKind.Transient, "Media upload returned no id."))
                : (true, mediaId, null);
        }
    }

    private async Task<ChannelSendResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return ChannelSendResult.Failure(
                ChannelSendFailureKind.Transient,
                $"Network error: {exception.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var messageId = TryReadMessageId(body, "messages", "id");

                return messageId is null
                    ? ChannelSendResult.Failure(ChannelSendFailureKind.Transient, "Provider accepted but returned no message id.")
                    : ChannelSendResult.Success(messageId);
            }

            return MapError(response.StatusCode, body);
        }
    }


    private static ChannelSendResult MapError(HttpStatusCode statusCode, string body)
    {
        var (code, message) = ExtractError(body);
        var detail = code is null ? message : $"{code}: {message}";

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return ChannelSendResult.Failure(
                ChannelSendFailureKind.RateLimited,
                detail ?? "Provider rate limit reached.",
                TimeSpan.FromSeconds(30));
        }

        if ((int)statusCode >= 500)
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Transient, detail ?? "Provider server error.");
        }

        return ChannelSendResult.Failure(
            ChannelSendFailureKind.Rejected,
            detail ?? $"Provider rejected the request ({(int)statusCode}).");
    }

    private static (string? Code, string? Message) ExtractError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("code", out var codeElement)
                    ? codeElement.ValueKind == JsonValueKind.Number
                        ? codeElement.GetInt64().ToString(CultureInfo.InvariantCulture)
                        : codeElement.GetString()
                    : null;

                var message = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;

                return (code, message);
            }
        }
        catch (JsonException)
        {
            // Fall through with the raw body omitted (never echoed — may contain tokens).
        }

        return (null, null);
    }

    private static string? TryReadMessageId(string json, params string[] path)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var current = document.RootElement;

            foreach (var segment in path)
            {
                if (current.ValueKind == JsonValueKind.Array)
                {
                    if (current.GetArrayLength() == 0)
                    {
                        return null;
                    }

                    current = current[0];
                }

                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    return null;
                }
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------- connect --

    public async Task<ChannelConnectionResult> VerifyAndDiscoverAsync(
        ChannelCredentialDraft draft,
        CancellationToken cancellationToken)
    {
        if (options.Value.SkipConnectVerification)
        {
            var phoneNumberId = draft.Values.GetValueOrDefault(ChannelCredentialKeys.PhoneNumberId);

            return ChannelConnectionResult.Valid(phoneNumberId);
        }

        if (!draft.Values.TryGetValue(ChannelCredentialKeys.AccessToken, out var accessToken)
            || string.IsNullOrWhiteSpace(accessToken)
            || !draft.Values.TryGetValue(ChannelCredentialKeys.PhoneNumberId, out var phoneId)
            || string.IsNullOrWhiteSpace(phoneId))
        {
            return ChannelConnectionResult.Invalid("Access token and phone number id are required.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{options.Value.ApiVersion}/{phoneId}?fields=display_phone_number,verified_name");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var displayPhone = TryReadString(body, "display_phone_number");

                return ChannelConnectionResult.Valid(displayPhone is null ? phoneId : phoneId);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var (code, message) = ExtractError(errorBody);

            return ChannelConnectionResult.Invalid(
                code is null ? $"Provider rejected the credentials ({(int)response.StatusCode})." : $"{code}: {message}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return ChannelConnectionResult.Invalid($"Provider unreachable: {exception.Message}");
        }
    }

    private static string? TryReadString(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // -------------------------------------------------------------------- helpers --

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value)
            ? value
            : headers.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static string? GetQuery(IReadOnlyDictionary<string, string> query, string name) =>
        query.TryGetValue(name, out var value)
            ? value
            : query.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static string? GetNested(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
