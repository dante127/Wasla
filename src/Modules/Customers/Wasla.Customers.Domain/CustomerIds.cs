using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Domain;

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerIdentityId(Guid Value)
{
    public static CustomerIdentityId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerContactId(Guid Value)
{
    public static CustomerContactId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerNoteId(Guid Value)
{
    public static CustomerNoteId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerActivityId(Guid Value)
{
    public static CustomerActivityId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
