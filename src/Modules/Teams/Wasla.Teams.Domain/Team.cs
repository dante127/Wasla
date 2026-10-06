using Wasla.BuildingBlocks.Domain;

namespace Wasla.Teams.Domain;

public readonly record struct TeamId(Guid Value)
{
    public static TeamId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public enum TeamRole
{
    Lead = 0,
    Member = 1,
}

/// <summary>A member of a team (link entity with a team-scoped role).</summary>
public sealed class TeamMember
{
    private TeamMember()
    {
    }

    public TeamMember(TeamId teamId, UserId userId, TeamRole role, DateTimeOffset joinedAt)
    {
        TeamId = teamId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public TeamId TeamId { get; private set; }

    public UserId UserId { get; private set; }

    public TeamRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }
}

/// <summary>A team: an assignment target for conversations and tickets.</summary>
public sealed class Team : AggregateRoot<TeamId>, ITenantOwned
{
    private Team()
    {
        Name = string.Empty;
    }

    private Team(TeamId id, TenantId tenantId, string name, string? description, DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        CreatedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public List<TeamMember> Members { get; private set; } = [];

    public static Team Create(TenantId tenantId, string name, string? description, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Team name is required.", nameof(name));
        }

        var team = new Team(TeamId.New(), tenantId, name.Trim(), description?.Trim(), now);
        team.RaiseDomainEvent(new TeamCreated(Guid.NewGuid(), now, team.Id, tenantId, team.Name));

        return team;
    }

    public void Rename(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Team name is required.", nameof(name));
        }

        Name = name.Trim();
        Description = description?.Trim();
    }

    public void ReplaceMembers(IReadOnlyCollection<(UserId UserId, TeamRole Role)> members, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(members);

        if (members.Select(member => member.UserId).Distinct().Count() != members.Count)
        {
            throw new ArgumentException("Duplicate team members are not allowed.", nameof(members));
        }

        if (members.Count > 0 && members.All(member => member.Role != TeamRole.Lead))
        {
            throw new ArgumentException("An active team must have at least one Lead.", nameof(members));
        }

        Members.Clear();
        Members.AddRange(members.Select(member => new TeamMember(Id, member.UserId, member.Role, now)));

        RaiseDomainEvent(new TeamMembersChanged(Guid.NewGuid(), now, Id, TenantId));
    }
}

public sealed record TeamCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    TeamId TeamId,
    TenantId TenantId,
    string Name) : IDomainEvent;

public sealed record TeamMembersChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    TeamId TeamId,
    TenantId TenantId) : IDomainEvent;
