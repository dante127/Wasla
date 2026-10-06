using Wasla.BuildingBlocks.Domain;

namespace Wasla.Messages.Domain;

public readonly record struct MessageId(Guid Value)
{
    public static MessageId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct AttachmentId(Guid Value)
{
    public static AttachmentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct MediaFileId(Guid Value)
{
    public static MediaFileId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
