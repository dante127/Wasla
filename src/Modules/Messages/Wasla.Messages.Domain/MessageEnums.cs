namespace Wasla.Messages.Domain;

public enum MessageDirection
{
    Inbound = 0,
    Outbound = 1,
}

public enum MessageType
{
    Text = 0,
    Image = 1,
    Video = 2,
    Audio = 3,
    Document = 4,
    Location = 5,
    Contact = 6,
    Sticker = 7,
    Template = 8,
    Interactive = 9,
    System = 10,
}

public enum MessageStatus
{
    Pending = 0,
    Sent = 1,
    Delivered = 2,
    Read = 3,
    Failed = 4,
}

public enum MediaScanStatus
{
    Pending = 0,
    Clean = 1,
    Infected = 2,
    Unknown = 3,
}
