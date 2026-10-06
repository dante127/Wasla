# ADR-0002: Multi-tenancy — shared database & schema with TenantId and defense in depth

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla is B2B SaaS: many companies share one platform, but Tenant A must **never** see
or affect Tenant B's data. Tenancy choices trade off isolation strength, operational
cost, and migration complexity. The dominant risk is silent cross-tenant leakage from a
single missed filter — so isolation must not depend on one mechanism.

## Decision

Use **shared database, shared schema, row-level tenancy**:

- Every tenant-owned entity carries `TenantId`; `TenantId` participates in unique
  constraints and leads composite indexes.
- Tenant identity comes **only** from the authenticated token (never from client input).
- Isolation is enforced in six independent layers (routing/authN → authorization →
  EF global query filters → SaveChanges write guards → database constraints →
  mandatory automated tests), as detailed in `docs/multi-tenancy.md`.
- System-wide operations (platform tooling, retention jobs) run under an explicit
  **system context** marker with audit logging — never under a "null tenant".
- **PostgreSQL Row-Level Security** is a documented, optional hardening step
  (Phase 9), not an MVP dependency.

## Consequences

**Positive**
- Lowest operational cost; one database to operate, migrate, back up.
- Cross-tenant features (operator tooling, aggregate analytics) remain possible under
  explicit, audited system context.
- Isolation is layered — a single bug does not become a breach.

**Negative / costs**
- Discipline overhead: filters, guards, tests must be maintained for every new entity.
- No per-tenant physical isolation guarantees (acceptable for the target market;
  revisit if a tier demands dedicated infrastructure).
- Noisy-neighbor effects possible; mitigated by per-tenant rate limits and observability.

## Alternatives considered

- **Database-per-tenant** — rejected: operational/migration cost unacceptable pre-scale;
  keeps the door open later if a premium isolation tier is demanded.
- **Schema-per-tenant** — rejected: same complexity class without decisive benefit.
- **Application-layer filtering only** — rejected outright by the spec's non-negotiable
  tenant isolation requirement; hence "defense in depth".
