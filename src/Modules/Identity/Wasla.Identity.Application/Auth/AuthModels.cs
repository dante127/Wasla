using Wasla.BuildingBlocks.Application;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Identity.Application.Auth;

public sealed record LoginRequest(string Email, string Password, Guid? MembershipId);

public sealed record MembershipSummary(Guid MembershipId, Guid TenantId, string TenantName, string TenantSlug);

public sealed record UserSummary(Guid Id, string DisplayName, string Email);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    UserSummary User,
    TenantSummary Tenant,
    IReadOnlyCollection<string> Permissions);

public abstract record LoginOutcome
{
    private LoginOutcome()
    {
    }

    public sealed record Success(LoginResponse Response) : LoginOutcome;

    public sealed record SelectionRequired(IReadOnlyList<MembershipSummary> Memberships) : LoginOutcome;

    public sealed record Failure(Error Error) : LoginOutcome;
}

public sealed record RefreshRequest(string RefreshToken);

public sealed record RefreshResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken);

public sealed record MeResponse(
    UserSummary User,
    TenantSummary Tenant,
    IReadOnlyCollection<string> Permissions,
    Guid MembershipId);
