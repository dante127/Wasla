using Wasla.Audit.Application;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Users;

/// <summary>Updates a user's display name and/or account status (tenant-scoped).</summary>
public sealed class UpdateUserHandler(
    ITenantContext tenantContext,
    IUserRepository users,
    IMembershipRepository memberships,
    IAuditWriter audit,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure(new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var membership = await memberships.GetAsync(tenantId, new UserId(userId), cancellationToken);

        if (membership is null)
        {
            return Result.Failure(new Error("users.not_found", "User not found."));
        }

        var user = await users.GetByIdAsync(membership.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(new Error("users.not_found", "User not found."));
        }

        var now = clock.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.ChangeDisplayName(request.DisplayName);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (string.Equals(request.Status, nameof(UserStatus.Active), StringComparison.OrdinalIgnoreCase))
            {
                user.Activate(now);
            }
            else if (string.Equals(request.Status, nameof(UserStatus.Disabled), StringComparison.OrdinalIgnoreCase))
            {
                user.Disable(now);
            }
            else
            {
                return Result.Failure(new Error("users.invalid_status", "Status must be 'Active' or 'Disabled'."));
            }
        }

        await audit.RecordAsync(
            new AuditRecord(tenantId, null, "users.user_updated", "User", user.Id.ToString(), null),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
