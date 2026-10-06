# ADR-0007: JWT/OIDC authentication + permission-based authorization

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla serves multi-tenant businesses with role-differentiated users (Owner, Admin,
Manager, Agent, Support, Marketing, Viewer). Authorization must be granular (the spec
lists permission codes), must never be scattered across controllers, and must scale to
custom roles later. Authentication must be strong, and token handling must not become
the weakest link.

## Decision

- **Authentication**: JWT access tokens (short-lived, ~15 min) issued by the Identity
  module, with **rotating refresh tokens** (hashed at rest, one-time use, reuse
  detection revokes the chain). Local password auth (if enabled) uses ASP.NET Core
  Identity hashers; architecture is OIDC-ready for external IdPs later.
- **Authorization**: **permission-based**, centralized:
  - a closed **permission catalog** (codes such as `conversation.assign`),
  - seeded roles as permission bundles; custom roles later,
  - ASP.NET Core **policy handlers** evaluate permission requirements declared as
    endpoint metadata — no authorization logic in endpoints/controllers,
  - resource-scoped rules (e.g. assignment guard) are policies too,
  - endpoints without declared permissions fail an architecture test (deny by default).
- **Tenant membership** is validated per request; tokens are tenant-scoped
  (`tenant_id` claim) and tenant switching re-issues tokens.
- "Last owner" protection: a tenant can never lose its final Owner.

## Consequences

**Positive**
- One vocabulary for permissions across API, UI (effective-permission endpoint), and
  tests; adding a role is data + seeds, not scattered conditionals.
- Token compromise windows are short; refresh theft is detectable and revocable.
- OIDC can be added per tenant without reworking authorization.

**Negative / costs**
- Permission snapshot in tokens can go stale for write-sensitive cases → mitigated via
  server-side re-checks on sensitive operations with a short permission cache.
- More indirection than `[Authorize(Roles="...")]` — deliberate; role checks are too
  coarse for the product's granularity.

## Alternatives considered

- **Role-based checks only** — rejected: insufficient granularity; brittle as roles evolve.
- **Externally hosted IdP from day one** — deferred: adds onboarding complexity for MVP;
  the design allows adding it later without breaking local auth.
- **Opaque session tokens with server lookup per request** — rejected for API scale
  (kept only for refresh tokens where lookup is required anyway).
