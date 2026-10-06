# ADR-0004: Outbox + Inbox patterns with background workers

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla must never lose side effects (notifications, broadcasts, sends) when a process
crashes between a database commit and a queue publish — the classic
"database succeeded, queue failed" hole. Conversely, providers will deliver webhooks
**more than once**; duplicate delivery must not create duplicate messages.

## Decision

1. **Outbox pattern** for all cross-module and outbound side effects: integration events
   are written to a per-module `outbox_messages` table **in the same transaction** as
   business data; a dispatcher worker drains rows (claim-based, `FOR UPDATE SKIP LOCKED`),
   invoking in-process handlers, SignalR broadcasts, and job enqueues.
2. **Inbox pattern** for provider webhooks: raw events are persisted with a uniqueness
   constraint before acknowledgement; processing checks and marks consumption once
   (`docs/webhooks.md`).
3. **At-least-once (+ idempotency)** delivery semantics: consumers must be idempotent
   (dedupe by event id); domain-level constraints (e.g. unique provider message id)
   provide the effective-once outcome.
4. Retries with exponential backoff and jitter; bounded attempts; **dead-letter** rows
   with alerting and a re-drive tool (safe by idempotency).
5. Heavy work never runs inline with HTTP requests or webhook acknowledgements.

## Consequences

**Positive**
- No lost side effects across crashes; ordering controllable per aggregate.
- Webhook replays are harmless; message duplication is structurally impossible.
- Backlog and failure states are observable (metrics on backlog age, dead letters).

**Negative / costs**
- More moving parts (dispatcher loops, claim queries, retry state) — one generic
  implementation reused by every module mitigates this.
- Eventual consistency between modules in flows that used to be one transaction; UI
  must tolerate small propagation delays (acceptable for these flows).

## Alternatives considered

- **Publish directly to a queue after commit** — rejected: loses events on crash
  window; ordering and retry semantics weaker.
- **Distributed transactions (2PC)** — rejected: complexity; outbox solves the actual problem.
- **External messaging framework/bus** — deferred: Redis + DB outbox meets MVP needs;
  a broker (e.g. RabbitMQ/Kafka) can slot behind the same outbox dispatcher if scale
  demands, without domain changes.
