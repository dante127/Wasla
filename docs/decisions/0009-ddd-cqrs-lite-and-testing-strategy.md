# ADR-0009: DDD boundaries, CQRS-lite, and the testing strategy

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla's business rules are real (conversation lifecycle, assignment, identity
resolution, message pipeline), but the platform must not drown in ceremony. The spec
demands: DDD where it provides value, CQRS where it provides value, no anemic models,
no god services, and a testing strategy strong enough to protect tenant isolation and
provider boundaries.

## Decision

**Domain modeling**
- DDD where value is real: aggregates with invariants (Conversation, Message, Customer,
  Channel), value objects, domain events; strongly-typed IDs (UUIDv7).
- Simple structures stay simple (tags, labels, settings) — no forced patterns.
- Domain purity: no infrastructure, no provider types, no framework leakage.

**CQRS-lite**
- Commands (mutations: `SendMessageCommand`, `AssignConversationCommand`, ...) and
  Queries (reads: `GetInboxQuery`, `GetConversationMessagesQuery`, ...) are distinct
  handler types; queries bypass aggregates and use projections.
- No event sourcing for business state; no separate read database in MVP — projections
  are SQL query DTOs, with fact tables only for analytics.
- A light in-process dispatcher; no heavyweight framework required.

**Testing strategy (release-blocking)**
1. Unit tests — domain rules, validators, authorization rules, renderers.
2. Integration tests — Testcontainers (PostgreSQL + Redis): API flows, webhooks
   (signature/replay/duplicate), idempotency, outbox dispatch, pagination.
3. Architecture tests — dependency direction, module isolation, provider-SDK isolation,
   endpoint→permission mapping, no raw SQL in modules.
4. **Tenant isolation suite** — the 10-scenario matrix is mandatory and blocks releases.
5. Deterministic fixtures — anonymized provider payloads replayed in adapter tests.

**Module data ownership**
- One `DbContext` per module, one schema per module, no cross-module joins/FKs;
  cross-module reads via contracts (batched) or local projections.

## Consequences

**Positive**
- Business rules are testable in isolation; the domain stays honest.
- Reads are fast and explicit (projections), writes carry invariants.
- The rules that matter most (isolation, boundaries, provider purity) are machine-checked.

**Negative / costs**
- More handler/types surface than a CRUD service; mitigated by "CQRS where it pays" and
  small cohesive handlers.
- Cross-module eventual consistency requires discipline in UX (loading/pending states).

## Alternatives considered

- **Full CQRS + event sourcing** — rejected: complexity not justified for MVP.
- **Anemic CRUD services** — rejected: business rules would scatter; spec forbids.
- **One shared DbContext for all modules** — rejected: boundary erosion and extraction
  pain; violates module ownership.
