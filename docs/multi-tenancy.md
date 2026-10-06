# Wasla — Multi-Tenancy

Wasla is a B2B SaaS platform: many companies share one application infrastructure while
their data stays strictly isolated. This document defines the tenancy model and the
**defense-in-depth** strategy that makes cross-tenant access impossible in practice —
not just "filtered in one place".

Related: [ADR-0002](decisions/0002-multi-tenancy-shared-schema.md),
[security.md](security.md), [database.md](database.md).

---

## 1. Tenancy model

- **Shared database, shared schema, row-level tenancy.** Tenant-owned rows carry
  `TenantId` (UUID). Isolation is enforced by the application (filters + guards) with
  database-level hardening options (§7).
- Tenants are created by Wasla operators (self-service signup is a later commercial
  decision); data for a tenant is never hard-deleted in MVP (staged closure).

## 2. Tenant resolution

- The **authenticated token** is the only trusted source of tenant identity. During
  login/tenant-switch, the Identity module issues an access token that contains the
  `tenant_id` claim for the selected membership.
- Resolution steps for each request:
  1. Token validation (JWT/OIDC).
  2. Membership check: the token's user must have an `Active` membership in the token's
     tenant (checked once per request; cached briefly with short TTL).
  3. `ITenantContext` (scoped) is established with `TenantId`, `UserId`, permissions.
- **Never** accept a tenant identifier from request bodies, query strings, or headers.
  Any endpoint that takes an ID takes a *resource* ID, not a tenant ID.
- Optional future enhancement: vanity subdomains / custom domains for branding only —
  they may *help resolve* which tenant context the user is entering, but they never
  replace the token claim check.

## 3. Tenant context propagation

`ITenantContext` is ambient within a request scope and is explicitly **re-established**
in every asynchronous boundary:

| Boundary | Rule |
|----------|------|
| HTTP request | Tenant context middleware, after authentication |
| Background job | Job payload must carry `TenantId`; the worker sets a context scope before executing; jobs without tenant context may only run system-level code |
| Outbox dispatcher | Every outbox row carries `TenantId`; consumers run inside a tenant context scope |
| SignalR | Connections are bound to `(TenantId, UserId)` at connect time from the token claim |
| Scheduled tasks | Execute per tenant inside a loop of tenant context scopes (or as system jobs) |

## 4. Enforcement layers (defense in depth)

A single mistake must not leak data. Isolation is enforced at **six independent layers**:

1. **Routing & authentication** — tenant claim + active membership required for any
   tenant-scoped endpoint.
2. **Authorization** — every permission policy check runs *within* a tenant context;
   roles/permissions are membership-scoped, never global.
3. **Persistence filters** — every module `DbContext` applies a global query filter
   `e.TenantId == context.TenantId` for all `ITenantOwned` entities; this also covers
   includes, joins performed by the module, and lazy scenarios. Raw SQL is banned in
   module code (only approved, reviewed system queries may bypass, see below).
4. **Write guards** — a `SaveChanges` interceptor rejects any added/modified
   `ITenantOwned` entity whose `TenantId` differs from the current tenant context, and
   stamps `TenantId` on inserts when unset. Modifying another tenant's row is
   impossible even if a filter was mistakenly bypassed for reads.
5. **Database structure** — `TenantId` is part of every unique constraint and is the
   leading column of every tenant-scoped composite index (see [database.md](database.md));
   no cross-module FKs exist to bypass.
6. **Tests** — the mandatory isolation suite (§8) exercises both positive and negative
   paths on every endpoint family in CI.

**System-context access** (e.g. platform operator tooling, analytics across tenants,
data-retention jobs) is a deliberate, separate capability:

- runs with an explicit `SystemTenantContext` marker, not an empty tenant;
- any query that intentionally spans tenants must be reachable only through dedicated,
  reviewed system services (not module repositories);
- every use is audit-logged (`audit.system_access`).

## 5. Tenant-aware behaviors checklist

| Area | Rule |
|------|------|
| **Caching** | Cache keys are prefixed `t:{tenantId}:...`; no cache entry may ever hold data mixed across tenants; invalidation is per tenant |
| **Rate limiting** | Limits are partitioned per tenant (and per user for interactive endpoints), plus per-provider for outbound sends |
| **Background jobs** | Created with tenant context; cross-tenant sweeps use per-tenant iteration |
| **Outbox/Inbox** | Every event/envelope carries `TenantId`; consumers refuse payloads missing tenant when required |
| **SignalR** | Group naming: `tenant:{tenantId}`, `tenant:{tenantId}:team:{teamId}`, `tenant:{tenantId}:user:{userId}` — broadcasts never cross the tenant root group ([ADR-0006](decisions/0006-signalr-tenant-scoped-realtime.md)) |
| **Storage** | Object keys are prefixed `tenant/{tenantId}/...`; signed URLs are issued only after authorization within the tenant |
| **Search** | Search predicates always include tenant scope; no global indexes |
| **Logging** | `TenantId` is a first-class structured log field for correlation and redaction review |

## 6. Cross-module data

Modules do not share tables and do not join across module schemas. Cross-module reads
use contracts or local projections ([architecture.md §6](architecture.md#6-module-boundaries-and-dependency-rules)).
Every contract method that returns tenant data **requires** a tenant context (the
implementation reads it from the ambient scope; callers cannot pass an arbitrary tenant
id). This keeps tenant rules in one place even as modules are extracted.

## 7. Database hardening (optional, planned)

Beyond application filters, PostgreSQL **Row-Level Security (RLS)** provides a second
wall:

- Session variable `app.current_tenant` set per connection/transaction from
  `ITenantContext`; policies `USING (TenantId = current_setting('app.current_tenant')::uuid)`
  on tenant tables.
- Enabled selectively (starting with the most sensitive tables) once the connection
  plumbing for session variables is in place; treated as a Phase 9 hardening item, not
  an MVP dependency.
- System-context queries use a dedicated role with `BYPASSRLS`, audited.

## 8. Mandatory isolation test plan (§48 of spec)

These tests are **release-blocking** and live in `tests/Wasla.IntegrationTests`:

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Tenant A lists conversations; B's rows exist | Only A's rows returned |
| 2 | A requests B's conversation by ID | `404` (not `403` — no existence leak) |
| 3 | A sends a message into B's conversation | `404` |
| 4 | A updates B's customer / tags / notes | `404`, no write |
| 5 | A accesses B's media via signed URL endpoints | `404`/`403`; storage key never leaves A's prefix |
| 6 | Token crafted with mismatched `tenant_id`/user membership | `401`/`403` |
| 7 | A subscribes to SignalR; a B event occurs | No event delivered to A |
| 8 | A's notification list never contains B data | Verified |
| 9 | Background job for A cannot mutate B rows (guarded contexts) | Verified via guard tests |
| 10 | Raw repository query without filter (simulated bug) | Write guard + filter still isolate; test documents the layered behavior |

Each scenario is implemented as an authorized integration test executed on every PR.

## 9. Failure modes we design against

| Failure mode | Prevention |
|--------------|------------|
| Developer forgets filter on a query | Global filters + write guard + tests |
| Filter bypassed by raw SQL | Raw SQL ban + architecture test (no `ExecuteSql` in modules) |
| Cache key collision across tenants | Prefix discipline + cache key builder utility + review |
| Job cross-contamination | Tenant-carrying payloads + context scope per job + tests |
| Realtime leak | Tenant-root group enforcement in hub + tests |
| IDOR via guessed IDs | All lookups scoped by tenant filter → 404 semantics |
