using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Wasla.IntegrationTests;

/// <summary>
/// Analytics acceptance: rollups match event-derived expected values (delta-based),
/// recompute is idempotent, reports are tenant-scoped and permission-guarded.
/// </summary>
[Collection("api")]
public sealed class AnalyticsTests(IntegrationEnvironment environment)
{
    [Fact]
    public async Task Overview_matches_event_derived_activity()
    {
        var owner = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);
        using var _ = owner;

        await environment.RecomputeAnalyticsAsync(environment.AlphaTenantId);
        var before = await GetOverviewAsync(owner);

        // Known activity: one inbound (webhook) + two outbound + resolve = +1 opened, +1 resolved, +1 in, +2 out.
        var chatId = "777300200";
        await PostTelegramAsync(environment.CreateClient(), TextUpdatePayload(chatId, "Analytics activity", 300201));
        Assert.True(await environment.ProcessInboxAsync() >= 1);

        var conversationId = await FindConversationIdAsync(owner, chatId);

        await SendTextAsync(owner, conversationId, "Reply one");
        await SendTextAsync(owner, conversationId, "Reply two");
        var resolve = await owner.PostAsync($"/api/v1/conversations/{conversationId}/resolve", null);
        Assert.True(resolve.IsSuccessStatusCode);

        await environment.RecomputeAnalyticsAsync(environment.AlphaTenantId);
        var after = await GetOverviewAsync(owner);

        Assert.Equal(before.GetProperty("conversationsOpened").GetInt32() + 1, after.GetProperty("conversationsOpened").GetInt32());
        Assert.Equal(before.GetProperty("conversationsResolved").GetInt32() + 1, after.GetProperty("conversationsResolved").GetInt32());
        Assert.Equal(before.GetProperty("messagesInbound").GetInt32() + 1, after.GetProperty("messagesInbound").GetInt32());
        Assert.Equal(before.GetProperty("messagesOutbound").GetInt32() + 2, after.GetProperty("messagesOutbound").GetInt32());
        Assert.True(after.GetProperty("avgFirstResponseMinutes").ValueKind != JsonValueKind.Null);

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var daily = after.GetProperty("daily").EnumerateArray().ToDictionary(point => point.GetProperty("date").GetString()!);
        var beforeDaily = before.GetProperty("daily").EnumerateArray().ToDictionary(point => point.GetProperty("date").GetString()!);

        Assert.True(daily.ContainsKey(today));
        var beforeToday = beforeDaily.TryGetValue(today, out var value) ? value : default;
        var afterToday = daily[today].Clone();

