using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.BuildingBlocks.Infrastructure;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Infrastructure.Adapters.WhatsApp;

namespace Wasla.UnitTests;

/// <summary>Fixture replay tests for the WhatsApp Cloud API adapter (no live provider calls).</summary>
public sealed class WhatsAppAdapterTests
{
    private const string AppSecret = "unit-app-secret";
    private const string AccessToken = "unit-access-token";
    private const string PhoneNumberId = "1234567890";
    private const string VerifyToken = "unit-verify-token";

    private static readonly Guid ChannelId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    // ------------------------------------------------------------- webhook verify --

    [Fact]
    public async Task Verify_accepts_valid_signature()
    {
        var body = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
        var adapter = CreateAdapter(new FakeHttpHandler());
        var context = PostContext(body, SignatureFor(body));

        var result = await adapter.VerifyWebhookAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Verify_rejects_tampered_body_and_missing_header()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var original = Encoding.UTF8.GetBytes("{\"amount\":1}");
        var tampered = Encoding.UTF8.GetBytes("{\"amount\":2}");

        var tamperedResult = await adapter.VerifyWebhookAsync(PostContext(tampered, SignatureFor(original)), CancellationToken.None);
        Assert.False(tamperedResult.IsValid);

        var missingResult = await adapter.VerifyWebhookAsync(PostContext(original, signature: null), CancellationToken.None);
        Assert.False(missingResult.IsValid);
    }

    [Fact]
    public async Task Verify_subscription_returns_challenge_only_for_correct_token()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var query = new Dictionary<string, string>
        {
            ["hub.mode"] = "subscribe",
            ["hub.verify_token"] = VerifyToken,
            ["hub.challenge"] = "challenge-123",
        };

        var valid = await adapter.VerifyWebhookAsync(GetContext(query), CancellationToken.None);
        Assert.True(valid.IsValid);
        Assert.Equal("challenge-123", valid.Challenge);

        query["hub.verify_token"] = "wrong";

