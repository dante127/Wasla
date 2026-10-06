using Wasla.BuildingBlocks.Application;

namespace Wasla.Conversations.Application.Abstractions;

/// <summary>Module-scoped unit of work (avoids cross-module IUnitOfWork ambiguity).</summary>
public interface IConversationsUnitOfWork : IUnitOfWork
{
}
