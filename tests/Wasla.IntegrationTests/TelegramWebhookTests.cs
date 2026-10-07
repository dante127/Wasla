using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Wasla.IntegrationTests;

/// <summary>
/// End-to-end Telegram webhook tests: secret-token verification, inbound ingest with
/// dedupe, media download into storage (stubbed transport; real verification/normalization).
/// </summary>
[Collection("api")]
public sealed class TelegramWebhookTests(IntegrationEnvironment environment)
{
    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    [Fact]
    public async Task Secret_token_is_required_for_webhooks()
    {
        var client = environment.CreateClient();
        var payload = TextUpdatePayload("777200300", "Hello, I need help", 3001);

        var ok = await PostTelegramAsync(client, payload, IntegrationEnvironment.TelegramWebhookSecret);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var wrong = await PostTelegramAsync(client, TextUpdatePayload("777200301", "x", 3002), "wrong-secret");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var missing = await PostTelegramAsync(client, TextUpdatePayload("777200302", "x", 3003), secret: null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
    }

    [Fact]
    public async Task Inbound_message_ingests_end_to_end_and_duplicates_are_single_effects()
    {
        var client = environment.CreateClient();
        var text = "Can you check my subscription?";
        var payload = TextUpdatePayload("777200400", text, 4001);

        var first = await PostTelegramAsync(client, payload, IntegrationEnvironment.TelegramWebhookSecret);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var processed = await environment.ProcessInboxAsync();
        Assert.True(processed >= 1);

        using var owner = await CreateOwnerClientAsync();
        var customerId = await FindCustomerIdAsync(owner, "777200400");
        var conversationId = await FindConversationIdAsync(owner, customerId);

        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/conversations/{conversationId}");
        Assert.Equal(text, detail.GetProperty("lastMessagePreview").GetString());

        var messages = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/conversations/{conversationId}/messages");
        Assert.Single(messages.GetProperty("items").EnumerateArray());

        var duplicate = await PostTelegramAsync(client, payload, IntegrationEnvironment.TelegramWebhookSecret);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal("duplicate", await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(0, await environment.ProcessInboxAsync());
    }

    [Fact]
    public async Task Inbound_photo_downloads_media_into_storage()
    {
        var client = environment.CreateClient();
        var payload = PhotoUpdatePayload("777200500", 5001);

        var response = await PostTelegramAsync(client, payload, IntegrationEnvironment.TelegramWebhookSecret);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(await environment.ProcessInboxAsync() >= 1);

        using var owner = await CreateOwnerClientAsync();
        var customerId = await FindCustomerIdAsync(owner, "777200500");
        var conversationId = await FindConversationIdAsync(owner, customerId);

        var messages = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/conversations/{conversationId}/messages");
        var message = messages.GetProperty("items").EnumerateArray().First();
        Assert.Equal("Image", message.GetProperty("type").GetString());

        var attachments = message.GetProperty("attachments").EnumerateArray().ToList();
        var attachment = Assert.Single(attachments);
        Assert.Equal("photo.png", attachment.GetProperty("fileName").GetString());
        Assert.Equal("image/png", attachment.GetProperty("contentType").GetString());

        var sink = environment.Services.GetRequiredService<TestOutboundSink>();
        Assert.Contains(sink.MediaDownloads, download => download.ProviderMediaId == "TG-FILE-5001");
    }

    // ------------------------------------------------------------------ helpers --

    private Task<HttpResponseMessage> PostTelegramAsync(HttpClient client, string payload, string? secret)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/webhooks/telegram/{environment.AlphaTelegramChannelId}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (secret is not null)
        {
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        }

        return client.SendAsync(request);
    }

    private static string TextUpdatePayload(string chatId, string text, long updateId)
    {
        var payload = new
        {
            update_id = updateId,
            message = new
            {
                message_id = updateId,
                date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                chat = new { id = long.Parse(chatId), type = "private" },
                from = new { id = long.Parse(chatId), first_name = "Tariq", last_name = "Test" },
                text,
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string PhotoUpdatePayload(string chatId, long updateId)
    {
        var payload = new
        {
            update_id = updateId,
            message = new
            {
                message_id = updateId,
                date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                chat = new { id = long.Parse(chatId), type = "private" },
                from = new { id = long.Parse(chatId), first_name = "Photo", last_name = "Tester" },
                photo = new[] { new { file_id = "TG-FILE-5001", file_size = 1000 } },
                caption = "A photo",
            },
        };

        return JsonSerializer.Serialize(payload);
    }

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
}
