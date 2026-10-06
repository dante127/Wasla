using Wasla.Audit.Application;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Users;

/// <summary>Creates a user and their membership (with roles) in the current tenant.</summary>
public sealed class CreateUserHandler(
    ITenantContext tenantContext,
    IUserRepository users,
    IMembershipRepository memberships,
    IRoleRepository roles,
    IPasswordHasher passwordHasher,
    IAuditWriter audit,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<UserListItem>> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<UserListItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        EmailAddress email;

        try
        {
            email = EmailAddress.Create(request.Email);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<UserListItem>(new Error("users.email_invalid", exception.Message));
        }

        var roleIds = request.RoleIds.Distinct().Select(id => new RoleId(id)).ToList();

        if (roleIds.Count > 0)
        {
            var found = await roles.GetByIdsAsync(tenantId, roleIds, cancellationToken);

            if (found.Count != roleIds.Count)
            {
                return Result.Failure<UserListItem>(
                    new Error("roles.not_found", "One or more roles were not found."));
            }
        }

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<UserListItem>(
                new Error("users.email_conflict", "A user with this email already exists."));
        }

        var now = clock.UtcNow;
        var user = User.Register(email, request.DisplayName, passwordHasher.Hash(request.Password), now);
        await users.AddAsync(user, cancellationToken);

        var membership = Membership.Create(tenantId, user.Id, now);
        membership.ReplaceRoles(roleIds, now);
        await memberships.AddAsync(membership, cancellationToken);

        await audit.RecordAsync(
            new AuditRecord(tenantId, null, "users.user_created", "User", user.Id.ToString(), $"email={email.Value}"),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserListItem(
            user.Id.Value,
            user.DisplayName,
            user.Email.Value,
            user.Status.ToString(),
            roleIds.Select(id => id.Value).ToList(),
            membership.JoinedAt));
    }
}
