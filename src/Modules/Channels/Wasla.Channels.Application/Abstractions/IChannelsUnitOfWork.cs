using Wasla.BuildingBlocks.Application;

namespace Wasla.Channels.Application.Abstractions;

/// <summary>Module-scoped unit of work (avoids cross-module IUnitOfWork ambiguity).</summary>
public interface IChannelsUnitOfWork : IUnitOfWork
{
}
