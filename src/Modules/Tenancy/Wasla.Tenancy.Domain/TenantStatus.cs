namespace Wasla.Tenancy.Domain;

/// <summary>Lifecycle status of a tenant.</summary>
public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
    Closed = 2,
}
