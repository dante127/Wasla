using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.UnitTests;

public sealed class EntityTests
{
    [Fact]
    public void Entities_with_same_id_and_type_are_equal()
    {
        var id = TenantId.New();

        var first = new TestEntity(id);
        var second = new TestEntity(id);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var first = new TestEntity(TenantId.New());
        var second = new TestEntity(TenantId.New());

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    private sealed class TestEntity(TenantId id) : Entity<TenantId>(id)
    {
    }
}

public sealed class AggregateRootTests
{
    [Fact]
    public void Raised_domain_events_are_collected_and_clearable()
    {
        var aggregate = new TestAggregate(TenantId.New());

        aggregate.DoSomething();

        var domainEvent = Assert.Single(aggregate.DomainEvents);
        Assert.IsType<TestDomainEvent>(domainEvent);

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    private sealed class TestAggregate(TenantId id) : AggregateRoot<TenantId>(id)
    {
        public void DoSomething() =>
            RaiseDomainEvent(new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
}

public sealed class ResultTests
{
    [Fact]
    public void Success_result_exposes_its_value()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_result_carries_the_error_and_throws_on_value_access()
    {
        var error = new Error("test.error", "Something failed.");
        var result = Result<int>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Failure_result_requires_an_error()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
    }
}

public sealed class TenantIdTests
{
    [Fact]
    public void New_generates_unique_non_empty_values()
    {
        var first = TenantId.New();
        var second = TenantId.New();

        Assert.NotEqual(first, second);
        Assert.NotEqual(Guid.Empty, first.Value);
    }
}
