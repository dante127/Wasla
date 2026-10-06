using Wasla.Identity.Domain;

namespace Wasla.Identity.Domain;

/// <summary>
/// Seeded system roles and their permission bundles (docs/security.md §4). Roles are
/// created per tenant; custom roles are a later feature.
/// </summary>
public static class SystemRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Agent = "Agent";
    public const string Support = "Support";
    public const string Marketing = "Marketing";
    public const string Viewer = "Viewer";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Definitions { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Owner] = All(),

            [Admin] = All(excluding: [PermissionCatalog.Billing.Manage]),

            [Manager] =
            [
                PermissionCatalog.Conversations.Read,
                PermissionCatalog.Conversations.Create,
                PermissionCatalog.Conversations.Update,
                PermissionCatalog.Conversations.Assign,
                PermissionCatalog.Conversations.Delete,
                PermissionCatalog.Messages.Read,
                PermissionCatalog.Messages.Send,
                PermissionCatalog.Customers.Read,
                PermissionCatalog.Customers.Update,
                PermissionCatalog.Customers.Export,
                PermissionCatalog.Tickets.Read,
                PermissionCatalog.Tickets.Create,
                PermissionCatalog.Tickets.Update,
                PermissionCatalog.Tickets.Close,
                PermissionCatalog.Tasks.Read,
                PermissionCatalog.Tasks.Manage,
                PermissionCatalog.Tags.Manage,
                PermissionCatalog.QuickReplies.Read,
                PermissionCatalog.QuickReplies.Manage,
                PermissionCatalog.Teams.Read,
                PermissionCatalog.Channels.Read,
                PermissionCatalog.Reports.Read,
                PermissionCatalog.Users.Read,
                PermissionCatalog.Audit.Read,
            ],

            [Agent] =
            [
                PermissionCatalog.Conversations.Read,
                PermissionCatalog.Conversations.Create,
                PermissionCatalog.Conversations.Update,
                PermissionCatalog.Conversations.Assign,
                PermissionCatalog.Messages.Read,
                PermissionCatalog.Messages.Send,
                PermissionCatalog.Customers.Read,
                PermissionCatalog.Customers.Update,
                PermissionCatalog.Tickets.Read,
                PermissionCatalog.Tickets.Create,
                PermissionCatalog.Tickets.Update,
                PermissionCatalog.Tasks.Read,
                PermissionCatalog.QuickReplies.Read,
                PermissionCatalog.Reports.Read,
            ],

            [Support] =
            [
                PermissionCatalog.Conversations.Read,
                PermissionCatalog.Conversations.Update,
                PermissionCatalog.Messages.Read,
                PermissionCatalog.Messages.Send,
                PermissionCatalog.Customers.Read,
                PermissionCatalog.Tickets.Read,
                PermissionCatalog.Tickets.Create,
                PermissionCatalog.Tickets.Update,
                PermissionCatalog.Tickets.Close,
                PermissionCatalog.Tasks.Read,
                PermissionCatalog.QuickReplies.Read,
                PermissionCatalog.Reports.Read,
            ],

            [Marketing] =
            [
                PermissionCatalog.Conversations.Read,
                PermissionCatalog.Messages.Read,
                PermissionCatalog.Customers.Read,
                PermissionCatalog.Tasks.Read,
                PermissionCatalog.QuickReplies.Read,
                PermissionCatalog.Reports.Read,
                PermissionCatalog.Campaigns.Read,
                PermissionCatalog.Campaigns.Create,
                PermissionCatalog.Campaigns.Send,
            ],

            [Viewer] =
            [
                PermissionCatalog.Conversations.Read,
                PermissionCatalog.Messages.Read,
                PermissionCatalog.Customers.Read,
                PermissionCatalog.Tickets.Read,
                PermissionCatalog.Tasks.Read,
                PermissionCatalog.Teams.Read,
                PermissionCatalog.QuickReplies.Read,
                PermissionCatalog.Reports.Read,
            ],
        };

    private static IReadOnlyList<string> All(params string[] excluding) =>
        PermissionCatalog.All
            .Where(permission => !excluding.Contains(permission, StringComparer.Ordinal))
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();
}
