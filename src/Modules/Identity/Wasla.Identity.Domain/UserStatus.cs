namespace Wasla.Identity.Domain;

/// <summary>Lifecycle status of a user account (global identity).</summary>
public enum UserStatus
{
    Invited = 0,
    Active = 1,
    Disabled = 2,
}
