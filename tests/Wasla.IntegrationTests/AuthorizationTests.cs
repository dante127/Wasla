using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wasla.IntegrationTests;

[Collection("api")]
public sealed class AuthorizationTests(IntegrationEnvironment environment)
{
    [Fact]
    public async Task Anonymous_request_to_users_returns_401()
    {
        using var client = environment.CreateClient();

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Agent_is_forbidden_from_reading_users()
    {
        using var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.AgentAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Agent_token_does_not_contain_user_read_permission()
    {
        using var client = environment.CreateClient();

        var auth = await environment.LoginAsync(client, IntegrationEnvironment.AgentAEmail);

        Assert.DoesNotContain("user.read", auth.Permissions);
        Assert.Contains("conversation.read", auth.Permissions);
    }

    [Fact]
    public async Task Owner_can_read_users_of_their_tenant()
    {
        using var client = environment.CreateClient();
        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<JsonElement>();
        var emails = users.EnumerateArray().Select(user => user.GetProperty("email").GetString()).ToList();

        Assert.Contains(IntegrationEnvironment.OwnerAEmail, emails);
        Assert.Contains(IntegrationEnvironment.AgentAEmail, emails);
    }

    [Fact]
    public async Task Owner_token_contains_infrastructure_permissions()
    {
        using var client = environment.CreateClient();

        var auth = await environment.LoginAsync(client, IntegrationEnvironment.OwnerAEmail);

        Assert.Contains("user.read", auth.Permissions);
        Assert.Contains("settings.manage", auth.Permissions);
        Assert.Contains("billing.manage", auth.Permissions);
    }
}
