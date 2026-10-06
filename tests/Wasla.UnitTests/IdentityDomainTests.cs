using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.UnitTests;

public sealed class IdentityDomainTests
{
    [Fact]
    public void User_register_normalizes_email_and_raises_event()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Register(EmailAddress.Create("  Owner@Example.COM "), "  Owner  ", "hash", now);

        Assert.Equal("owner@example.com", user.Email.Value);
        Assert.Equal("Owner", user.DisplayName);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Single(user.DomainEvents);
        Assert.IsType<UserRegistered>(user.DomainEvents.First());
    }

    [Fact]
    public void User_status_transitions_are_idempotent_and_audited()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Register(EmailAddress.Create("user@example.com"), "User", "hash", now);
        user.ClearDomainEvents();

        user.Disable(now);
        user.Disable(now);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Single(user.DomainEvents);

        user.Activate(now);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Membership_replace_roles_deduplicates()
    {
        var membership = Membership.Create(TenantId.New(), UserId.New(), DateTimeOffset.UtcNow);
        var role = RoleId.New();

        membership.ReplaceRoles([role, role], DateTimeOffset.UtcNow);

        Assert.Single(membership.Roles);
        Assert.Equal(role, membership.Roles[0].RoleId);
    }

    [Fact]
    public void Role_create_rejects_unknown_permissions()
    {
        Assert.Throws<ArgumentException>(() =>
            Role.Create(TenantId.New(), "Custom", ["not.a.permission"], isSystem: false));
    }

    [Fact]
    public void RefreshToken_lifecycle_is_enforced()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Issue(TenantId.New(), UserId.New(), "hash", now.AddDays(30), now);

        Assert.True(token.IsActive(now));
        Assert.False(token.IsActive(now.AddDays(31)));

        token.Revoke(now);
        Assert.False(token.IsActive(now));
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public void Seed_roles_match_the_permission_catalog_rules()
    {
        var ownerPermissions = SystemRoles.Definitions[SystemRoles.Owner];
        var adminPermissions = SystemRoles.Definitions[SystemRoles.Admin];

        Assert.Equal(PermissionCatalog.All.Count, ownerPermissions.Count);
        Assert.DoesNotContain(PermissionCatalog.Billing.Manage, adminPermissions);
        Assert.Contains(PermissionCatalog.Users.Read, adminPermissions);
    }
}
