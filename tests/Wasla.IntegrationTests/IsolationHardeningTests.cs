using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wasla.IntegrationTests;

/// <summary>
/// Isolation hardening: negative-path probes for cross-tenant references that were not
/// covered by the per-module isolation suites (quick-reply render, conversation
/// creation inputs, assignment targets, media attachment).
/// </summary>
[Collection("api")]
public sealed class IsolationHardeningTests(IntegrationEnvironment environment)
{
    [Fact]
    public async Task Quick_reply_render_cannot_read_another_tenants_customer()
    {
        using var owner = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);

        var quickReplies = await owner.GetFromJsonAsync<JsonElement>("/api/v1/quick-replies");
        var welcomeId = quickReplies.EnumerateArray()
            .First(item => item.GetProperty("key").GetString() == "welcome")
            .GetProperty("id").GetGuid();

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/quick-replies/{welcomeId}/render",
            new { customerId = environment.BetaCustomerId });

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Zed Beta", body);
    }

    [Fact]
    public async Task Conversation_creation_validates_customer_and_channel_tenancy()
    {
        using var owner = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);

        var foreignCustomer = await owner.PostAsJsonAsync(
            "/api/v1/conversations",
            new { customerId = environment.BetaCustomerId, channelId = environment.AlphaWhatsAppChannelId });
        Assert.NotEqual(HttpStatusCode.OK, foreignCustomer.StatusCode);

        var foreignChannel = await owner.PostAsJsonAsync(
            "/api/v1/conversations",
            new { customerId = environment.AlphaCustomerId, channelId = environment.BetaWhatsAppChannelId });
        Assert.NotEqual(HttpStatusCode.OK, foreignChannel.StatusCode);
    }

    [Fact]
    public async Task Assignment_cannot_target_another_tenants_user()
    {
        using var owner = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/assign",
            new { userId = environment.BetaOwnerUserId });

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Message_send_cannot_attach_another_tenants_media()
    {
        using var ownerA = await CreateClientAsync(IntegrationEnvironment.OwnerAEmail);

        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "pixel.png");

        var upload = await ownerA.PostAsync("/api/v1/media", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var mediaId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var ownerB = await CreateClientAsync(IntegrationEnvironment.OwnerBEmail);

        var send = await ownerB.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.BetaConversationId}/messages",
            new { type = "Image", body = "cross-tenant media?", mediaFileIds = new[] { mediaId } });

        Assert.NotEqual(HttpStatusCode.OK, send.StatusCode);
    }

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }
}
