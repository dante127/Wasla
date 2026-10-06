# Wasla — Roadmap, Risks & Open Questions

This document fixes the MVP scope, the phase plan, and the standing risk register.
Phases are executed in order; each phase ends with a green build, passing tests, and a
pushed, reviewable state (§61, §67).

---

## 1. MVP scope (first production target, §63)

| Area | Included in MVP |
|------|-----------------|
| Identity | Tenants, users, memberships, roles, permissions, teams, authentication, authorization |
| Channels | **WhatsApp (Cloud API)** and **Telegram** adapters; channel connect/verify/disconnect |
| Inbox | Conversations, messages, attachments, assignment (user/team), status, priority, tags, internal notes, mentions, quick replies |
| Customer 360 | Customers, channel identities, contact data, conversation history, search, filters |
| Infrastructure | PostgreSQL, Redis, S3-compatible storage, SignalR, background jobs, webhooks, outbox, inbox/idempotency, audit log, logging, OpenTelemetry, health checks |
| Analytics | Basic: conversations, messages, first response time, resolution time, agent performance |
| API | Versioned REST + OpenAPI; SignalR hub for real-time |

**Out of MVP** (designed for, not built): Instagram/Facebook/Email/SMS adapters,
Campaigns, Automation, Bot Builder, Commerce (orders/payments), SaaS Billing, AI
capabilities, advanced analytics, customer merge UX, RLS hardening (optional Phase 9).

---

## 2. Phase plan (§64)

| Phase | Objective | Key deliverables | Acceptance |
|-------|-----------|------------------|------------|
| **0 — Architecture** ✅ | Assessment + design before code | This docs set: architecture, domain model, ERD, boundaries, API contract, security model, multi-tenancy strategy, event model, ADRs | docs reviewed; decisions recorded (this repo state) |
| **1 — Foundation** | Runnable skeleton | Solution + projects + dependency rules; PostgreSQL + EF Core wiring (per-module contexts, schemas, migrations infra); Redis; configuration (strongly-typed options + validation); Serilog; OpenTelemetry; health checks; Docker + compose; GitHub Actions CI; global exception handling + ProblemDetails; `.env.example` | `dotnet build` + all test projects green; `docker compose up` runs API + deps; CI pipeline green on `main`; health endpoints respond |
| **2 — Identity & Tenancy** | AuthN/AuthZ + tenant core | Tenant, User, Membership, Role, Permission, Team domain + persistence; JWT issuance/rotation; permission policies; tenant context + filters + write guards; audit trail base; tenant isolation test suite (10 scenarios) | isolation suite green (release-blocking); auth flows integration-tested; seeded roles verified |
| **3 — Customers** | Customer 360 base | Customer, identities, contacts, tags, notes, activities; identity resolution (no auto-merge); search (FTS/trigram); Customer 360 read APIs | identity resolution tests (same phone → same customer); search tests; isolation extended to customer endpoints |
| **4 — Conversations** | Unified inbox core | Conversation lifecycle, message model + store, attachments pipeline (storage + validation), assignment, statuses, priority, tags, internal notes + mentions, quick replies (safe template renderer); inbox query APIs with cursor pagination | send/receive flows integration-tested via simulated channel; inbox filters + pagination tests; quick-reply renderer unit tests |
| **5 — WhatsApp** | First real adapter | WhatsApp Cloud API adapter (official only): connect/verify, outbound send (text/media/template), inbound webhook verify + normalize, status callbacks; fixture replay tests | end-to-end against sandbox number: inbound → conversation/message rows; outbound → delivered; duplicates produce single effects |
| **6 — Telegram** | Second adapter, same abstraction | Telegram Bot API adapter: setWebhook with secret, outbound send, inbound normalize, media download to storage | same acceptance as Phase 5 on TG; architecture tests prove zero provider leakage |
| **7 — Real-Time** | Live inbox | SignalR hub with tenant-scoped groups; events: NewMessage, MessageStatusChanged, ConversationUpdated, ConversationAssigned, ConversationTagged, NotificationCreated, Typing | live updates verified in integration tests; tenant group isolation test; presence/typing via Redis |
| **8 — Analytics** | Basic reporting | Fact tables + rollups from day-one events; overview/agent/channel reports; idempotent recompute jobs | rollups match event-derived expected values; report endpoints permission-tested |
| **9 — Hardening** | Production readiness | Security review; isolation test expansion; load testing (webhook storms, send bursts); DB query analysis + index tuning; failure/retry chaos tests; observability validation (dashboards/alerts); RLS evaluation; runbook completion | load/failure results recorded; no critical findings open; alerts verified firing; performance baseline documented |

