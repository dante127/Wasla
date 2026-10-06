using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wasla.IntegrationTests;

[Collection("api")]
public sealed class CustomerIsolationTests(IntegrationEnvironment environment)
{
    private async Task<HttpClient> CreateClientForAsync(string email)
    {
        var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    [Fact]
    public async Task Customer_search_is_tenant_scoped()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);
        var searchA = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/customers?q=Zed");
        Assert.Equal(0, searchA.GetProperty("totalCount").GetInt32());

        using var clientB = await CreateClientForAsync(IntegrationEnvironment.OwnerBEmail);
        var searchB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/customers?q=Alice");
        Assert.Equal(0, searchB.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Cannot_read_another_tenants_customer()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);

        var response = await clientA.GetAsync($"/api/v1/customers/{environment.BetaCustomerId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_update_another_tenants_customer()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);

        var response = await clientA.PatchAsJsonAsync(
            $"/api/v1/customers/{environment.BetaCustomerId}",
            new { displayName = "Hacked" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_add_note_or_tag_to_another_tenants_customer()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);

        var noteResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/customers/{environment.BetaCustomerId}/notes",
            new { body = "Cross-tenant note." });
        Assert.Equal(HttpStatusCode.NotFound, noteResponse.StatusCode);

        var tagResponse = await clientA.PostAsJsonAsync(
            $"/api/v1/customers/{environment.BetaCustomerId}/tags",
            new { tagIds = new[] { environment.AlphaVipTagId } });
        Assert.Equal(HttpStatusCode.NotFound, tagResponse.StatusCode);
    }

    [Fact]
    public async Task Tag_catalog_is_tenant_scoped()
    {
        using var clientA = await CreateClientForAsync(IntegrationEnvironment.OwnerAEmail);
        var tagsA = await clientA.GetFromJsonAsync<JsonElement>("/api/v1/tags");
        var namesA = tagsA.EnumerateArray().Select(tag => tag.GetProperty("name").GetString()).ToList();

        using var clientB = await CreateClientForAsync(IntegrationEnvironment.OwnerBEmail);
        var tagsB = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/tags");
        var namesB = tagsB.EnumerateArray().Select(tag => tag.GetProperty("name").GetString()).ToList();

        Assert.Contains("VIP", namesA);
        Assert.DoesNotContain("Beta Only", namesA);
        Assert.Contains("Beta Only", namesB);
        Assert.DoesNotContain("VIP", namesB);
    }
}
