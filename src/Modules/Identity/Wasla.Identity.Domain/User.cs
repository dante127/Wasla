using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Domain;

/// <summary>
/// A user account. Users are global identities; tenant access is granted through memberships.
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    private User()
    {
        Email = EmailAddress.FromTrusted("unknown@invalid");
        DisplayName = string.Empty;
    }

    private User(UserId id, EmailAddress email, string displayName, string passwordHash, DateTimeOffset now)
        : base(id)
    {
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Status = UserStatus.Active;
        CreatedAt = now;
    }

    public EmailAddress Email { get; private set; }

    public string DisplayName { get; private set; }

    public string? PasswordHash { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public static User Register(EmailAddress email, string displayName, string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        var user = new User(UserId.New(), email, displayName.Trim(), passwordHash, now);
        user.RaiseDomainEvent(new UserRegistered(Guid.NewGuid(), now, user.Id, email.Value));

        return user;
    }

    public void Activate(DateTimeOffset now)
    {
        if (Status == UserStatus.Active)
        {
            return;
        }

        Status = UserStatus.Active;
        RaiseDomainEvent(new UserActivated(Guid.NewGuid(), now, Id));
    }

    public void Disable(DateTimeOffset now)
    {
        if (Status == UserStatus.Disabled)
        {
            return;
        }

        Status = UserStatus.Disabled;
        RaiseDomainEvent(new UserDisabled(Guid.NewGuid(), now, Id));
    }

    public void ChangeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
    }

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;
}
