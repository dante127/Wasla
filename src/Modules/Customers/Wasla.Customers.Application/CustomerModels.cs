namespace Wasla.Customers.Application;

public sealed record CustomerTagItem(Guid Id, string Key, string Name, string? Color);

public sealed record CustomerIdentityItem(
    Guid Id,
    string ChannelType,
    string ExternalId,
    string DisplayValue,
    DateTimeOffset LastSeenAt);

public sealed record CustomerContactItem(Guid Id, string Type, string Value, bool IsVerified);

public sealed record CustomerNoteItem(Guid Id, Guid? AuthorUserId, string Body, DateTimeOffset CreatedAt);

public sealed record CustomerListItem(
    Guid Id,
    string DisplayName,
    string Status,
    IReadOnlyList<CustomerTagItem> Tags,
    int IdentityCount,
    DateTimeOffset UpdatedAt);

public sealed record CustomerDetail(
    Guid Id,
    string DisplayName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CustomerIdentityItem> Identities,
    IReadOnlyList<CustomerContactItem> Contacts,
    IReadOnlyList<CustomerNoteItem> Notes,
    IReadOnlyList<CustomerTagItem> Tags);

public sealed record CustomerSearchQuery(string? Q, IReadOnlyList<Guid>? TagIds, int Page = 1, int PageSize = 25);

public sealed record CustomerSearchResponse(
    IReadOnlyList<CustomerListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreateCustomerRequest(string DisplayName, string? Email, string? Phone);

public sealed record UpdateCustomerRequest(string DisplayName);

public sealed record AddIdentityRequest(string ChannelType, string ExternalId, string? DisplayValue);

public sealed record AddNoteRequest(string Body);

public sealed record AddTagsRequest(IReadOnlyList<Guid> TagIds);

public sealed record CustomerTimelineItem(
    Guid Id,
    string Type,
    string? Data,
    Guid? ActorUserId,
    DateTimeOffset OccurredAt);

public sealed record CustomerTimelineResponse(
    IReadOnlyList<CustomerTimelineItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
