using Wasla.Tenancy.Domain;

namespace Wasla.UnitTests;

public sealed class TenancyDomainTests
{
    [Fact]
    public void Create_sets_fields_and_raises_created_event()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Acme Ltd", TenantSlug.Create("acme"), "en", "UTC", now);

        Assert.Equal("Acme Ltd", tenant.Name);
        Assert.Equal("acme", tenant.Slug.Value);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(now, tenant.CreatedAt);
        Assert.Single(tenant.DomainEvents);
        Assert.IsType<TenantCreated>(tenant.DomainEvents.First());
    }

    [Fact]
    public void Suspend_and_reactivate_transition_status_with_events()
    {
        var tenant = Tenant.Create("Acme Ltd", TenantSlug.Create("acme"), "en", "UTC", DateTimeOffset.UtcNow);
        tenant.ClearDomainEvents();

        tenant.Suspend(DateTimeOffset.UtcNow);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);

        tenant.Reactivate(DateTimeOffset.UtcNow);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(2, tenant.DomainEvents.Count);
    }

    [Fact]
    public void Suspend_throws_when_tenant_is_not_active()
    {
        var tenant = Tenant.Create("Acme Ltd", TenantSlug.Create("acme"), "en", "UTC", DateTimeOffset.UtcNow);
        tenant.Suspend(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => tenant.Suspend(DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("Acme")]
    [InlineData("a1-b2")]
    [InlineData("my-company-123")]
    public void Slug_accepts_valid_values_and_normalizes_case(string value)
    {
        var slug = TenantSlug.Create(value);

        Assert.Equal(value.ToLowerInvariant(), slug.Value);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a b")]
    [InlineData("-lead")]
    [InlineData("trail-")]
    [InlineData("my_slug")]
    public void Slug_rejects_invalid_values(string value)
    {
        Assert.Throws<ArgumentException>(() => TenantSlug.Create(value));
    }
}
