namespace Wasla.Channels.Application;

public sealed record ChannelListItem(Guid Id, string Type, string DisplayName, string Status);

public sealed record CreateChannelRequest(string Type, string DisplayName);
