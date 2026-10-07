using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Wasla.IntegrationTests;

/// <summary>
/// Live-update tests: SignalR hub connections (real auth via the test host), tenant-scoped
/// broadcasts, typing relay and Redis-backed presence.
/// </summary>
[Collection("api")]
public sealed class RealTimeTests(IntegrationEnvironment environment)
{
    [Fact]
    public async Task New_message_events_reach_tenant_members()
    {
        var (connection, _) = await ConnectHubAsync(IntegrationEnvironment.OwnerAEmail);
        await using var disposable = connection;

        var received = Capture(connection, "NewMessage");

        using var owner = await CreateOwnerClientAsync();
        var response = await owner.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/messages",
            new { type = "Text", body = "Realtime ping" });
        Assert.True(response.IsSuccessStatusCode);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(environment.AlphaConversationId, payload.GetProperty("conversationId").GetGuid());
        Assert.Equal("Outbound", payload.GetProperty("direction").GetString());
        Assert.Equal("Realtime ping", payload.GetProperty("preview").GetString());
    }

    [Fact]
    public async Task Events_do_not_cross_tenant_boundaries()
    {
        var (connectionB, _) = await ConnectHubAsync(IntegrationEnvironment.OwnerBEmail);
        await using var disposable = connectionB;

        var received = Capture(connectionB, "NewMessage");

        using var owner = await CreateOwnerClientAsync();
        var response = await owner.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/messages",
            new { type = "Text", body = "Tenant A only" });
        Assert.True(response.IsSuccessStatusCode);

        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        Assert.False(received.Task.IsCompleted);
    }

    [Fact]
    public async Task Assignment_events_are_broadcast()
    {
        var (connection, _) = await ConnectHubAsync(IntegrationEnvironment.OwnerAEmail);
        await using var disposable = connection;

        var received = Capture(connection, "ConversationAssigned");

        using var owner = await CreateOwnerClientAsync();
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var myUserId = me.GetProperty("user").GetProperty("id").GetGuid();

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/assign",
            new { userId = myUserId });
        Assert.True(response.IsSuccessStatusCode);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(environment.AlphaConversationId, payload.GetProperty("conversationId").GetGuid());
        Assert.Equal(myUserId, payload.GetProperty("assignedUserId").GetGuid());
    }

    [Fact]
    public async Task Typing_is_relayed_to_others_but_not_the_sender()
    {
        var (sender, _) = await ConnectHubAsync(IntegrationEnvironment.OwnerAEmail);
        await using var senderDisposable = sender;

        var (listener, _) = await ConnectHubAsync(IntegrationEnvironment.AgentAEmail);
        await using var listenerDisposable = listener;

        var senderTyping = Capture(sender, "Typing");
        var listenerTyping = Capture(listener, "Typing");

        using var owner = await CreateOwnerClientAsync();
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var myUserId = me.GetProperty("user").GetProperty("id").GetGuid();

        await sender.InvokeAsync("Typing", environment.AlphaConversationId);

        var payload = await listenerTyping.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(environment.AlphaConversationId, payload.GetProperty("conversationId").GetGuid());
        Assert.Equal(myUserId, payload.GetProperty("userId").GetGuid());

        await Task.Delay(TimeSpan.FromMilliseconds(800));
        Assert.False(senderTyping.Task.IsCompleted);
    }

    [Fact]
    public async Task Presence_is_tracked_in_redis()
    {
        using var owner = await CreateOwnerClientAsync();
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var myUserId = me.GetProperty("user").GetProperty("id").GetGuid();

        var (connection, _) = await ConnectHubAsync(IntegrationEnvironment.OwnerAEmail);

        await Task.Delay(TimeSpan.FromMilliseconds(1000));

        var database = environment.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var key = $"presence:{environment.AlphaTenantId}";
        var value = await database.HashGetAsync(key, myUserId.ToString());
        Assert.True(value.HasValue && (long)value >= 1, $"expected presence >= 1, got {value}");

        await connection.DisposeAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(1000));

        var after = await database.HashGetAsync(key, myUserId.ToString());
        Assert.True(!after.HasValue || (long)after < (long)value);
    }

    // ------------------------------------------------------------------ helpers --

    private async Task<(HubConnection Connection, string AccessToken)> ConnectHubAsync(string email)
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, email);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost/hubs/inbox"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => environment.CreateServerHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(auth.AccessToken);
            })
            .Build();

        await connection.StartAsync();

        return (connection, auth.AccessToken);
    }

    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    private static TaskCompletionSource<JsonElement> Capture(HubConnection connection, string eventName)
    {
        var source = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<JsonElement>(eventName, element => source.TrySetResult(element.Clone()));

        return source;
    }
}
