# ADR-0012: PostgreSQL Row-Level Security — evaluation (not adopted for MVP)

## Status
Accepted (evaluation, Phase 9). Decision: **do not enable RLS for the MVP**; revisit under
the triggers listed below.

## Context
The platform stores all tenants in shared schemas with a `TenantId` column. Isolation is a
six-layer defense in depth: JWT-scoped tenant context → authorization policies → EF query
filters per module (`ModuleDbContext.ApplyTenantFilter`) → SaveChanges write guards →
database constraints/indexes → release-blocking isolation test suites. Phase 9 asked whether
PostgreSQL RLS should replace/augment the EF-level filters.

## Evaluation

**How RLS would work here:** per-request session GUC (e.g. `SET LOCAL app.tenant_id = ...`)
plus `CREATE POLICY` per tenant-owned table using `USING ("TenantId" = current_setting('app.tenant_id')::uuid)`.

**Costs and risks identified:**
- Connection pooling: `SET LOCAL` must be transaction-scoped and reliably reset; a missed reset leaks across pooled connections (fail-open risk precisely where defense matters most).
- Migration/maintenance burden: every new tenant-owned table needs policies; drift is a silent hole.
- Analytics/worker paths: platform jobs legitimately cross tenants (analytics recompute, inbox claiming) and would need bypass roles — expanding privileged-surface management.
- Debuggability: policy failures surface as empty results (confusing support cases) unless carefully instrumented.
- Current EF filters + write guards + tests already provide defense at the layer where all application traffic flows; RLS adds value mainly against *direct database access*.

## Decision
Keep application-layer enforcement for the MVP. Do **not** enable RLS now.

## Revisit triggers
1. Compliance requirement for database-enforced tenancy.
2. Direct/human DB access to production data added (analytics notebooks, BI tools, support consoles).
3. Multi-service access to the same database (additional services beyond this monolith).
4. A tenancy incident traced to an application-layer bypass.

## Implementation sketch (if adopted later)
Per-connection `SET LOCAL app.tenant_id` in an EF interceptor; policies generated centrally
from a shared base class/attribute on `ITenantOwned` entities; a dedicated bypass role used
only by the (audited) platform jobs; isolation suite extended with a raw-connection test
matrix proving policy coverage per table.

## Consequences
- No change to current behavior; isolation remains test-verified at the application layer.
- Recorded as an evaluated, deliberate deferral — not an oversight — for security reviews.
