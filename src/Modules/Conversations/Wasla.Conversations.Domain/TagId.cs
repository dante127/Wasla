using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Domain;

public readonly record struct TagId(Guid Value)
{
    public static TagId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
