namespace Wasla.Identity.Domain;

/// <summary>Status of a user's membership within a tenant.</summary>
public enum MembershipStatus
{
    Invited = 0,
    Active = 1,
    Suspended = 2,
}
