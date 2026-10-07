# Wasla — Hardening Report (Phase 9)

Final phase of the MVP: security review, isolation expansion, load/failure testing,
query/index tuning, observability validation and the RLS evaluation. Every result below is
backed by captured execution evidence from this repository's build/test/smoke runs.

---

## 1. Security review

| Area | Status | Evidence / notes |
|------|--------|------------------|
| Secret management | ✅ | No secrets in source; dev-only values in `appsettings.Development.json`; `Security:EncryptionKey` + `Jwt:SigningKey` externalized |
| Channel credentials at rest | ✅ | AES-256-GCM (`AesGcmStringEncryptor`), version-prefixed; decrypt failure behaves as unconfigured |
| Authentication | ✅ | HS256 JWT (15 min) + rotating refresh with reuse detection; strict auth-endpoint rate limiting (new) |
| Webhook verification | ✅ | WhatsApp HMAC-SHA256 constant-time compare + subscription token; Telegram secret-token constant-time compare; size caps (256 KB webhook / 25 MB media) |
| Security headers | ✅ (new) | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy` on every response |
| Rate limiting | ✅ (new) | Fixed-window limiter on `/auth/login|refresh|logout`; verified live: 25 permits then **11×429** on a 35-attempt burst (dev limit 25/min) |
| Tenant isolation | ✅ | Six-layer defense; expanded suites (see §2); analytics/reports scoped; hub groups tenant-scoped |
| Provider leakage | ✅ | Architecture test locks provider identifiers to `Adapters/` (WhatsApp/Telegram field names, signature headers, setWebhook, etc.) |
| Log redaction | ✅ | Structured logs contain ids/types only; credentials never logged |
| Request body limits | ✅ (new) | Kestrel `MaxRequestBodySize` 30 MB edge cap |

## 2. Isolation test expansion (new probes)

| Probe | Result |
|-------|--------|
| Quick-reply render with another tenant's customerId | rejected; no cross-tenant data in the response |
| Conversation creation with foreign customer / foreign channel | rejected |
| Assignment to another tenant's user | rejected |
| Message send attaching another tenant's media file | rejected |
| (existing suites) conversations/messages/channels/customers/tags/users/roles/webhooks + hub isolation | all green |

## 3. Concurrency and load

| Scenario | Result |
|----------|--------|
| **Inbox storm (test)** — 50 concurrent signed deliveries, same customer | all 200; **exactly 50** messages persisted; byte-identical replay → 0 reprocessing |
| **Inbox storm (live smoke)** — 80 concurrent deliveries | **80×200 in 644 ms (~124 rps)**; drained to exactly 80 messages in ~4.4 s |
| **Send burst** — 40 concurrent sends | all accepted; dispatcher delivered all 40 via the (stub) adapter |
| Claim contention | `FOR UPDATE SKIP LOCKED` claiming with stale-claim recovery; no lost/duplicated work observed under concurrency |

## 4. Failure and retry behavior

| Scenario | Result |
|----------|--------|
| Transient adapter failure → retry | first attempt rescheduled (backoff, message stays Pending); next attempt delivers; provider id recorded |
| Rate-limited / transient provider responses | mapped with retry-after/backoff; attempt budget → dead-letter with reason (uncertain outcomes are reconciled by status webhooks — never blind-duplicated) |
| Signed-but-garbage payload | persisted for forensics, processed as `Skipped`; zero side effects; no crash |
| Duplicate delivery (event + domain level) | body-hash dedupe + provider-message-id dedupe + monotonic statuses → single effects |

## 5. Database / query tuning

- New index: `IX_Conversations_TenantId_CustomerId_ChannelId` (inbound conversation resolver path) — migration `HardeningIndexes`.
- Hot-path index inventory: inbox claim `(Status, NextAttemptAt)` + unique `(ChannelId, BodyHash)`; outbox claim `(Status, NextAttemptAt)`; messages `(TenantId, ConversationId, CreatedAt desc)` + unique filtered `IdempotencyKey` / `ProviderMessageId`; conversations `(TenantId, Status, LastMessageAt)`, assignee/team, customer(+channel).
- Aggregation costs reviewed in Phase 8: inbox list resolves customer/channel/tag look-ups in **batched contract calls** (no N+1); analytics sources use server-side grouped counts where translation-safe and bounded two-column projections otherwise.
- Flagged for production hardening: `pg_stat_statements` + slow-query review under real volume (not runnable in this environment).

## 6. Observability validation

- Health: `/health`, `/health/live`, `/health/ready` — live-verified (200, `Healthy`).
- Business metrics: meter `Wasla` counters for inbox (received/duplicates/processed/skipped/failed) and outbox (sent/retried/dead-lettered) — emitted on all pipeline paths; OTLP export via `Telemetry:OtlpEndpoint`.
- Alert rules drafted in [runbook.md](runbook.md) §2 (backlog age, dead-letter growth, signature spikes, outbox backlog).
- Traces: ASP.NET + HttpClient instrumentation active; logs are structured and redacted.

## 7. RLS evaluation

Evaluated PostgreSQL Row-Level Security — **not adopted for the MVP** (rationale, costs and
revisit triggers in [ADR-0012](decisions/0012-rls-evaluation.md)). Application-layer isolation
is enforced and test-verified on every module and the realtime/reporting surfaces.

## 8. Findings register

| # | Finding | Severity | Disposition |
|---|---------|----------|-------------|
| 1 | Rate-limit config read eagerly at startup (misses host-built config overrides) | Medium | **Fixed** — bound via options, resolved per request (caught by the new test suites) |
| 2 | Missing auth-endpoint rate limiting | Medium | **Fixed** — limiter added + live-verified 429s |
| 3 | Missing security headers | Low | **Fixed** — middleware + test |
| 4 | Conversation resolver index gap | Low | **Fixed** — `HardeningIndexes` migration |
| 5 | Inbound media download best-effort (no retry on provider failure) | Low | **Accepted** — documented; revisit with media hardening |
| 6 | Single-instance SignalR (no backplane) | Medium (scale) | **Accepted for MVP** — documented; backplane before scale-out |
| 7 | Provider sandbox E2E pending external accounts | Low | **External dependency** — fixture/stub coverage complete |

**No critical or high-severity findings remain open.**

## 9. Verification summary

- Build: 0 warnings / 0 errors (`TreatWarningsAsErrors`).
- Test suite: **147/147** green — Unit 70 · Architecture 7 · Integration 70 (Testcontainers PostgreSQL + Redis).
- Live smoke: security headers, storm throughput, exactly-once ingest, replay dedupe, rate-limit 429s — all captured.
