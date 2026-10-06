namespace Wasla.BuildingBlocks.Application;

/// <summary>Ambient authenticated user for the current request or background-job scope.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? DisplayName { get; }
}
