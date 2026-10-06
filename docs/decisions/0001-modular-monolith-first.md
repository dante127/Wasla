# ADR-0001: Modular monolith first, extraction-ready

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla is a multi-tenant omnichannel CRM SaaS. Its core domains (inbox, customers,
conversations, messages, channels) are **highly cohesive**: a single inbound message
touches customer resolution, conversation lifecycle, message storage, notifications,
and analytics. The initial team is small, and the product must reach real users quickly
while remaining production-grade.

Two failure paths are common:
1. Microservices too early → operational overhead, distributed-transaction pain, slow delivery.
2. Layer-per-type monolith → no internal boundaries, everything depends on everything,
   impossible to extract later.

## Decision

Build a **modular monolith**:

- One deployable (ASP.NET Core host), one process (API + workers), one PostgreSQL
  database with **one schema per module**.
- Modules have strict boundaries: `Domain / Application / Infrastructure` layers,
  communication **only** via public contracts and integration events.
- Each module owns its data; no cross-module foreign keys, joins, or internal
  references (enforced by architecture tests).
- Extraction seams are maintained deliberately: contracts, outbox events, per-module
  schema, module-scoped workers, per-module configuration.

## Consequences

**Positive**
- Fast, simple delivery and debugging; single build/test/deploy pipeline.
- Transactional consistency where it matters (within modules); no premature distributed protocols.
- Boundaries that are real (test-enforced), enabling later extraction of hot modules
  (e.g. Channels, Messages) without domain rewrites.

**Negative / costs**
- Boundary discipline must be enforced continuously (architecture tests; review).
- Single deployable means shared blast radius; mitigated by clean rollback and phased
  rollout later.
- Some cross-module flows are eventually consistent (by design; see ADR-0004).

## Alternatives considered

- **Microservices from day one** — rejected: premature operational cost, no scale need yet.
- **Unstructured layered monolith** — rejected: recreates the dependency tangle modular
  boundaries exist to prevent.
