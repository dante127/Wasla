using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Wasla.IntegrationTests;

/// <summary>
/// Hardening acceptance: concurrency storms (webhook burst, send burst), transient
/// failure retry behavior, poison-payload tolerance and security headers.
/// </summary>
[Collection("api")]
public sealed class HardeningTests(IntegrationEnvironment environment)
{
    [Fact]
    public async Task Security_headers_are_present_on_responses()
    {
        var client = environment.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions));
        Assert.Contains("nosniff", contentTypeOptions!);
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").First());
    }

    [Fact]
    public async Task Inbox_storm_is_processed_exactly_once()
    {
        var client = environment.CreateClient();
        var chatId = "777500100";
        var payloads = Enumerable.Range(0, 50)
            .Select(index => TextUpdatePayload(chatId, $"Storm message {index}", 500100L + index))
            .ToList();

        var responses = await Task.WhenAll(payloads.Select(payload => PostTelegramAsync(client, payload)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        Assert.True(await environment.ProcessInboxAsync() >= 50);
        await DrainInboxAsync();

        using var owner = await CreateOwnerClientAsync();
        var conversationId = await FindConversationIdAsync(owner, chatId);
        Assert.Equal(50, await CountMessagesAsync(owner, conversationId));

        // Replay storm: every delivery is acknowledged but nothing reprocesses.
        var replays = await Task.WhenAll(payloads.Select(payload => PostTelegramAsync(client, payload)));
        Assert.All(replays, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(0, await environment.ProcessInboxAsync());
        Assert.Equal(50, await CountMessagesAsync(owner, conversationId));
    }

    [Fact]
    public async Task Send_burst_dispatches_all_messages()
    {
        using var owner = await CreateOwnerClientAsync();

        await DrainOutboxAsync();

        var sink = environment.Services.GetRequiredService<TestOutboundSink>();
        var baseline = sink.Messages.Count;

        var responses = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            owner.PostAsJsonAsync(
                $"/api/v1/conversations/{environment.AlphaConversationId}/messages",
                new { type = "Text", body = $"Burst message {index}" })));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        await DrainOutboxAsync();

        var captured = sink.Messages.Count - baseline;
        Assert.True(captured >= 40, $"expected >= 40 captured sends, got {captured}");
    }

    [Fact]
    public async Task Transient_failures_are_retried_then_delivered()
    {
        using var owner = await CreateOwnerClientAsync();

        await DrainOutboxAsync();

        var sink = environment.Services.GetRequiredService<TestOutboundSink>();
        sink.FailNext(1);

        var send = await owner.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/messages",
            new { type = "Text", body = "Retry me" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var messageId = (await send.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Attempt 1: transient failure → rescheduled, message still Pending.
        Assert.True(await environment.DispatchOutboxAsync() >= 1);
        var afterFirst = await GetMessageAsync(owner, environment.AlphaConversationId, messageId);
        Assert.Equal("Pending", afterFirst.GetProperty("status").GetString());

        // Nudge the retry clock and let attempt 2 succeed.
        using (var scope = environment.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Wasla.Messages.Infrastructure.MessagesDbContext>();
            var row = db.OutboxMessages
                .Where(outbox => outbox.Status == Wasla.Messages.Domain.OutboxMessageStatus.Pending)
                .OrderByDescending(outbox => outbox.CreatedAt)
                .First();
            db.Entry(row).Property("NextAttemptAt").CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        Assert.True(await environment.DispatchOutboxAsync() >= 1);

        var afterSecond = await GetMessageAsync(owner, environment.AlphaConversationId, messageId);
        Assert.Equal("Sent", afterSecond.GetProperty("status").GetString());
        Assert.StartsWith("wamid.TEST-", afterSecond.GetProperty("providerMessageId").GetString());
    }

    [Fact]
    public async Task Signed_garbage_payloads_are_skipped_without_side_effects()
    {
        var client = environment.CreateClient();

        var response = await PostTelegramAsync(client, "this is definitely not json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, await environment.ProcessInboxAsync());
        Assert.Equal(0, await environment.ProcessInboxAsync());
    }

    // ------------------------------------------------------------------ helpers --

    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    private async Task DrainInboxAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            if (await environment.ProcessInboxAsync() == 0)
            {
                return;
            }
        }
    }

    private async Task DrainOutboxAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            if (await environment.DispatchOutboxAsync() == 0)
            {
                return;
            }
        }
    }

    private Task<HttpResponseMessage> PostTelegramAsync(HttpClient client, string payload)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/webhooks/telegram/{environment.AlphaTelegramChannelId}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", IntegrationEnvironment.TelegramWebhookSecret);

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
                from = new { id = long.Parse(chatId), first_name = "Storm", last_name = "Tester" },
                text,
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    private static async Task<Guid> FindConversationIdAsync(HttpClient client, string searchTerm)
    {
        var customer = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers?q={searchTerm}");
        var customerId = customer.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/conversations?limit=50");

        return page.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("customer").GetProperty("id").GetGuid() == customerId)
            .GetProperty("id").GetGuid();
    }

    private static async Task<int> CountMessagesAsync(HttpClient client, Guid conversationId)
    {
        var total = 0;
        string? cursor = null;

        for (var page = 0; page < 20; page++)
        {
            var url = $"/api/v1/conversations/{conversationId}/messages?limit=100";

            if (cursor is not null)
            {
                url += $"&before={Uri.EscapeDataString(cursor)}";
            }

            var response = await client.GetFromJsonAsync<JsonElement>(url);
            total += response.GetProperty("items").EnumerateArray().Count();

            var next = response.GetProperty("nextCursor");

            cursor = next.ValueKind == JsonValueKind.Null ? null : next.GetString();

            if (cursor is null)
            {
                break;
            }
        }

        return total;
    }

    private static async Task<JsonElement> GetMessageAsync(HttpClient client, Guid conversationId, Guid messageId)
    {
        string? cursor = null;

        for (var page = 0; page < 20; page++)
        {
            var url = $"/api/v1/conversations/{conversationId}/messages?limit=100";

            if (cursor is not null)
            {
                url += $"&before={Uri.EscapeDataString(cursor)}";
            }

            var response = await client.GetFromJsonAsync<JsonElement>(url);

            foreach (var item in response.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("id").GetGuid() == messageId)
                {
                    return item.Clone();
                }
            }

            var next = response.GetProperty("nextCursor");

            cursor = next.ValueKind == JsonValueKind.Null ? null : next.GetString();

            if (cursor is null)
            {
                break;
            }
        }

        throw new InvalidOperationException($"Message {messageId} was not found.");
    }
}
