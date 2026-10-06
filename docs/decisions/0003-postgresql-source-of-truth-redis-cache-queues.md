# ADR-0003: PostgreSQL as source of truth; Redis for cache, queues, and ephemeral state

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla needs durable relational storage plus caching, rate limiting, ephemeral real-time
state (typing/presence), and background job infrastructure. Treating Redis as a
database risks data loss (eviction, persistence gaps) and inconsistent sources of truth.

## Decision

- **PostgreSQL is the single source of truth** for all business state (messages,
  conversations, customers, audit, outbox, etc.).
- **Redis is used for**:
  - distributed caching (with tenant-prefixed keys; TTLs; never authoritative),
  - rate limiting counters,
  - ephemeral state: typing indicators, presence, distributed locks only when truly
    required,
  - background job queue for scheduled/fan-out jobs (source of truth for job *intent*
    remains the database outbox/enqueue record).
- Anything that survives a Redis flush must live in PostgreSQL. If Redis is lost, the
  system degrades in performance, not in data integrity.

## Consequences

**Positive**
- Clear durability model; recovery semantics are simple.
- Caching/rate limiting isolated from correctness-critical paths.
- The outbox pattern (ADR-0004) keeps critical side effects DB-backed, so Redis is not
  a single point of truth for message dispatch.

**Negative / costs**
- Cache coherency must be handled deliberately (short TTLs, explicit invalidation on
  writes; tenant-scoped keys).
- Two moving dependencies to operate (Redis optionality: API must tolerate Redis outage
  with degraded behavior — fail-open for cache, fail-closed only for limits).

## Alternatives considered

- **Redis as primary store** — rejected: durability/fit.
- **No cache layer initially** — partially adopted: caching only where it demonstrably
  helps (permission sets, dashboard counts); the option is designed in, not assumed.
