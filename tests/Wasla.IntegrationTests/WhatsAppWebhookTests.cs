using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Wasla.IntegrationTests;

/// <summary>
/// End-to-end webhook pipeline tests: signed delivery → raw persist → inbox worker →
/// conversation/message rows; outbound send → outbox dispatcher (stubbed transport) →
/// status callbacks advance monotonically. No live Meta calls.
/// </summary>
[Collection("api")]
public sealed class WhatsAppWebhookTests(IntegrationEnvironment environment)
{
    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    [Fact]
    public async Task Subscription_verification_echoes_challenge_only_for_correct_token()
    {
        var client = environment.CreateClient();

        var ok = await client.GetAsync(
            $"/api/v1/webhooks/whatsapp/{environment.AlphaWhatsAppChannelId}" +
            $"?hub.mode=subscribe&hub.verify_token={IntegrationEnvironment.WhatsAppVerifyToken}&hub.challenge=challenge-77");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("challenge-77", await ok.Content.ReadAsStringAsync());

        var wrong = await client.GetAsync(
            $"/api/v1/webhooks/whatsapp/{environment.AlphaWhatsAppChannelId}" +
            "?hub.mode=subscribe&hub.verify_token=nope&hub.challenge=challenge-77");
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
    }

    [Fact]
    public async Task Signed_inbound_message_ingests_end_to_end_and_duplicates_are_single_effects()
    {
        var client = environment.CreateClient();
        var phone = "+963777100200";
        var text = "Where is my order #42?";
        var payload = InboundPayload(phone, text, "wamid.IN-E2E-1");

        var first = await PostSignedAsync(client, payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var processed = await environment.ProcessInboxAsync();
        Assert.True(processed >= 1);

        // Resolve the customer/conversation through the public API (tenant A owner).
        using var owner = await CreateOwnerClientAsync();
        var customerId = await FindCustomerIdAsync(owner, "963777100200");
        var conversationId = await FindConversationIdAsync(owner, customerId);

        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/conversations/{conversationId}");
        Assert.Equal(text, detail.GetProperty("lastMessagePreview").GetString());
        Assert.True(detail.GetProperty("unreadCount").GetInt32() >= 1);

        var messages = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/conversations/{conversationId}/messages");
        var items = messages.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("Inbound", items[0].GetProperty("direction").GetString());
        Assert.Equal(text, items[0].GetProperty("body").GetString());

        // Replay the exact same delivery: acknowledged, but no second effects.
        var duplicate = await PostSignedAsync(client, payload);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        var reprocessed = await environment.ProcessInboxAsync();
        Assert.Equal(0, reprocessed);

        var afterDuplicate = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/conversations/{conversationId}/messages");
        Assert.Single(afterDuplicate.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Outbound_dispatch_then_status_callbacks_advance_monotonically()
    {
        var client = environment.CreateClient();
        var phone = "+963777100300";
        await PostSignedAsync(client, InboundPayload(phone, "Can you check my delivery?", "wamid.IN-E2E-2"));
        await environment.ProcessInboxAsync();

        using var owner = await CreateOwnerClientAsync();
        var customerId = await FindCustomerIdAsync(owner, "963777100300");
        var conversationId = await FindConversationIdAsync(owner, customerId);

        // Send: message goes Pending with an outbox row.
        var sendResponse = await owner.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { type = "Text", body = "We are checking with the courier." });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var sent = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", sent.GetProperty("status").GetString());

        // Dispatch through the outbox processor (stubbed transport).
        var dispatched = await environment.DispatchOutboxAsync();
        Assert.True(dispatched >= 1);

        var afterDispatchFirst = await GetMessageAsync(owner, conversationId, sent.GetProperty("id").GetGuid());
        var statusNow = afterDispatchFirst.GetProperty("status").GetString();
        var sink = environment.Services.GetRequiredService<TestOutboundSink>();
        var captured = sink.Messages.FirstOrDefault(message => message.MessageId == sent.GetProperty("id").GetGuid());

        string debug;
        using (var scope = environment.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Wasla.Messages.Infrastructure.MessagesDbContext>();
            var rows = db.OutboxMessages
                .OrderByDescending(row => row.CreatedAt)
                .Take(3)
                .Select(row => row.Status + ":" + row.LastError)
                .ToList();

            debug = "status=" + statusNow + "; sinkCount=" + sink.Messages.Count + "; outbox=[" + string.Join(" | ", rows) + "]";
        }

        Assert.True(captured is not null, debug);
        Assert.Equal(phone, captured!.RecipientExternalId);
        Assert.Equal("We are checking with the courier.", captured.Body);

        var afterDispatch = await GetMessageAsync(owner, conversationId, sent.GetProperty("id").GetGuid());
        Assert.Equal("Sent", afterDispatch.GetProperty("status").GetString());
        var providerMessageId = afterDispatch.GetProperty("providerMessageId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(providerMessageId));

        // Provider callbacks: delivered → read; a late "sent" must not regress.
        await PostSignedAsync(client, StatusPayload(providerMessageId!, "delivered"));
        await environment.ProcessInboxAsync();
        Assert.Equal("Delivered", (await GetMessageAsync(owner, conversationId, sent.GetProperty("id").GetGuid())).GetProperty("status").GetString());

        await PostSignedAsync(client, StatusPayload(providerMessageId!, "read"));
        await environment.ProcessInboxAsync();
        Assert.Equal("Read", (await GetMessageAsync(owner, conversationId, sent.GetProperty("id").GetGuid())).GetProperty("status").GetString());

        await PostSignedAsync(client, StatusPayload(providerMessageId!, "sent"));
        await environment.ProcessInboxAsync();
        Assert.Equal("Read", (await GetMessageAsync(owner, conversationId, sent.GetProperty("id").GetGuid())).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Invalid_signature_is_rejected_and_nothing_is_persisted()
    {
        var client = environment.CreateClient();
        var payload = InboundPayload("+963777100400", "Should never appear", "wamid.IN-BAD");
        var bytes = Encoding.UTF8.GetBytes(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/webhooks/whatsapp/{environment.AlphaWhatsAppChannelId}")
        {
            Content = new ByteArrayContent(bytes),
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Hub-Signature-256", IntegrationEnvironment.ComputeSignature("wrong-secret", bytes));

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var processed = await environment.ProcessInboxAsync();
        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task Oversized_payload_is_rejected_with_413()
    {
        var client = environment.CreateClient();
        var padding = new string('x', 300 * 1024);
        var payload = InboundPayload("+963777100500", padding, "wamid.IN-BIG");

        var response = await PostSignedAsync(client, payload);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_channel_returns_404()
    {
        var client = environment.CreateClient();
        var bytes = Encoding.UTF8.GetBytes("{}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/webhooks/whatsapp/{Guid.NewGuid()}")
        {
            Content = new ByteArrayContent(bytes),
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Hub-Signature-256", IntegrationEnvironment.ComputeSignature(IntegrationEnvironment.WhatsAppAppSecret, bytes));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ helpers --

    private Task<HttpResponseMessage> PostSignedAsync(HttpClient client, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/webhooks/whatsapp/{environment.AlphaWhatsAppChannelId}")
        {
            Content = new ByteArrayContent(bytes),
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Hub-Signature-256", IntegrationEnvironment.ComputeSignature(IntegrationEnvironment.WhatsAppAppSecret, bytes));

        return client.SendAsync(request);
    }

    private static string InboundPayload(string from, string text, string messageId) =>
        $$$"""
        {"object":"whatsapp_business_account","entry":[{"id":"WABA-1","changes":[{"field":"messages","value":{
          "messaging_product":"whatsapp",
          "metadata":{"display_phone_number":"15551234567","phone_number_id":"963111111111"},
          "messages":[{"from":"{{{from}}}","id":"{{{messageId}}}","timestamp":"{{{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}","type":"text","text":{"body":{{{JsonSerializer.Serialize(text)}}}}}]
        }}]}]}
        """;

    private static string StatusPayload(string providerMessageId, string status) =>
        $$$"""
        {"object":"whatsapp_business_account","entry":[{"id":"WABA-1","changes":[{"field":"messages","value":{
          "messaging_product":"whatsapp",
          "metadata":{"display_phone_number":"15551234567","phone_number_id":"963111111111"},
          "statuses":[{"id":"{{{providerMessageId}}}","status":"{{{status}}}","timestamp":"{{{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}"}]
        }}]}]}
        """;

    private static async Task<Guid> FindCustomerIdAsync(HttpClient client, string searchTerm)
    {
        var results = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers?q={Uri.EscapeDataString(searchTerm)}");

        return results.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .First();
    }

    private static async Task<Guid> FindConversationIdAsync(HttpClient client, Guid customerId)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/conversations?limit=50");

        return page.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("customer").GetProperty("id").GetGuid() == customerId)
            .GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetMessageAsync(HttpClient client, Guid conversationId, Guid messageId)
    {
        var messages = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/conversations/{conversationId}/messages?limit=50");

        return messages.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("id").GetGuid() == messageId)
            .Clone();
    }}
