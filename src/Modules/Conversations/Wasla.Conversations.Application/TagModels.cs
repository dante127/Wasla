namespace Wasla.Conversations.Application;

public sealed record TagListItem(Guid Id, string Key, string Name, string? Color);

public sealed record CreateTagRequest(string Key, string Name, string? Color);
