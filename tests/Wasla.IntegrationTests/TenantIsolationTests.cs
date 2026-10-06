using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wasla.IntegrationTests;

[Collection("api")]
public sealed class TenantIsolationTests(IntegrationEnvironment environment)
{
    [Fact]
    public async Task Users_list_is_scoped_to_the_current_tenant()
    {
        using var client = environment.CreateClient();
        var authA = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        var response = await client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<JsonElement>();
        var emails = users.EnumerateArray().Select(user => user.GetProperty("email").GetString()).ToList();

        Assert.Contains(IntegrationEnvironment.OwnerAEmail, emails);
        Assert.DoesNotContain(IntegrationEnvironment.OwnerBEmail, emails);
    }

    [Fact]
    public async Task Cannot_update_a_user_of_another_tenant()
    {
        using var client = environment.CreateClient();
        var authA = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/users/{environment.BetaOwnerUserId}",
            new { displayName = "Cross-tenant update" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_assign_roles_to_a_user_of_another_tenant()
    {
        using var client = environment.CreateClient();
        var authA = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/users/{environment.BetaOwnerUserId}/roles",
            new { roleIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Roles_are_tenant_scoped()
    {
        using var client = environment.CreateClient();

        var authA = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        var rolesA = await client.GetFromJsonAsync<JsonElement>("/api/v1/roles");
        var namesA = rolesA.EnumerateArray().Select(role => role.GetProperty("name").GetString()).ToList();

        var authB = await environment.LoginAsync(client, IntegrationEnvironment.OwnerBEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);
        var rolesB = await client.GetFromJsonAsync<JsonElement>("/api/v1/roles");
        var namesB = rolesB.EnumerateArray().Select(role => role.GetProperty("name").GetString()).ToList();

        Assert.DoesNotContain(IntegrationEnvironment.BetaOnlyRoleName, namesA);
        Assert.Contains(IntegrationEnvironment.BetaOnlyRoleName, namesB);
        Assert.Contains("Owner", namesA);
        Assert.Contains("Owner", namesB);
    }

    [Fact]
    public async Task Tenancy_current_returns_the_own_tenant()
    {
        using var client = environment.CreateClient();

        var authA = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        var tenantA = await client.GetFromJsonAsync<JsonElement>("/api/v1/tenancy/current");
        Assert.Equal("alpha", tenantA.GetProperty("slug").GetString());

        var authB = await environment.LoginAsync(client, IntegrationEnvironment.OwnerBEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);
        var tenantB = await client.GetFromJsonAsync<JsonElement>("/api/v1/tenancy/current");
        Assert.Equal("beta", tenantB.GetProperty("slug").GetString());
    }
}
