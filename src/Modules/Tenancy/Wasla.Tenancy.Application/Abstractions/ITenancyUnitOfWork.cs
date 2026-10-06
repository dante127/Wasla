using Wasla.BuildingBlocks.Application;

namespace Wasla.Tenancy.Application.Abstractions;

/// <summary>Module-scoped unit of work (avoids cross-module IUnitOfWork ambiguity).</summary>
public interface ITenancyUnitOfWork : IUnitOfWork
{
}
