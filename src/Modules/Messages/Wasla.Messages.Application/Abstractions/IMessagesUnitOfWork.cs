using Wasla.BuildingBlocks.Application;

namespace Wasla.Messages.Application.Abstractions;

/// <summary>Module-scoped unit of work (avoids cross-module IUnitOfWork ambiguity).</summary>
public interface IMessagesUnitOfWork : IUnitOfWork
{
}
