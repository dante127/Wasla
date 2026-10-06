# ADR-0006: SignalR with tenant-scoped groups for real-time

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

The unified inbox is inherently real-time: new messages, status changes, assignments,
typing indicators, and notifications must reach the right agents instantly. This must
never become a cross-tenant leak channel (e.g. a broadcast reaching the wrong company),
and fan-out must stay efficient as tenant count grows.

## Decision

Use **SignalR** with **tenant-scoped groups** as the real-time transport:

- Connections authenticate with the same JWT as the API; membership in groups is
  derived **exclusively** from token claims (`tenant_id`, `user_id`, team memberships).
- Group naming: `tenant:{tenantId}`, `tenant:{tenantId}:team:{teamId}`,
  `tenant:{tenantId}:user:{userId}` (plus conversation-scoped subscription groups for
  open threads). No broadcast ever targets a group that spans tenants.
- Integration events are mapped to typed client events (`NewMessage`,
  `MessageStatusChanged`, `ConversationUpdated`, `ConversationAssigned`,
  `ConversationTagged`, `NotificationCreated`, `TypingStarted/Stopped`) in one
  mapping component with unit tests.
- Ephemeral signals (typing/presence) are Redis-backed with TTL and are not persisted.
- MVP runs a single API instance; before horizontal scale-out, a SignalR backplane
  (Redis or Azure SignalR) is introduced — group design already supports it.

## Consequences

**Positive**
- Instant inbox UX with minimal payloads; narrow fan-out (conversation/user scoped
  where possible).
- Tenant isolation is structural (group naming + claim-derived membership + tests),
  consistent with the platform-wide isolation strategy.

**Negative / costs**
- Backplane needed for multi-instance deployments (planned, not MVP-blocking).
- Clients must handle reconnect/missed-event recovery (contract: refetch on reconnect;
  events carry ids for reconciliation).

## Alternatives considered

- **Polling** — rejected: latency and load; poor UX for messaging.
- **SSE/WebSockets from scratch** — rejected: SignalR provides groups, scaling hooks,
  and mature client libraries.
- **Tenant-wide broadcasts only** — rejected: inefficient fan-out; per-user/team groups
  are first-class.