        Assert.Equal(1, Delta(beforeToday, afterToday, "conversationsOpened"));
        Assert.Equal(1, Delta(beforeToday, afterToday, "conversationsResolved"));
        Assert.Equal(1, Delta(beforeToday, afterToday, "messagesInbound"));
        Assert.Equal(2, Delta(beforeToday, afterToday, "messagesOutbound"));
    }

    [Fact]
    public async Task Recompute_is_idempotent()
    {
        var owner = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);
        using var _ = owner;

        await environment.RecomputeAnalyticsAsync(environment.AlphaTenantId);
        var first = await SnapshotAsync(owner);

        await environment.RecomputeAnalyticsAsync(environment.AlphaTenantId);
        var second = await SnapshotAsync(owner);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Agent_report_attributes_conversations_by_assignment()
    {
        var owner = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);
        using var _ = owner;

        var chatId = "777300300";
        await PostTelegramAsync(environment.CreateClient(), TextUpdatePayload(chatId, "Assign me", 300301));
        Assert.True(await environment.ProcessInboxAsync() >= 1);

        var conversationId = await FindConversationIdAsync(owner, chatId);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var myUserId = me.GetProperty("user").GetProperty("id").GetGuid();

        var assign = await owner.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/assign", new { userId = myUserId });
        Assert.True(assign.IsSuccessStatusCode);

        await environment.RecomputeAnalyticsAsync(environment.AlphaTenantId);

        var agents = await owner.GetFromJsonAsync<JsonElement>("/api/v1/analytics/agents");
        var mine = agents.EnumerateArray().First(item => item.GetProperty("userId").GetGuid() == myUserId);

        Assert.True(mine.GetProperty("assignedConversations").GetInt32() >= 1);
    }

    [Fact]
    public async Task Channel_report_resolves_names_and_volumes()
    {
        var owner = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);
        using var _ = owner;

        await environment.RecomputeAnalyticsAsync(environment.AlphaTenantId);

        var channels = await owner.GetFromJsonAsync<JsonElement>("/api/v1/analytics/channels");
        var whatsApp = channels.EnumerateArray()
            .First(item => item.GetProperty("channelId").GetGuid() == environment.AlphaWhatsAppChannelId);

        Assert.Equal("Alpha WhatsApp", whatsApp.GetProperty("channelName").GetString());
        Assert.True(whatsApp.GetProperty("conversationsOpened").GetInt32() >= 1);
        Assert.True(whatsApp.GetProperty("messagesInbound").GetInt32() >= 1);
    }

    [Fact]
    public async Task Reports_are_tenant_scoped()
    {
        var ownerA = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);
        using var _ = ownerA;
        var ownerB = await CreateClientAsync(IntegrationEnvironment.OwnerBEmail);
        using var __ = ownerB;

        await environment.RecomputeAnalyticsAsync(environment.AlphaTenantId);
        await environment.RecomputeAnalyticsAsync(environment.BetaTenantId);

        var alphaBefore = (await GetOverviewAsync(ownerA)).GetRawText();
        var betaBefore = await GetOverviewAsync(ownerB);

        // Beta-only activity: open a conversation for the beta customer on the beta channel.
        var create = await ownerB.PostAsJsonAsync(
            "/api/v1/conversations",
            new { customerId = environment.BetaCustomerId, channelId = environment.BetaWhatsAppChannelId });
        Assert.True(create.IsSuccessStatusCode);

        await environment.RecomputeAnalyticsAsync(environment.BetaTenantId);

        var betaAfter = await GetOverviewAsync(ownerB);
        Assert.Equal(
            betaBefore.GetProperty("conversationsOpened").GetInt32() + 1,
            betaAfter.GetProperty("conversationsOpened").GetInt32());

        // Alpha facts were untouched by the beta recompute.
        Assert.Equal(alphaBefore, (await GetOverviewAsync(ownerA)).GetRawText());

        // Beta reports never surface the alpha channel.
        var betaChannels = await ownerB.GetFromJsonAsync<JsonElement>("/api/v1/analytics/channels");
        Assert.DoesNotContain(
            betaChannels.EnumerateArray(),
            item => item.GetProperty("channelId").GetGuid() == environment.AlphaWhatsAppChannelId);
    }

    [Fact]
    public async Task Report_endpoints_enforce_permissions()
    {
        var anonymous = environment.CreateClient();
        var anonymousResponse = await anonymous.GetAsync("/api/v1/analytics/overview");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var agent = await CreateClientAsync(IntegrationEnvironment.AgentAEmail);
        using var _ = agent;
        var agentResponse = await agent.GetAsync("/api/v1/analytics/overview");
        Assert.Equal(HttpStatusCode.OK, agentResponse.StatusCode);
    }

    // ------------------------------------------------------------------ helpers --

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    private static async Task<JsonElement> GetOverviewAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/v1/analytics/overview");

    private static async Task<string> SnapshotAsync(HttpClient client)
    {
        var overview = (await GetOverviewAsync(client)).GetRawText();
        var agents = (await client.GetFromJsonAsync<JsonElement>("/api/v1/analytics/agents")).GetRawText();
        var channels = (await client.GetFromJsonAsync<JsonElement>("/api/v1/analytics/channels")).GetRawText();

        return overview + "|" + agents + "|" + channels;
    }

    private static int Delta(JsonElement before, JsonElement after, string property)
    {
        var beforeValue = before.ValueKind == JsonValueKind.Undefined ? 0 : before.GetProperty(property).GetInt32();

        return after.GetProperty(property).GetInt32() - beforeValue;
    }

    private static async Task SendTextAsync(HttpClient client, Guid conversationId, string body)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { type = "Text", body });

        Assert.True(response.IsSuccessStatusCode);
    }

    private async Task<Guid> FindConversationIdAsync(HttpClient client, string chatId)
    {
        var customer = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers?q={chatId}");
        var customerId = customer.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/conversations?limit=50");

        return page.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("customer").GetProperty("id").GetGuid() == customerId)
            .GetProperty("id").GetGuid();
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
                from = new { id = long.Parse(chatId), first_name = "Analytics", last_name = "Tester" },
                text,
            },
        };

        return JsonSerializer.Serialize(payload);
    }
}
