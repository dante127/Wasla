using Wasla.BuildingBlocks.Application;

namespace Wasla.Customers.Application.Abstractions;

/// <summary>Module-scoped unit of work (avoids cross-module IUnitOfWork ambiguity).</summary>
public interface ICustomersUnitOfWork : IUnitOfWork
{
}
