# Architecture Decision Records

Wasla records every significant architectural decision as an ADR (Context / Decision /
Consequences / Status). This index is updated in the same commit as any ADR change.

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0001](0001-modular-monolith-first.md) | Modular monolith first, extraction-ready | Accepted | 2026-10-06 |
| [0002](0002-multi-tenancy-shared-schema.md) | Multi-tenancy: shared database & schema with TenantId + defense in depth | Accepted | 2026-10-06 |
| [0003](0003-postgresql-source-of-truth-redis-cache-queues.md) | PostgreSQL as source of truth; Redis for cache/queues/ephemeral state | Accepted | 2026-10-06 |
| [0004](0004-outbox-inbox-background-workers.md) | Outbox + Inbox patterns with background workers | Accepted | 2026-10-06 |
| [0005](0005-channel-adapter-abstraction.md) | Channel adapter abstraction and provider isolation | Accepted | 2026-10-06 |
| [0006](0006-signalr-tenant-scoped-realtime.md) | SignalR with tenant-scoped groups for real-time | Accepted | 2026-10-06 |
| [0007](0007-authn-jwt-oidc-permission-authorization.md) | JWT/OIDC authentication + permission-based authorization | Accepted | 2026-10-06 |
| [0008](0008-s3-compatible-media-storage.md) | S3-compatible media storage; metadata only in PostgreSQL | Accepted | 2026-10-06 |
| [0009](0009-ddd-cqrs-lite-and-testing-strategy.md) | DDD boundaries, CQRS-lite, and the testing strategy | Accepted | 2026-10-06 |
| [0010](0010-postgres-fulltext-search-first.md) | PostgreSQL full-text search first, behind a search port | Accepted | 2026-10-06 |

## Template

```markdown
# ADR-NNNN: Title

- **Status:** Proposed | Accepted | Superseded by ADR-XXXX
- **Date:** YYYY-MM-DD
- **Deciders:** (roles/people)

## Context
What problem/force leads to a decision.

## Decision
What we decided — concrete and unambiguous.

## Consequences
Positive, negative, and neutral outcomes; constraints this creates.

## Alternatives considered (optional)
What was rejected and why.
```
