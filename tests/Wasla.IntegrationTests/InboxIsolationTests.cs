using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wasla.IntegrationTests;

[Collection("api")]
public sealed class InboxIsolationTests(IntegrationEnvironment environment)
{
    private async Task<HttpClient> CreateClientForAsync(string email)
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    [Fact]
    public async Task Conversations_list_is_tenant_scoped()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);
        var pageA = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/conversations");
        var idsA = pageA.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(environment.AlphaConversationId, idsA);
        Assert.DoesNotContain(environment.BetaConversationId, idsA);

        using var clientB = await CreateClientForAsync(IntegrationEnvironment.OwnerBEmail);
        var pageB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/conversations");
        var idsB = pageB.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(environment.BetaConversationId, idsB);
        Assert.DoesNotContain(environment.AlphaConversationId, idsB);
    }

    [Fact]
    public async Task Cannot_read_or_write_in_another_tenants_conversation()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);

        var readResponse = await clientA.GetAsync($"/api/v1/conversations/{environment.BetaConversationId}");
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        var sendResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.BetaConversationId}/messages",
            new { type = "Text", body = "Cross-tenant!" });
        Assert.Equal(HttpStatusCode.NotFound, sendResponse.StatusCode);

        var noteResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.BetaConversationId}/notes",
            new { body = "Cross-tenant note." });
        Assert.Equal(HttpStatusCode.NotFound, noteResponse.StatusCode);

        var resolveResponse = await clientA.PostAsync(
            $"/api/v1/conversations/{environment.BetaConversationId}/resolve", null);
        Assert.Equal(HttpStatusCode.NotFound, resolveResponse.StatusCode);
    }

    [Fact]
    public async Task Channels_are_tenant_scoped()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);
        var channelsA = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/channels");
        var namesA = channelsA.EnumerateArray().Select(channel => channel.GetProperty("displayName").GetString()).ToList();

        Assert.Contains("Alpha WhatsApp", namesA);
        Assert.DoesNotContain("Beta WhatsApp", namesA);

        using var clientB = await CreateClientForAsync(IntegrationEnvironment.OwnerBEmail);
        var channelsB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/channels");
        var namesB = channelsB.EnumerateArray().Select(channel => channel.GetProperty("displayName").GetString()).ToList();

        Assert.Contains("Beta WhatsApp", namesB);
        Assert.DoesNotContain("Alpha WhatsApp", namesB);
    }
}