---

## 3. Risk register

### 3.1 Architectural risks

| Risk | Likelihood | Impact | Mitigation | Status |
|------|-----------|--------|------------|--------|
| Provider complexity leaking into core | Medium | High | Adapter contract + architecture tests + provider SDK isolation | Designed (ADR-0005) |
| Cross-module coupling creep (shared tables/queries) | Medium | High | Contracts/events only; no cross-schema joins; architecture tests | Designed (ADR-0009) |
| Outbox dispatch bottleneck at scale | Low→Med | Medium | Batch + SKIP LOCKED claim; horizontal dispatchers; backlog metrics; volume watch list | Designed (ADR-0004) |
| Tenant leakage via a missed filter | Low | Critical | Six-layer defense + write guards + mandatory test suite | Designed (ADR-0002) |
| WhatsApp policy changes (pricing/windows/templates) | Medium | High | All provider variance behind adapter; capability flags; templates supported from Phase 5 | Accepted risk, adapter-scoped |
| Multi-instance SignalR fan-out without backplane | Medium | Medium | MVP single instance + Redis presence; backplane (Azure SignalR/Redis) planned before scale-out | Watch (Phase 7) |

### 3.2 Delivery risks

| Risk | Mitigation |
|------|-----------|
| Scope creep beyond MVP | MVP scope table above is binding; post-MVP items require explicit decisions |
| SDK/toolchain friction (.NET 10 on dev machines) | Install SDK early (pre-Phase 1 gate); CI pins SDK version |
| Provider sandbox access (WhatsApp test number, bot) | Acquire sandbox apps early; adapters developed test-first with fixtures |
| Long-lived feature branches drift | Trunk-based + phase-sized increments |

### 3.3 Performance risks

| Risk | Where | Mitigation |
|------|-------|-----------|
| N+1 in inbox list (customer/channel/tags per row) | `/conversations` | Projection queries; batched contract reads; query-count assertions on hot paths |
| Message history scan growth | messages table | Cursor pagination + composite index; partitioning path documented |
| Webhook storms (provider retries after outage) | ingress | Fast path + queue + claim-based workers scale-out; backlog alerts |
| SignalR fan-out to large tenants | Phase 7+ | Narrow groups (conversation/user scoped); payload small (ids + minimal fields) |
| Heavy analytics on OLTP | reports | Fact tables + scheduled recompute, never ad-hoc scans of messages |

---

## 4. Open questions (for the product owner)

1. **Auth for MVP**: local email/password only, or OIDC/SSO from day one? (Design supports both; local assumed for Phase 2.)
2. **WhatsApp onboarding**: which Meta Business account / test number will Phase 5 use, and who owns template approvals?
3. **Hosting target**: cloud provider + region (affects managed PostgreSQL/Redis/storage choices and data residency).
4. **SLA definitions**: default first-response/resolution targets per priority for the SLA fields (fields exist; targets need product input).
5. **Tenant signup**: operator-provisioned tenants for MVP (assumed) vs self-service signup later — confirm.
6. **Email sending** for notifications (Phase 2+ notification emails): provider preference (SendGrid/SES/SMTP)?
7. **Data retention defaults**: message/audit retention windows for non-paying tiers (placeholder policy exists in database.md).

---

## 5. Standing verification rule

Each phase ends with: green build + tests, docs updated, an ADR for any new decision,
and a pushed commit (`Phase N: ...`) on `main` — so the repository always tells the
project's true story (§69 requires nothing less).
