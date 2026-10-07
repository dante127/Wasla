using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.BuildingBlocks.Infrastructure;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Infrastructure.Adapters.Telegram;

namespace Wasla.UnitTests;

/// <summary>Fixture replay tests for the Telegram Bot API adapter (no live provider calls).</summary>
public sealed class TelegramAdapterTests
{
    private const string BotToken = "test-bot-token";
    private const string WebhookSecret = "unit-tg-secret";

    private static readonly Guid ChannelId = Guid.Parse("22222222-3333-4444-5555-666666666666");

    // ------------------------------------------------------------- webhook verify --

    [Fact]
    public async Task Verify_accepts_matching_secret_token_and_rejects_others()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());

        var valid = await adapter.VerifyWebhookAsync(PostContext(WebhookSecret), CancellationToken.None);
        Assert.True(valid.IsValid);

        var wrong = await adapter.VerifyWebhookAsync(PostContext("wrong"), CancellationToken.None);
        Assert.False(wrong.IsValid);

        var missing = await adapter.VerifyWebhookAsync(PostContext(null), CancellationToken.None);
        Assert.False(missing.IsValid);

        var get = await adapter.VerifyWebhookAsync(
            new ChannelWebhookContext(ChannelId, TenantId.New(), "GET", new Dictionary<string, string>(), new Dictionary<string, string>(), [], DateTimeOffset.UtcNow),
            CancellationToken.None);
        Assert.False(get.IsValid);
    }

    // ---------------------------------------------------------------- normalize --

    [Fact]
    public async Task Normalize_maps_text_message_with_sender_name_and_reply()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var payload = """
            {"update_id":1001,"message":{"message_id":55,"date":1730000000,
              "chat":{"id":777200100,"type":"private"},
              "from":{"id":777200100,"first_name":"Tariq","last_name":"Test","username":"tariq"},
              "text":"Hello from Telegram","reply_to_message":{"message_id":50,"text":"earlier"}}}
            """;

        var events = await adapter.NormalizeInboundAsync(PostContext(WebhookSecret, payload), CancellationToken.None);

        var message = Assert.IsType<InboundMessageEvent>(Assert.Single(events));
        Assert.Equal("55", message.ProviderMessageId);
        Assert.Equal("777200100", message.ExternalCustomerId);
        Assert.Equal("Tariq Test", message.CustomerDisplayName);
        Assert.Equal(MessageType.Text, message.Type);
        Assert.Equal("Hello from Telegram", message.Body);
        Assert.Equal("50", message.ReplyToProviderMessageId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1730000000), message.SentAt);
        Assert.Contains("schemaVersion", message.ProviderMetadata);
    }

    [Fact]
    public async Task Normalize_picks_largest_photo_and_maps_document()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var photoPayload = """
            {"update_id":1002,"message":{"message_id":56,"date":1730000100,"chat":{"id":777200101},
              "photo":[{"file_id":"small","file_size":100},{"file_id":"big","file_size":9000}],"caption":"Look!"}}
            """;

        var photoEvents = await adapter.NormalizeInboundAsync(PostContext(WebhookSecret, photoPayload), CancellationToken.None);
        var photo = Assert.IsType<InboundMessageEvent>(Assert.Single(photoEvents));
        Assert.Equal(MessageType.Image, photo.Type);
        Assert.Equal("Look!", photo.Body);
        Assert.Equal("big", Assert.Single(photo.Media).ProviderMediaId);

        var documentPayload = """
            {"update_id":1003,"message":{"message_id":57,"date":1730000200,"chat":{"id":777200101},
              "document":{"file_id":"doc-9","file_name":"invoice.pdf","mime_type":"application/pdf"}}}
            """;

        var documentEvents = await adapter.NormalizeInboundAsync(PostContext(WebhookSecret, documentPayload), CancellationToken.None);
        var document = Assert.IsType<InboundMessageEvent>(Assert.Single(documentEvents));
        Assert.Equal(MessageType.Document, document.Type);
        var documentMedia = Assert.Single(document.Media);
        Assert.Equal("doc-9", documentMedia.ProviderMediaId);
        Assert.Equal("invoice.pdf", documentMedia.FileName);
        Assert.Equal("application/pdf", documentMedia.MimeType);
    }

    [Fact]
    public async Task Normalize_maps_callback_query_to_interactive()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var payload = """
            {"update_id":1004,"callback_query":{"id":"cb-1","from":{"id":777200102,"first_name":"Nour"},
              "message":{"message_id":60,"chat":{"id":777200102}},"data":"menu:order_42"}}
            """;

        var events = await adapter.NormalizeInboundAsync(PostContext(WebhookSecret, payload), CancellationToken.None);

        var callback = Assert.IsType<InboundMessageEvent>(Assert.Single(events));
        Assert.Equal(MessageType.Interactive, callback.Type);
        Assert.Equal("menu:order_42", callback.Body);
        Assert.Equal("cb-1", callback.ProviderMessageId);
        Assert.Equal("777200102", callback.ExternalCustomerId);
    }

    [Fact]
    public async Task Normalize_ignores_unknown_update_shapes()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());

        var events = await adapter.NormalizeInboundAsync(
            PostContext(WebhookSecret, """{"update_id":1005,"channel_post":{"message_id":1}}"""),
            CancellationToken.None);

        Assert.Empty(events);
    }

    // ----------------------------------------------------------------- outbound --

    [Fact]
    public async Task SendMessage_posts_expected_payload_and_reads_message_id()
    {
        var handler = new FakeHttpHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"ok":true,"result":{"message_id":123,"chat":{"id":777200100}}}""");
        var adapter = CreateAdapter(handler);

        var result = await adapter.SendMessageAsync(
            new ChannelOutboundMessage(TenantId.New(), ChannelId, Guid.NewGuid(), "777200100", "Hi from Wasla"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("123", result.ProviderMessageId);

        var request = Assert.Single(handler.Requests);
        Assert.Contains($"/bot{BotToken}/sendMessage", request.Uri.ToString());
        Assert.Contains("\"chat_id\":\"777200100\"", request.Body);
        Assert.Contains("Hi from Wasla", request.Body);
    }

    [Fact]
    public async Task SendMessage_maps_rate_limit_transient_and_rejection()
    {
        var rateLimited = await SendWith(
            HttpStatusCode.TooManyRequests,
            """{"ok":false,"error_code":429,"description":"Too Many Requests: retry later","parameters":{"retry_after":7}}""");
        Assert.Equal(ChannelSendFailureKind.RateLimited, rateLimited.FailureKind);
        Assert.Equal(TimeSpan.FromSeconds(7), rateLimited.RetryAfter);

        var transient = await SendWith(HttpStatusCode.BadGateway, """{"ok":false,"error_code":502,"description":"Bad Gateway"}""");
        Assert.Equal(ChannelSendFailureKind.Transient, transient.FailureKind);

        var rejected = await SendWith(HttpStatusCode.BadRequest, """{"ok":false,"error_code":400,"description":"Bad Request: chat not found"}""");
        Assert.Equal(ChannelSendFailureKind.Rejected, rejected.FailureKind);
        Assert.Contains("chat not found", rejected.FailureReason);
    }

    [Fact]
    public async Task SendMedia_uploads_multipart_with_caption()
    {
        var handler = new FakeHttpHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"ok":true,"result":{"message_id":77}}""");
        var adapter = CreateAdapter(handler, new FakeFileStorage([1, 2, 3]));

        var result = await adapter.SendMediaAsync(
            new ChannelOutboundMediaMessage(
                TenantId.New(), ChannelId, Guid.NewGuid(), "777200100", MessageType.Document,
                "tenant/x/invoice.pdf", "application/pdf", "invoice.pdf", 3, "Your invoice"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var request = Assert.Single(handler.Requests);
        Assert.Contains($"/bot{BotToken}/sendDocument", request.Uri.ToString());
        Assert.Contains("invoice.pdf", request.Body);
        Assert.Contains("Your invoice", request.Body);
    }

    // ------------------------------------------------------------------- connect --

    [Fact]
    public async Task Connect_discovery_reads_bot_username()
    {
        var handler = new FakeHttpHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"ok":true,"result":{"id":42,"username":"wasla_bot","first_name":"Wasla"}}""");
        var adapter = CreateAdapter(handler, skipConnectVerification: false);

        var result = await adapter.VerifyAndDiscoverAsync(
            new ChannelCredentialDraft(new Dictionary<string, string> { [ChannelCredentialKeys.AccessToken] = BotToken }),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("@wasla_bot", result.ExternalAccountId);

        var invalid = await adapter.VerifyAndDiscoverAsync(
            new ChannelCredentialDraft(new Dictionary<string, string>()),
            CancellationToken.None);
        Assert.False(invalid.IsValid);
    }

    // --------------------------------------------------------------------- media --

    [Fact]
    public async Task Download_fetches_file_via_getFile_and_returns_bytes()
    {
        var handler = new FakeHttpHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"ok":true,"result":{"file_path":"photos/file_1.jpg","file_size":4}}""");
        handler.EnqueueBytes(HttpStatusCode.OK, [255, 216, 255, 224], "image/jpeg");
        var adapter = CreateAdapter(handler);

        var downloaded = await adapter.DownloadAsync(
            TenantId.New(),
            ChannelId,
            new InboundMediaReference("FILE-1", MessageType.Image, null, null, null),
            CancellationToken.None);

        Assert.NotNull(downloaded);
        Assert.Equal(4, downloaded!.Content.Length);
        Assert.Equal("image/jpeg", downloaded.ContentType);
        Assert.Equal("image.jpg", downloaded.FileName);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("getFile?file_id=FILE-1", handler.Requests[0].Uri.ToString());
        Assert.Contains($"/file/bot{BotToken}/photos/file_1.jpg", handler.Requests[1].Uri.ToString());
    }

    // ------------------------------------------------------------------- fakes --

    private static async Task<ChannelSendResult> SendWith(HttpStatusCode status, string body)
    {
        var handler = new FakeHttpHandler();
        handler.EnqueueJson(status, body);
        var adapter = CreateAdapter(handler);

        return await adapter.SendMessageAsync(
            new ChannelOutboundMessage(TenantId.New(), ChannelId, Guid.NewGuid(), "777200100", "text"),
            CancellationToken.None);
    }

    private static TelegramBotAdapter CreateAdapter(FakeHttpHandler handler, FakeFileStorage? storage = null, bool skipConnectVerification = true)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://tg.test/"),
        };

        return new TelegramBotAdapter(
            httpClient,
            Options.Create(new TelegramOptions { BaseUrl = "https://tg.test", SkipConnectVerification = skipConnectVerification }),
            new FakeCredentialStore(),
            storage ?? new FakeFileStorage([]),
            new SystemClock());
    }

    private static ChannelWebhookContext PostContext(string? secret, string body = """{"update_id":1}""") =>
        new(
            ChannelId,
            TenantId.New(),
            "POST",
            secret is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["X-Telegram-Bot-Api-Secret-Token"] = secret },
            new Dictionary<string, string>(),
            Encoding.UTF8.GetBytes(body),
            DateTimeOffset.UtcNow);

    private sealed class FakeCredentialStore : IChannelCredentialStore
    {
        private readonly Dictionary<string, string> _values = new()
        {
            [ChannelCredentialKeys.AccessToken] = BotToken,
            [ChannelCredentialKeys.VerifyToken] = WebhookSecret,
        };

        public Task<string?> GetAsync(TenantId tenantId, Guid channelId, string key, CancellationToken cancellationToken) =>
            Task.FromResult(_values.GetValueOrDefault(key));

        public Task SetAsync(TenantId tenantId, Guid channelId, string key, string value, CancellationToken cancellationToken)
        {
            _values[key] = value;

            return Task.CompletedTask;
        }

        public Task RemoveAsync(TenantId tenantId, Guid channelId, string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);

            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileStorage(byte[] bytes) : IFileStorage
    {
        public Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(new MemoryStream(bytes));

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly List<(HttpStatusCode Status, string Body, byte[]? Bytes, string ContentType)> _responses = [];
        private int _index = -1;

        public List<CapturedRequest> Requests { get; } = [];

        public void EnqueueJson(HttpStatusCode status, string body) =>
            _responses.Add((status, body, null, "application/json"));

        public void EnqueueBytes(HttpStatusCode status, byte[] bytes, string contentType) =>
            _responses.Add((status, string.Empty, bytes, contentType));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));

            _index = Math.Min(_index + 1, _responses.Count - 1);

            var (status, responseBody, bytes, contentType) = _responses[_index];

            var content = bytes is null
                ? new StringContent(responseBody, Encoding.UTF8, contentType)
                : new ByteArrayContent(bytes);

            if (bytes is not null)
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            }

            return new HttpResponseMessage(status) { Content = content };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);
}
