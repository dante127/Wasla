# ADR-0011: Channel adapter contracts hoisted to BuildingBlocks

## Status
Accepted (Phase 5).

## Context
The channel adapter abstraction was originally placed in `Wasla.Channels.Application/Contracts`
(docs/channels.md §2). Phase 5 wired two real flows:

- **Inbound**: the Channels pipeline must insert messages via `Wasla.Messages.Application`.
- **Outbound**: the Messages outbox dispatcher must resolve adapters via the registry.

The solution architecture tests forbid infrastructure-to-infrastructure references across
modules, and allow only foreign `*.Application` references. Making both directions real would
have produced project cycles: `Messages.Application <-> Channels.Application` (registry vs
message writer) and, with the Conversations contracts already consumed by Messages,
`Channels -> Messages -> Conversations -> Channels` transitively.

## Decision
The provider-facing protocol contracts (`IChannelAdapter`, `IChannelAdapterRegistry`,
`IChannelConnectionVerifier(Registry)`, webhook/normalized-event/send/connect DTOs, credential
key names, routing info) live in **`Wasla.BuildingBlocks.Application.ChannelAdapters`**.
Adapter implementations remain in `Wasla.Channels.Infrastructure/Adapters/<Provider>/`.
The channel read contract (`ChannelInfo`, `IChannelInfoProvider`) was hoisted to
`Wasla.BuildingBlocks.Application.Contracts` for the same reason (Conversations renders
channel references without referencing the Channels module — mirroring the Phase 4
`CustomerSummary` hoist).

## Consequences
- `Messages.Application` depends on the platform contract only; no Messages->Channels reference.
- `Channels.Application` consumes Messages/Conversations/Customers contracts one-directionally.
- The ChannelType enum and MessageType/MessageDirection already live in `BuildingBlocks.Domain`
  (Phase 3/5), keeping the adapter protocol provider-neutral.
- docs/channels.md §2 amended; future adapters (Telegram, Instagram, ...) are unaffected by
  the location change.