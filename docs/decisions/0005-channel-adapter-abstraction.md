# ADR-0005: Channel adapter abstraction and provider isolation

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla must integrate WhatsApp, Telegram, Instagram, Facebook, Email, and SMS. Each
provider has different APIs, payloads, auth, quirks, and policies — and they change.
The spec's most important design rule: a channel must **never** leak its
provider-specific implementation into the core CRM domain.

## Decision

Define a **provider-agnostic channel abstraction** and confine all provider code to
channel adapters:

- `IChannelAdapter` (+ supporting DTOs) lives in `Wasla.Channels.Application/Contracts`:
  verify webhook, normalize inbound, send message, send media, declare capabilities.
- Implementations live only in `Wasla.Channels.Infrastructure/Adapters/<Provider>/`.
  Provider SDKs are referenced **only** there (architecture-test enforced).
- Inbound provider payloads are normalized into the internal message model; provider
  extras are preserved in `ProviderMetadata` (JSONB, adapter-owned schema version) and
  never parsed by the core.
- **MVP adapters: WhatsApp (official Cloud API only)** and **Telegram Bot API**.
  Unofficial WhatsApp protocols/scraping are explicitly forbidden.
- Adapter-specific tests (fixture replay, outbound mapping, idempotency) are the only
  place provider field names may appear.

## Consequences

**Positive**
- Core domain and all modules stay provider-free; new channels are additive work.
- Provider churn (API/policy changes) is contained to one adapter package.
- Capability flags let features degrade gracefully per provider (e.g. delivery receipts
  absent on Telegram bots).

**Negative / costs**
- The abstraction must be designed against ≥ 2 real providers before freezing (Phases
  5–6 stage changes); occasional adapter-specific escape hatches (capability-gated) are
  expected and acceptable.
- Slightly more mapping code per provider vs direct usage — the price of isolation.

## Alternatives considered

- **Direct provider calls from application handlers** — rejected: violates the core
  rule; entangles domain with provider field names.
- **Single mega-adapter with provider switches** — rejected: same leak, worse maintenance.
