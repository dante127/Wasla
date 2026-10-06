using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wasla.IntegrationTests;

[Collection("api")]
public sealed class CustomerTests(IntegrationEnvironment environment)
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    [Fact]
    public async Task Search_finds_customers_by_name_fragment_and_phone()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var byName = await client.GetFromJsonAsync<JsonElement>("/api/v1/customers?q=alice");
        var names = byName.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("displayName").GetString()).ToList();
        Assert.Contains(IntegrationEnvironment.AlphaCustomerName, names);

        var byPhone = await client.GetFromJsonAsync<JsonElement>("/api/v1/customers?q=963111");
        Assert.True(byPhone.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Customer_detail_returns_identities_contacts_and_tags()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{environment.AlphaCustomerId}");

        Assert.Equal(IntegrationEnvironment.AlphaCustomerName, detail.GetProperty("displayName").GetString());
        Assert.True(detail.GetProperty("identities").GetArrayLength() >= 1);
        Assert.True(detail.GetProperty("contacts").GetArrayLength() >= 1);
        Assert.Contains(
            detail.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()),
            name => name == "VIP");
    }

    [Fact]
    public async Task Create_customer_adds_contact_and_is_searchable()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            displayName = "Carol New",
            phone = "+963555555555",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var search = await client.GetFromJsonAsync<JsonElement>("/api/v1/customers?q=Carol");
        var carol = search.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("displayName").GetString() == "Carol New");

        var detail = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{carol.GetProperty("id").GetGuid()}");
        Assert.Contains(
            detail.GetProperty("contacts").EnumerateArray(),
            contact => contact.GetProperty("type").GetString() == "Phone");
    }

    [Fact]
    public async Task Notes_appear_on_the_customer_timeline()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var noteResponse = await client.PostAsJsonAsync(
            $"/api/v1/customers/{environment.AlphaCustomerId}/notes",
            new { body = "Called about the invoice." });
        Assert.Equal(HttpStatusCode.OK, noteResponse.StatusCode);

        var timeline = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{environment.AlphaCustomerId}/timeline");
        var types = timeline.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString()).ToList();

        Assert.Contains("note.added", types);
    }

    [Fact]
    public async Task Duplicate_identity_is_rejected_with_conflict()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/customers/{environment.AlphaCustomerId}/identities",
            new { channelType = "WhatsApp", externalId = IntegrationEnvironment.AlphaCustomerPhone });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Identity_linked_to_another_customer_is_rejected()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var search = await client.GetFromJsonAsync<JsonElement>("/api/v1/customers?q=bob");
        var bobId = search.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("displayName").GetString() == "Bob Smith")
            .GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/customers/{bobId}/identities",
            new { channelType = "WhatsApp", externalId = IntegrationEnvironment.AlphaCustomerPhone });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_channel_type_is_rejected_with_bad_request()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/customers/{environment.AlphaCustomerId}/identities",
            new { channelType = "Fax", externalId = "+963000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Add_and_remove_tag_roundtrip()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var search = await client.GetFromJsonAsync<JsonElement>("/api/v1/customers?q=bob");
        var bobId = search.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("displayName").GetString() == "Bob Smith")
            .GetProperty("id").GetGuid();

        var addResponse = await client.PostAsJsonAsync(
            $"/api/v1/customers/{bobId}/tags",
            new { tagIds = new[] { environment.AlphaVipTagId } });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var afterAdd = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers/{bobId}");
        Assert.Contains(
            afterAdd.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()),
            name => name == "VIP");

        var removeResponse = await client.DeleteAsync(
            $"/api/v1/customers/{bobId}/tags/{environment.AlphaVipTagId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var afterRemove = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers/{bobId}");
        Assert.DoesNotContain(
            afterRemove.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()),
            name => name == "VIP");
    }
}
