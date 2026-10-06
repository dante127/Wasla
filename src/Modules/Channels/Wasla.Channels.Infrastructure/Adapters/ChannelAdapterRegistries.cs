using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Channels.Infrastructure.Adapters;

/// <summary>Resolves channel adapters by type; provider strings are never switched on elsewhere.</summary>
public sealed class ChannelAdapterRegistry : IChannelAdapterRegistry
{
    private readonly Dictionary<ChannelType, IChannelAdapter> _adapters;

    public ChannelAdapterRegistry(IEnumerable<IChannelAdapter> adapters) =>
        _adapters = adapters
            .GroupBy(adapter => adapter.ChannelType)
            .ToDictionary(group => group.Key, group => group.Last());

    public IChannelAdapter? Resolve(ChannelType channelType) =>
        _adapters.TryGetValue(channelType, out var adapter) ? adapter : null;
}

/// <summary>Resolves connection verifiers by type (adapter optional capability).</summary>
public sealed class ChannelConnectionVerifierRegistry : IChannelConnectionVerifierRegistry
{
    private readonly Dictionary<ChannelType, IChannelConnectionVerifier> _verifiers;

    public ChannelConnectionVerifierRegistry(IEnumerable<IChannelConnectionVerifier> verifiers) =>
        _verifiers = verifiers
            .GroupBy(verifier => verifier.ChannelType)
            .ToDictionary(group => group.Key, group => group.Last());

    public IChannelConnectionVerifier? Resolve(ChannelType channelType) =>
        _verifiers.TryGetValue(channelType, out var verifier) ? verifier : null;
}
