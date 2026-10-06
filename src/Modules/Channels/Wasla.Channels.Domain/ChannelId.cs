using Wasla.BuildingBlocks.Domain;

namespace Wasla.Channels.Domain;

public readonly record struct ChannelId(Guid Value)
{
    public static ChannelId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
