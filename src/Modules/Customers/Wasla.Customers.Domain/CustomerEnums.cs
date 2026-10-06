namespace Wasla.Customers.Domain;

/// <summary>Lifecycle status of a customer profile.</summary>
public enum CustomerStatus
{
    Active = 0,
    Archived = 1,
}

/// <summary>Type of a customer contact point.</summary>
public enum ContactType
{
    Phone = 0,
    Email = 1,
    Other = 2,
}
