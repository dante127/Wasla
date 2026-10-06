using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Domain;

/// <summary>A link entity assigning a role to a membership.</summary>
public sealed class MembershipRole
{
    private MembershipRole()
    {
    }

    public MembershipRole(MembershipId membershipId, RoleId roleId)
    {
        MembershipId = membershipId;
        RoleId = roleId;
    }

    public MembershipId MembershipId { get; private set; }

    public RoleId RoleId { get; private set; }
}

/// <summary>
/// Binds a user into a tenant with roles. Being a member is what makes a user's requests
/// tenant-scoped; roles are permission bundles evaluated by the authorization layer.
/// </summary>
public sealed class Membership : AggregateRoot<MembershipId>, ITenantOwned
{
    private Membership()
    {
    }

    private Membership(MembershipId id, TenantId tenantId, UserId userId, DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Status = MembershipStatus.Active;
        JoinedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public UserId UserId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public List<MembershipRole> Roles { get; private set; } = [];

    public static Membership Create(TenantId tenantId, UserId userId, DateTimeOffset now)
    {
        var membership = new Membership(MembershipId.New(), tenantId, userId, now);
        membership.RaiseDomainEvent(new MembershipCreated(Guid.NewGuid(), now, membership.Id, tenantId, userId));

        return membership;
    }

    public void Activate(DateTimeOffset now)
    {
        if (Status == MembershipStatus.Active)
        {
            return;
        }

        Status = MembershipStatus.Active;
        RaiseDomainEvent(new MembershipRolesChanged(Guid.NewGuid(), now, Id, TenantId, CurrentRoleIds));
    }

    public void Suspend(DateTimeOffset now)
    {
        if (Status == MembershipStatus.Suspended)
        {
            return;
        }

        Status = MembershipStatus.Suspended;
        RaiseDomainEvent(new MembershipRolesChanged(Guid.NewGuid(), now, Id, TenantId, CurrentRoleIds));
    }

    public void ReplaceRoles(IEnumerable<RoleId> roleIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(roleIds);

        var distinct = roleIds.Distinct().ToList();
        Roles.Clear();
        Roles.AddRange(distinct.Select(roleId => new MembershipRole(Id, roleId)));

        RaiseDomainEvent(new MembershipRolesChanged(Guid.NewGuid(), now, Id, TenantId, distinct));
    }

    private IReadOnlyList<RoleId> CurrentRoleIds => Roles.Select(role => role.RoleId).ToList();
}
