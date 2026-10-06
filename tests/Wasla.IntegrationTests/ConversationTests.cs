using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wasla.IntegrationTests;

[Collection("api")]
public sealed class ConversationTests(IntegrationEnvironment environment)
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    [Fact]
    public async Task Conversations_list_is_shaped_with_customer_and_channel()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/conversations");
        var items = page.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);

        var alpha = items.First(item => item.GetProperty("id").GetGuid() == environment.AlphaConversationId);

        Assert.Equal(IntegrationEnvironment.AlphaCustomerName, alpha.GetProperty("customer").GetProperty("displayName").GetString());
        Assert.Equal("WhatsApp", alpha.GetProperty("channel").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Conversation_detail_includes_tags_and_timeline()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/conversations/{environment.AlphaConversationId}");

        Assert.Equal("Open", detail.GetProperty("status").GetString());
        Assert.Contains(
            detail.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()),
            name => name == "VIP");

        var timeline = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/conversations/{environment.AlphaConversationId}/timeline");
        Assert.True(timeline.GetProperty("totalCount").GetInt32() >= 2);
    }

    [Fact]
    public async Task Unread_and_tag_filters_work()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var unread = await client.GetFromJsonAsync<JsonElement>("/api/v1/conversations?unread=true");
        Assert.Contains(
            unread.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == environment.AlphaConversationId);

        var tagged = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/conversations?tagIds={environment.AlphaVipTagId}");
        Assert.Contains(
            tagged.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == environment.AlphaConversationId);
    }

    [Fact]
    public async Task Assign_resolve_and_reopen_flow()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var myUserId = me.GetProperty("user").GetProperty("id").GetGuid();

        var assignResponse = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/assign",
            new { userId = myUserId });
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assigned = await assignResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(myUserId, assigned.GetProperty("assignedUserId").GetGuid());

        var resolveResponse = await client.PostAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/resolve", null);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Resolved", resolved.GetProperty("status").GetString());

        var reopenResponse = await client.PostAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/reopen", null);
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        var reopened = await reopenResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Open", reopened.GetProperty("status").GetString());

        var closeFromOpen = await client.PostAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/close", null);
        Assert.Equal(HttpStatusCode.Conflict, closeFromOpen.StatusCode);
    }

    [Fact]
    public async Task Send_message_is_idempotent_and_updates_preview()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/conversations/{environment.AlphaConversationId}/messages");
        request.Headers.Add("Idempotency-Key", "itest-idem-1");
        request.Content = JsonContent.Create(new { type = "Text", body = "Thanks, on my way." });

        var first = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = firstBody.GetProperty("id").GetGuid();
        Assert.Equal("Pending", firstBody.GetProperty("status").GetString());

        var replay = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/conversations/{environment.AlphaConversationId}/messages");
        replay.Headers.Add("Idempotency-Key", "itest-idem-1");
        replay.Content = JsonContent.Create(new { type = "Text", body = "Different body ignored." });

        var second = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(messageId, secondBody.GetProperty("id").GetGuid());

        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/conversations/{environment.AlphaConversationId}");
        Assert.Equal("Thanks, on my way.", detail.GetProperty("lastMessagePreview").GetString());
    }

    [Fact]
    public async Task Messages_cursor_pagination_walks_backwards()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var firstPage = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/conversations/{environment.AlphaConversationId}/messages?limit=1");
        var firstItems = firstPage.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(firstItems);

        var nextCursor = firstPage.GetProperty("nextCursor").GetString();

        if (nextCursor is not null)
        {
            var secondPage = await client.GetFromJsonAsync<JsonElement>(
                $"/api/v1/conversations/{environment.AlphaConversationId}/messages?limit=10&before={Uri.EscapeDataString(nextCursor)}");
            var secondItems = secondPage.GetProperty("items").EnumerateArray().ToList();

            Assert.DoesNotContain(secondItems, item => item.GetProperty("id").GetGuid() == firstItems[0].GetProperty("id").GetGuid());
        }
    }

    [Fact]
    public async Task Quick_replies_list_and_render()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var items = await client.GetFromJsonAsync<JsonElement>("/api/v1/quick-replies");
        var welcome = items.EnumerateArray().First(item => item.GetProperty("key").GetString() == "welcome");

        var renderResponse = await client.PostAsJsonAsync(
            $"/api/v1/quick-replies/{welcome.GetProperty("id").GetGuid()}/render",
            new { customerId = environment.AlphaCustomerId });
        Assert.Equal(HttpStatusCode.OK, renderResponse.StatusCode);

        var rendered = await renderResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            $"Hello {IntegrationEnvironment.AlphaCustomerName}, how can we help you?",
            rendered.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Media_upload_validates_and_attaches_to_message()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "pixel.png");

        var upload = await client.PostAsync("/api/v1/media", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var mediaId = uploaded.GetProperty("id").GetGuid();
        Assert.Equal("image/png", uploaded.GetProperty("contentType").GetString());

        var sendResponse = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{environment.AlphaConversationId}/messages",
            new { type = "Image", body = "Look at this", mediaFileIds = new[] { mediaId } });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var message = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(message.GetProperty("attachments").EnumerateArray());

        using var badForm = new MultipartFormDataContent();
        var badContent = new ByteArrayContent([1, 2, 3]);
        badContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        badForm.Add(badContent, "file", "evil.html");

        var badUpload = await client.PostAsync("/api/v1/media", badForm);
        Assert.Equal(HttpStatusCode.BadRequest, badUpload.StatusCode);
    }
}
