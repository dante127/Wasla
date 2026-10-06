using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Domain;

/// <summary>A tenant-scoped role: a named bundle of permissions from the closed catalog.</summary>
public sealed class Role : AggregateRoot<RoleId>, ITenantOwned
{
    private Role()
    {
        Name = string.Empty;
    }

    private Role(RoleId id, TenantId tenantId, string name, IEnumerable<string> permissions, bool isSystem)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        IsSystem = isSystem;
        Permissions = permissions.Distinct(StringComparer.Ordinal).ToList();
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public bool IsSystem { get; private set; }

    public List<string> Permissions { get; private set; } = [];

    public static Role Create(TenantId tenantId, string name, IEnumerable<string> permissions, bool isSystem)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name is required.", nameof(name));
        }

        var permissionList = permissions.ToList();
        var unknown = permissionList.Where(permission => !PermissionCatalog.IsKnown(permission)).ToList();

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Unknown permission codes: {string.Join(", ", unknown)}.",
                nameof(permissions));
        }

        return new Role(RoleId.New(), tenantId, name.Trim(), permissionList, isSystem);
    }

    public void SetPermissions(IEnumerable<string> permissions)
    {
        var permissionList = permissions.ToList();
        var unknown = permissionList.Where(permission => !PermissionCatalog.IsKnown(permission)).ToList();

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Unknown permission codes: {string.Join(", ", unknown)}.",
                nameof(permissions));
        }

        Permissions = permissionList.Distinct(StringComparer.Ordinal).ToList();
    }
}