        var invalid = await adapter.VerifyWebhookAsync(GetContext(query), CancellationToken.None);
        Assert.False(invalid.IsValid);
    }

    // ---------------------------------------------------------------- normalize --

    [Fact]
    public async Task Normalize_maps_text_message_with_contact_display_name()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var payload = """
            {"object":"whatsapp_business_account","entry":[{"id":"WABA","changes":[{"field":"messages","value":{
              "messaging_product":"whatsapp",
              "metadata":{"display_phone_number":"15551234567","phone_number_id":"1234567890"},
              "contacts":[{"profile":{"name":"Hassan Ali"},"wa_id":"963777000111"}],
              "messages":[{"from":"963777000111","id":"wamid.IN-1","timestamp":"1730000000","type":"text","text":{"body":"Hello there"},"context":{"id":"wamid.REPLY-0"}}]
            }}]}]}
            """;

        var events = await adapter.NormalizeInboundAsync(PostContext(Encoding.UTF8.GetBytes(payload), signature: null), CancellationToken.None);

        var message = Assert.IsType<InboundMessageEvent>(Assert.Single(events));
        Assert.Equal("wamid.IN-1", message.ProviderMessageId);
        Assert.Equal("963777000111", message.ExternalCustomerId);
        Assert.Equal("Hassan Ali", message.CustomerDisplayName);
        Assert.Equal(MessageType.Text, message.Type);
        Assert.Equal("Hello there", message.Body);
        Assert.Equal("wamid.REPLY-0", message.ReplyToProviderMessageId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1730000000), message.SentAt);
        Assert.Contains("schemaVersion", message.ProviderMetadata);
    }

    [Fact]
    public async Task Normalize_maps_media_message_to_type_and_media_reference()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"1234567890"},
              "messages":[{"from":"963777000222","id":"wamid.IN-IMG","timestamp":"1730000100","type":"image",
                "image":{"id":"MEDIA-9","mime_type":"image/jpeg","caption":"Look at this"}}]
            }}]}]}
            """;

        var events = await adapter.NormalizeInboundAsync(PostContext(Encoding.UTF8.GetBytes(payload), signature: null), CancellationToken.None);

        var message = Assert.IsType<InboundMessageEvent>(Assert.Single(events));
        Assert.Equal(MessageType.Image, message.Type);
        Assert.Equal("Look at this", message.Body);
        var media = Assert.Single(message.Media);
        Assert.Equal("MEDIA-9", media.ProviderMediaId);
        Assert.Equal("image/jpeg", media.MimeType);
    }

    [Fact]
    public async Task Normalize_maps_status_updates_including_failure()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var payload = """
            {"entry":[{"changes":[{"value":{
              "metadata":{"phone_number_id":"1234567890"},
              "statuses":[
                {"id":"wamid.OUT-A","status":"sent","timestamp":"1730000200"},
                {"id":"wamid.OUT-A","status":"delivered","timestamp":"1730000201"},
                {"id":"wamid.OUT-A","status":"read","timestamp":"1730000202"},
                {"id":"wamid.OUT-B","status":"failed","timestamp":"1730000203","errors":[{"code":131047,"title":"Re-engagement message"}]}
              ]
            }}]}]}
            """;

        var events = await adapter.NormalizeInboundAsync(PostContext(Encoding.UTF8.GetBytes(payload), signature: null), CancellationToken.None);

        Assert.Equal(4, events.Count);
        Assert.Collection(
            events.Cast<MessageStatusUpdateEvent>(),
            sent => Assert.Equal(MessageStatusUpdateKind.Sent, sent.Kind),
            delivered => Assert.Equal(MessageStatusUpdateKind.Delivered, delivered.Kind),
            read => Assert.Equal(MessageStatusUpdateKind.Read, read.Kind),
            failed =>
            {
                Assert.Equal(MessageStatusUpdateKind.Failed, failed.Kind);
                Assert.Equal("Re-engagement message", failed.FailureReason);
            });
    }

    [Fact]
    public async Task Normalize_returns_empty_for_unparseable_signed_payload()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());
        var events = await adapter.NormalizeInboundAsync(
            PostContext(Encoding.UTF8.GetBytes("this is not json"), signature: null),
            CancellationToken.None);

        Assert.Empty(events);
    }

    // ----------------------------------------------------------------- outbound --

    [Fact]
    public async Task SendText_posts_expected_payload_and_reads_message_id()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"messaging_product":"whatsapp","messages":[{"id":"wamid.OUT-1"}]}""");
        var adapter = CreateAdapter(handler);

        var result = await adapter.SendMessageAsync(
            new ChannelOutboundMessage(TenantId.New(), ChannelId, Guid.NewGuid(), "963777000555", "Hi from Wasla"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.OUT-1", result.ProviderMessageId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains($"/v21.0/{PhoneNumberId}/messages", request.Uri.ToString());
        Assert.Equal("Bearer " + AccessToken, request.Authorization);
        Assert.Contains("\"to\":\"963777000555\"", request.Body);
        Assert.Contains("Hi from Wasla", request.Body);
        Assert.Contains("\"messaging_product\":\"whatsapp\"", request.Body);
    }

    [Fact]
    public async Task SendText_maps_rate_limit_transient_and_rejection()
    {
        var rateLimited = await SendWithStatuses(HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests);
        Assert.Equal(ChannelSendFailureKind.RateLimited, rateLimited.FailureKind);

        var transient = await SendWithStatuses(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError);
        Assert.Equal(ChannelSendFailureKind.Transient, transient.FailureKind);

        var rejected = await SendWithStatuses(
            HttpStatusCode.BadRequest,
            body: """{"error":{"code":131047,"message":"More than 24 hours have passed"}}""");
        Assert.Equal(ChannelSendFailureKind.Rejected, rejected.FailureKind);
        Assert.Contains("131047", rejected.FailureReason);
    }

    [Fact]
    public async Task SendMedia_uploads_then_sends_by_media_id()
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"id":"MEDIA-UP-1"}""");
        handler.Enqueue(HttpStatusCode.OK, """{"messages":[{"id":"wamid.OUT-MED"}]}""");
        var adapter = CreateAdapter(handler, new FakeFileStorage([1, 2, 3, 4]));

        var result = await adapter.SendMediaAsync(
            new ChannelOutboundMediaMessage(
                TenantId.New(),
                ChannelId,
                Guid.NewGuid(),
                "963777000555",
                MessageType.Image,
                "tenant/x/media.png",
                "image/png",
                "media.png",
                4,
                "A caption"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("wamid.OUT-MED", result.ProviderMessageId);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains($"/v21.0/{PhoneNumberId}/media", handler.Requests[0].Uri.ToString());
        Assert.Contains("media.png", handler.Requests[0].Body);
        Assert.Contains($"/v21.0/{PhoneNumberId}/messages", handler.Requests[1].Uri.ToString());
        Assert.Contains("\"id\":\"MEDIA-UP-1\"", handler.Requests[1].Body);
        Assert.Contains("A caption", handler.Requests[1].Body);
    }

    [Fact]
    public async Task SendMedia_rejects_unsupported_types_locally()
    {
        var adapter = CreateAdapter(new FakeHttpHandler());

        var result = await adapter.SendMediaAsync(
            new ChannelOutboundMediaMessage(
                TenantId.New(), ChannelId, Guid.NewGuid(), "963", MessageType.Sticker,
                "key", "image/webp", "sticker.webp", 1, null),
            CancellationToken.None);

        Assert.Equal(ChannelSendFailureKind.Invalid, result.FailureKind);
    }

    // ------------------------------------------------------------------- fakes --

    private static async Task<ChannelSendResult> SendWithStatuses(HttpStatusCode status, HttpStatusCode? retryStatus = null, string? body = null)
    {
        var handler = new FakeHttpHandler();
        handler.Enqueue(status, body ?? """{"error":{"code":1,"message":"provider error"}}""");

        if (retryStatus is not null)
        {
            handler.Enqueue(retryStatus.Value, body ?? """{"error":{"code":1,"message":"provider error"}}""");
        }

        var adapter = CreateAdapter(handler);

        return await adapter.SendMessageAsync(
            new ChannelOutboundMessage(TenantId.New(), ChannelId, Guid.NewGuid(), "963777000555", "text"),
            CancellationToken.None);
    }

    private static WhatsAppCloudAdapter CreateAdapter(FakeHttpHandler handler, FakeFileStorage? storage = null, FakeCredentialStore? credentials = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.test/"),
        };

        return new WhatsAppCloudAdapter(
            httpClient,
            Options.Create(new WhatsAppOptions { BaseUrl = "https://graph.test", ApiVersion = "v21.0" }),
            credentials ?? new FakeCredentialStore(),
            storage ?? new FakeFileStorage([]),
            new SystemClock());
    }

    private static ChannelWebhookContext PostContext(byte[] body, string? signature) =>
        new(
            ChannelId,
            TenantId.New(),
            "POST",
            signature is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["X-Hub-Signature-256"] = signature },
            new Dictionary<string, string>(),
            body,
            DateTimeOffset.UtcNow);

    private static ChannelWebhookContext GetContext(Dictionary<string, string> query) =>
        new(ChannelId, TenantId.New(), "GET", new Dictionary<string, string>(), query, [], DateTimeOffset.UtcNow);

    private static string SignatureFor(byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AppSecret));

        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    private sealed class FakeCredentialStore : IChannelCredentialStore
    {
        private readonly Dictionary<string, string> _values = new()
        {
            [ChannelCredentialKeys.AccessToken] = AccessToken,
            [ChannelCredentialKeys.AppSecret] = AppSecret,
            [ChannelCredentialKeys.VerifyToken] = VerifyToken,
            [ChannelCredentialKeys.PhoneNumberId] = PhoneNumberId,
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
        private readonly List<(HttpStatusCode Status, string Body)> _responses = [];
        private int _index = -1;

        public List<CapturedRequest> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, string body) => _responses.Add((status, body));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                body,
                request.Headers.Authorization?.ToString()));

            _index = Math.Min(_index + 1, _responses.Count - 1);

            var (status, responseBody) = _responses[_index];

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body, string? Authorization);
}
