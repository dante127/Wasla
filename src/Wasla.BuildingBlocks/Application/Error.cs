namespace Wasla.BuildingBlocks.Application;

/// <summary>A domain/application error described by a stable code and a safe, displayable message.</summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
