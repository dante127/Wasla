namespace Wasla.BuildingBlocks.Domain;

/// <summary>
/// The normalized message type. Hoisted to the platform layer in Phase 5 so channel
/// adapters (and cross-module dispatch) depend on the platform, never on a feature module.
/// </summary>
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

/// <summary>The normalized message direction.</summary>
public enum MessageDirection
{
    Inbound = 0,
    Outbound = 1,
}
