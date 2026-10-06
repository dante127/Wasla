using Wasla.BuildingBlocks.Application;

namespace Wasla.Identity.Application.Abstractions;

/// <summary>Module-scoped unit of work (avoids cross-module IUnitOfWork ambiguity).</summary>
public interface IIdentityUnitOfWork : IUnitOfWork
{
}
