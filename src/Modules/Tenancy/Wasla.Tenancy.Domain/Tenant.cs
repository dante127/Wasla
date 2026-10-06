using Wasla.BuildingBlocks.Domain;

namespace Wasla.Tenancy.Domain;

/// <summary>Root aggregate for a tenant (company account) in the Wasla platform.</summary>
public sealed class Tenant : AggregateRoot<TenantId>, ITenantOwned
{
    private Tenant()
    {
        Name = string.Empty;
        Slug = TenantSlug.FromTrusted(string.Empty);
        DefaultCulture = "en";
        TimeZone = "UTC";
    }

    private Tenant(
        TenantId id,
        string name,
        TenantSlug slug,
        string defaultCulture,
        string timeZone,
        DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        TenantId = id;
        DefaultCulture = defaultCulture;
        TimeZone = timeZone;
        Status = TenantStatus.Active;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; }

    public TenantSlug Slug { get; private set; }

    public TenantStatus Status { get; private set; }

    public string DefaultCulture { get; private set; }

    public string TimeZone { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public TenantId TenantId { get; private set; }

    public static Tenant Create(
        string name,
        TenantSlug slug,
        string defaultCulture,
        string timeZone,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tenant name is required.", nameof(name));
        }

        var tenant = new Tenant(
            TenantId.New(),
            name.Trim(),
            slug,
            defaultCulture,
            timeZone,
            now);

        tenant.RaiseDomainEvent(new TenantCreated(Guid.NewGuid(), now, tenant.Id, tenant.Name, slug.Value));

        return tenant;
    }

    public void Suspend(DateTimeOffset now)
    {
        if (Status != TenantStatus.Active)
        {
            throw new InvalidOperationException($"Only an active tenant can be suspended (current status: {Status}).");
        }

        Status = TenantStatus.Suspended;
        RaiseDomainEvent(new TenantSuspended(Guid.NewGuid(), now, Id));
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (Status != TenantStatus.Suspended)
        {
            throw new InvalidOperationException($"Only a suspended tenant can be reactivated (current status: {Status}).");
        }

        Status = TenantStatus.Active;
        RaiseDomainEvent(new TenantReactivated(Guid.NewGuid(), now, Id));
    }
}
