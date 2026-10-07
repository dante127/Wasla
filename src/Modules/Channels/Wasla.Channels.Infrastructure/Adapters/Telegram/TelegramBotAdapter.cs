using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;

namespace Wasla.Channels.Infrastructure.Adapters.Telegram;

/// <summary>
/// Telegram Bot API adapter (webhook mode with secret token — docs/channels.md §9).
/// Implements inbound verification + normalization, outbound text/media sends, credential
/// verification (getMe) and media download (getFile). Provider field names live only in this file.
/// </summary>
public sealed class TelegramBotAdapter(
    HttpClient httpClient,
    IOptions<TelegramOptions> options,
    IChannelCredentialStore credentials,
    IFileStorage fileStorage,
    IClock clock) : IChannelAdapter, IChannelConnectionVerifier, IChannelMediaDownloader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";
    private const int MaxDownloadBytes = 16 * 1024 * 1024;

    public ChannelType ChannelType => ChannelType.Telegram;

    public ChannelCapabilities Capabilities { get; } = new(
        SupportsDeliveryReceipts: false,
        SupportsReadReceipts: false,
        SupportsTyping: false,
        SupportsTemplates: false,
        SupportsMedia: true,
        MaxTextLength: 4096);

    // ------------------------------------------------------------------ webhook --

    public async Task<WebhookVerificationResult> VerifyWebhookAsync(
        ChannelWebhookContext context,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(context.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return WebhookVerificationResult.Invalid("Only POST webhooks are supported.");
        }

        var provided = GetHeader(context.Headers, SecretHeader);

        if (string.IsNullOrWhiteSpace(provided))
        {
            return WebhookVerificationResult.Invalid("Missing secret token header.");
        }

        var expected = await credentials.GetAsync(
            context.TenantId,
            context.ChannelId,
            ChannelCredentialKeys.VerifyToken,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(expected))
        {
            return WebhookVerificationResult.Invalid("No webhook secret configured for this channel.");
        }

        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided.Trim()),
            Encoding.UTF8.GetBytes(expected));

        return valid
            ? WebhookVerificationResult.Valid()
            : WebhookVerificationResult.Invalid("Secret token mismatch.");
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

            if (!document.RootElement.TryGetProperty("update_id", out _))
            {
                return Task.FromResult<IReadOnlyList<NormalizedInboundEvent>>(events);
            }

            if (document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.Object)
            {
                var normalized = NormalizeMessage(message);

                if (normalized is not null)
                {
                    events.Add(normalized);
                }
            }
            else if (document.RootElement.TryGetProperty("callback_query", out var callback)
                && callback.ValueKind == JsonValueKind.Object)
            {
                var normalized = NormalizeCallback(callback);

                if (normalized is not null)
                {
                    events.Add(normalized);
                }
            }

            // edited_message / channel_post / other update kinds are intentionally ignored (MVP).
        }
        catch (JsonException)
        {
            // Signed-but-unparseable payloads stay as raw events (docs/webhooks.md §2).
        }

        return Task.FromResult<IReadOnlyList<NormalizedInboundEvent>>(events);
    }

    private InboundMessageEvent? NormalizeMessage(JsonElement message)
    {
        var messageId = ReadLongAsString(message, "message_id");

        if (messageId is null || !message.TryGetProperty("chat", out var chat))
        {
            return null;
        }

        var chatId = ReadLongAsString(chat, "id");

        if (chatId is null)
        {
            return null;
        }

        var displayName = ReadDisplayName(message);
        var sentAt = ReadDate(message, "date");
        var replyTo = message.TryGetProperty("reply_to_message", out var reply)
            ? ReadLongAsString(reply, "message_id")
            : null;

        var (messageType, body, media) = MapContent(message);
        var metadata = "{\"schemaVersion\":1,\"channel\":\"telegram\",\"message\":" + message.GetRawText() + "}";

        return new InboundMessageEvent(
            messageId,
            chatId,
            displayName,
            messageType,
            body,
            sentAt,
            replyTo,
            metadata,
            media);
    }

    private InboundMessageEvent? NormalizeCallback(JsonElement callback)
    {
        var callbackId = callback.TryGetProperty("id", out var id) ? id.GetString() : null;

        if (string.IsNullOrWhiteSpace(callbackId))
        {
            return null;
        }

        string? chatId = null;

        if (callback.TryGetProperty("message", out var message)
            && message.TryGetProperty("chat", out var chat))
        {
            chatId = ReadLongAsString(chat, "id");
        }

        chatId ??= callback.TryGetProperty("from", out var from) ? ReadLongAsString(from, "id") : null;

        if (chatId is null)
        {
            return null;
        }

        var data = callback.TryGetProperty("data", out var dataElement) ? dataElement.GetString() : null;
        var metadata = "{\"schemaVersion\":1,\"channel\":\"telegram\",\"callback_query\":" + callback.GetRawText() + "}";

        return new InboundMessageEvent(
            callbackId,
            chatId,
            ReadDisplayName(callback),
            MessageType.Interactive,
            data,
            clock.UtcNow,
            null,
            metadata,
            []);
    }

    private static (MessageType Type, string? Body, IReadOnlyList<InboundMediaReference> Media) MapContent(JsonElement message)
    {
        if (message.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            return (MessageType.Text, text.GetString(), []);
        }

        string? caption = message.TryGetProperty("caption", out var captionElement) && captionElement.ValueKind == JsonValueKind.String
            ? captionElement.GetString()
            : null;

        if (message.TryGetProperty("photo", out var photo) && photo.ValueKind == JsonValueKind.Array)
        {
            // Photos arrive ordered by ascending size; the last entry is the largest.
            var largest = photo.EnumerateArray().LastOrDefault();

            if (largest.ValueKind == JsonValueKind.Object
                && largest.TryGetProperty("file_id", out var fileId)
                && fileId.ValueKind == JsonValueKind.String)
            {
                return (MessageType.Image, caption, [new InboundMediaReference(fileId.GetString()!, MessageType.Image, null, null, caption)]);
            }
        }

        var mediaType = message.TryGetProperty("document", out var document) ? MessageType.Document
            : message.TryGetProperty("video", out document) ? MessageType.Video
            : message.TryGetProperty("animation", out document) ? MessageType.Video
            : message.TryGetProperty("voice", out document) ? MessageType.Audio
            : message.TryGetProperty("audio", out document) ? MessageType.Audio
            : message.TryGetProperty("sticker", out document) ? MessageType.Sticker
            : (MessageType?)null;

        if (mediaType is not null
            && document.TryGetProperty("file_id", out var mediaFileId)
            && mediaFileId.ValueKind == JsonValueKind.String)
        {
            var mime = document.TryGetProperty("mime_type", out var mimeElement) ? mimeElement.GetString() : null;
            var fileName = document.TryGetProperty("file_name", out var fileNameElement) ? fileNameElement.GetString() : null;

            return (mediaType.Value, caption, [new InboundMediaReference(mediaFileId.GetString()!, mediaType.Value, mime, fileName, caption)]);
        }

        if (message.TryGetProperty("location", out _))
        {
            return (MessageType.Location, null, []);
        }

        if (message.TryGetProperty("contact", out _))
        {
            return (MessageType.Contact, null, []);
        }

        return (MessageType.System, null, []);
    }

    // ------------------------------------------------------------------ outbound --

    public async Task<ChannelSendResult> SendMessageAsync(
        ChannelOutboundMessage message,
        CancellationToken cancellationToken)
    {
        var token = await credentials.GetAsync(
            message.TenantId, message.ChannelId, ChannelCredentialKeys.AccessToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Invalid, "Channel credentials are incomplete.");
        }

        var payload = new { chat_id = message.RecipientExternalId, text = message.Body };

        return await SendJsonAsync(token, "sendMessage", payload, cancellationToken);
    }

    public async Task<ChannelSendResult> SendMediaAsync(
        ChannelOutboundMediaMessage message,
        CancellationToken cancellationToken)
    {
        var token = await credentials.GetAsync(
            message.TenantId, message.ChannelId, ChannelCredentialKeys.AccessToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Invalid, "Channel credentials are incomplete.");
        }

        var (method, field) = message.Type switch
        {
            MessageType.Image => ("sendPhoto", "photo"),
            MessageType.Video => ("sendVideo", "video"),
            MessageType.Audio => ("sendAudio", "audio"),
            MessageType.Document => ("sendDocument", "document"),
            MessageType.Sticker => ("sendSticker", "sticker"),
            _ => (null, null),
        };

        if (method is null || field is null)
        {
            return ChannelSendResult.Failure(
                ChannelSendFailureKind.Invalid,
                $"Telegram cannot send message type '{message.Type}'.");
        }

        await using var stream = await fileStorage.OpenReadAsync(message.StorageKey, cancellationToken);

        if (stream is null)
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Invalid, "Media file could not be read.");
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(message.RecipientExternalId), "chat_id");

        if (!string.IsNullOrWhiteSpace(message.Caption))
        {
            form.Add(new StringContent(message.Caption), "caption");
        }

        var fileContent = new ByteArrayContent(buffer.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(message.ContentType);
        form.Add(fileContent, field, message.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"bot{token}/{method}")
        {
            Content = form,
        };

        return await SendAsync(request, cancellationToken);
    }

    private async Task<ChannelSendResult> SendJsonAsync(string token, string method, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"bot{token}/{method}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
        };

        return await SendAsync(request, cancellationToken);
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
            return ChannelSendResult.Failure(ChannelSendFailureKind.Transient, $"Network error: {exception.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return Interpret(body, response.StatusCode);
        }
    }

    private static ChannelSendResult Interpret(string body, HttpStatusCode statusCode)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True;

            if (ok)
            {
                if (root.TryGetProperty("result", out var result)
                    && result.TryGetProperty("message_id", out var messageId)
                    && messageId.ValueKind == JsonValueKind.Number)
                {
                    return ChannelSendResult.Success(messageId.GetInt64().ToString(CultureInfo.InvariantCulture));
                }

                return ChannelSendResult.Failure(ChannelSendFailureKind.Transient, "Provider accepted but returned no message id.");
            }

            var description = root.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString()
                : null;
            var errorCode = root.TryGetProperty("error_code", out var errorCodeElement)
                && errorCodeElement.ValueKind == JsonValueKind.Number
                    ? errorCodeElement.GetInt64()
                    : (int)statusCode;

            if (errorCode == 429)
            {
                var retryAfter = 30L;

                if (root.TryGetProperty("parameters", out var parameters)
                    && parameters.TryGetProperty("retry_after", out var retryElement)
                    && retryElement.ValueKind == JsonValueKind.Number)
                {
                    retryAfter = retryElement.GetInt64();
                }

                return ChannelSendResult.Failure(
                    ChannelSendFailureKind.RateLimited,
                    description ?? "Rate limit reached.",
                    TimeSpan.FromSeconds(Math.Clamp(retryAfter, 1, 3600)));
            }

            if (errorCode >= 500)
            {
                return ChannelSendResult.Failure(ChannelSendFailureKind.Transient, description ?? "Provider server error.");
            }

            return ChannelSendResult.Failure(
                ChannelSendFailureKind.Rejected,
                description ?? $"Provider rejected the request ({errorCode}).");
        }
        catch (JsonException)
        {
            return ChannelSendResult.Failure(ChannelSendFailureKind.Transient, "Unparseable provider response.");
        }
    }

    // ------------------------------------------------------------------- connect --

    public async Task<ChannelConnectionResult> VerifyAndDiscoverAsync(
        ChannelCredentialDraft draft,
        CancellationToken cancellationToken)
    {
        if (options.Value.SkipConnectVerification)
        {
            return ChannelConnectionResult.Valid();
        }

        if (!draft.Values.TryGetValue(ChannelCredentialKeys.AccessToken, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            return ChannelConnectionResult.Invalid("Bot token is required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"bot{token}/getMe");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var description = TryReadString(body, "description");

                return ChannelConnectionResult.Invalid(description ?? $"Provider rejected the token ({(int)response.StatusCode}).");
            }

            var username = TryReadString(body, "username");

            return ChannelConnectionResult.Valid(username is null ? null : "@" + username);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return ChannelConnectionResult.Invalid($"Provider unreachable: {exception.Message}");
        }
    }

    // -------------------------------------------------------------------- media --

    public async Task<DownloadedMedia?> DownloadAsync(
        TenantId tenantId,
        Guid channelId,
        InboundMediaReference media,
        CancellationToken cancellationToken)
    {
        var token = await credentials.GetAsync(
            tenantId, channelId, ChannelCredentialKeys.AccessToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            using var infoRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"bot{token}/getFile?file_id={Uri.EscapeDataString(media.ProviderMediaId)}");

            using var infoResponse = await httpClient.SendAsync(infoRequest, cancellationToken);

            if (!infoResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var infoBody = await infoResponse.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(infoBody);

            if (!document.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                || !document.RootElement.TryGetProperty("result", out var result)
                || !result.TryGetProperty("file_path", out var filePathElement))
            {
                return null;
            }

            if (result.TryGetProperty("file_size", out var fileSize)
                && fileSize.ValueKind == JsonValueKind.Number
                && fileSize.GetInt64() > MaxDownloadBytes)
            {
                return null;
            }

            var filePath = filePathElement.GetString();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            using var fileResponse = await httpClient.GetAsync($"file/bot{token}/{filePath}", cancellationToken);

            if (!fileResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await fileResponse.Content.ReadAsByteArrayAsync(cancellationToken);

            if (bytes.Length > MaxDownloadBytes)
            {
                return null;
            }

            var contentType = fileResponse.Content.Headers.ContentType?.MediaType ?? DefaultContentType(media.Type);

            return new DownloadedMedia(bytes, contentType, FileNameFor(media, contentType));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    // -------------------------------------------------------------------- helpers --

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value)
            ? value
            : headers.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static string? ReadLongAsString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt64().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => value.GetString(),
            _ => null,
        };
    }

    private static string? ReadDisplayName(JsonElement element)
    {
        if (!element.TryGetProperty("from", out var from) || from.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var first = from.TryGetProperty("first_name", out var firstName) ? firstName.GetString() : null;
        var last = from.TryGetProperty("last_name", out var lastName) ? lastName.GetString() : null;
        var username = from.TryGetProperty("username", out var usernameElement) ? usernameElement.GetString() : null;

        var full = string.Join(" ", new[] { first, last }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrWhiteSpace(full) ? username : full;
    }

    private DateTimeOffset ReadDate(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(value.GetInt64());
            }
            catch (ArgumentOutOfRangeException)
            {
                return clock.UtcNow;
            }
        }

        return clock.UtcNow;
    }

    private static string? TryReadString(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("result", out var result))
            {
                return document.RootElement.TryGetProperty(property, out var rootValue) ? rootValue.GetString() : null;
            }

            return result.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string DefaultContentType(MessageType type) => type switch
    {
        MessageType.Image => "image/jpeg",
        MessageType.Video => "video/mp4",
        MessageType.Audio => "audio/ogg",
        MessageType.Sticker => "image/webp",
        _ => "application/octet-stream",
    };

    private static string FileNameFor(InboundMediaReference media, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(media.FileName))
        {
            return media.FileName;
        }

        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            "audio/ogg" => ".ogg",
            "audio/mpeg" => ".mp3",
            _ => ".bin",
        };

        return media.Type.ToString().ToLowerInvariant() + extension;
    }
}
