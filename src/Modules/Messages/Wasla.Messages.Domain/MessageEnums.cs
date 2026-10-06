namespace Wasla.Messages.Domain;

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
