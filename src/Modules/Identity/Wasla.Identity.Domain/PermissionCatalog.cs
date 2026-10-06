namespace Wasla.Identity.Domain;

/// <summary>
/// The closed permission catalog (see docs/security.md §3). Codes are part of the API
/// contract; adding a code is a deliberate change, typos are bugs.
/// </summary>
public static class PermissionCatalog
{
    public static class Conversations
    {
        public const string Read = "conversation.read";
        public const string Create = "conversation.create";
        public const string Update = "conversation.update";
        public const string Assign = "conversation.assign";
        public const string Delete = "conversation.delete";
    }

    public static class Messages
    {
        public const string Read = "message.read";
        public const string Send = "message.send";
        public const string Delete = "message.delete";
    }

    public static class Customers
    {
        public const string Read = "customer.read";
        public const string Update = "customer.update";
        public const string Export = "customer.export";
    }

    public static class Tickets
    {
        public const string Read = "ticket.read";
        public const string Create = "ticket.create";
        public const string Update = "ticket.update";
        public const string Close = "ticket.close";
    }

    public static class Tasks
    {
        public const string Read = "task.read";
        public const string Manage = "task.manage";
    }

    public static class Tags
    {
        public const string Manage = "tag.manage";
    }

    public static class QuickReplies
    {
        public const string Read = "quickreply.read";
        public const string Manage = "quickreply.manage";
    }

    public static class Teams
    {
        public const string Read = "team.read";
        public const string Manage = "team.manage";
    }

    public static class Channels
    {
        public const string Read = "channel.read";
        public const string Manage = "channel.manage";
    }

    public static class Reports
    {
        public const string Read = "report.read";
    }

    public static class Users
    {
        public const string Read = "user.read";
        public const string Create = "user.create";
        public const string Update = "user.update";
        public const string Delete = "user.delete";
    }

    public static class Settings
    {
        public const string Manage = "settings.manage";
    }

    public static class Audit
    {
        public const string Read = "audit.read";
    }

    public static class Billing
    {
        public const string Read = "billing.read";
        public const string Manage = "billing.manage";
    }

    public static class Campaigns
    {
        public const string Read = "campaign.read";
        public const string Create = "campaign.create";
        public const string Send = "campaign.send";
    }

    public static class Bots
    {
        public const string Read = "bot.read";
        public const string Create = "bot.create";
        public const string Publish = "bot.publish";
    }

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Conversations.Read, Conversations.Create, Conversations.Update, Conversations.Assign, Conversations.Delete,
        Messages.Read, Messages.Send, Messages.Delete,
        Customers.Read, Customers.Update, Customers.Export,
        Tickets.Read, Tickets.Create, Tickets.Update, Tickets.Close,
        Tasks.Read, Tasks.Manage,
        Tags.Manage,
        QuickReplies.Read, QuickReplies.Manage,
        Teams.Read, Teams.Manage,
        Channels.Read, Channels.Manage,
        Reports.Read,
        Users.Read, Users.Create, Users.Update, Users.Delete,
        Settings.Manage,
        Audit.Read,
        Billing.Read, Billing.Manage,
        Campaigns.Read, Campaigns.Create, Campaigns.Send,
        Bots.Read, Bots.Create, Bots.Publish,
    };

    public static bool IsKnown(string permission) => All.Contains(permission);
}
