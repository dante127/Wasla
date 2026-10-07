# Wasla — Operations Runbook

Companion to [hardening-report.md](hardening-report.md). Covers day-2 operations for the
MVP deployment (single API instance + PostgreSQL + Redis).

---

## 1. Services and lifecycle

| Item | Value |
|------|-------|
| API | `Wasla.Api` (ASP.NET Core, `/health`, `/health/live`, `/health/ready`, OpenAPI in Development) |
| Dependencies | PostgreSQL (`wasla` DB, per-module schemas + `__EFMigrationsHistory` per schema), Redis (presence, future queues) |
| Local stack | `docker compose up -d postgres redis` (postgres on host port **5433**, redis 6379) |
| Migrate + seed | `dotnet Wasla.Api.dll --seed` (applies **all module migrations**, then seeds dev data; idempotent) |
| Background workers | Inbox processor, Outbox dispatcher, Analytics recompute — toggled/configured via `Workers` config section |

### Configuration sections (appsettings / WASLA_ env vars)

`Database`, `Redis`, `Jwt`, `Security:EncryptionKey`, `Storage`, `WhatsApp`, `Telegram`,
`Workers`, `RateLimiting`, `Cors`, `Telemetry`.

**Security:EncryptionKey** encrypts channel credentials at rest (AES-256-GCM, base64 32 bytes).
Losing it makes stored provider credentials undecryptable — treat as critical secret; supply
per environment via secret manager/env var.

---

## 2. Observability

- **Health**: `/health/live` (process), `/health/ready` (PostgreSQL + Redis). Wire both to the platform prober.
- **Metrics**: OpenTelemetry meter `Wasla` — counters `wasla.inbox.received|duplicates|processed|skipped|failed`,
  `wasla.outbox.sent|retried|dead_lettered`; plus ASP.NET + runtime instrumentation. Configure
  `Telemetry:OtlpEndpoint` to export.
- **Traces**: ASP.NET + HttpClient instrumentation (OTLP).
- **Logs**: Serilog compact JSON; message bodies and tokens are redacted by design — never log payloads at info level.

### Alert rules (draft — wire to your monitoring)

| Alert | Condition | Rationale |
|-------|-----------|-----------|
| Inbox backlog age | oldest `Pending` inbox event > 2 min | worker stall / DB slowness |
| Inbox dead letters | `DeadLettered` count increase > 0 in 5 min | poison payloads / adapter faults |
| Signature failures | 401 count on webhook endpoints spike | misconfigured secret or attack |
| Outbox backlog | `Pending` outbox rows > 100 for 5 min | provider outage / dispatcher stall |
| Outbox dead letters | `DeadLettered` increase > 0 | provider rejections needing attention |
| 5xx rate | > 1% for 5 min | general health |

---

## 3. Queue operations (SQL)

```sql
-- Inbox backlog by status
SELECT "Status", count(*) FROM channels."InboxEvents" GROUP BY 1;

-- Dead-letter re-drive (safe: processing is idempotent)
UPDATE channels."InboxEvents"
SET "Status" = 'Pending', "Attempts" = 0, "NextAttemptAt" = now(), "LastError" = NULL
WHERE "Status" = 'DeadLettered' AND "ReceivedAt" > now() - interval '24 hours';

-- Outbox backlog / reap
SELECT "Status", count(*) FROM messages."OutboxMessages" GROUP BY 1;

UPDATE messages."OutboxMessages"
SET "Status" = 'Pending', "Attempts" = 0, "NextAttemptAt" = now()
WHERE "Status" = 'DeadLettered';
```

Stale in-flight rows (crashed workers) are automatically reclaimed after 10 minutes by the
claim queries (`FOR UPDATE SKIP LOCKED`); no manual intervention needed.

---

## 4. Webhooks and channels

| Provider | Endpoint | Verification |
|----------|----------|--------------|
| WhatsApp Cloud | `GET/POST /api/v1/webhooks/whatsapp/{channelId}` | `X-Hub-Signature-256` HMAC (app secret) + `hub.verify_token` on GET |
| Telegram | `POST /api/v1/webhooks/telegram/{channelId}` | `X-Telegram-Bot-Api-Secret-Token` header |

- `{channelId}` is an opaque channel UUID; the tenant is resolved server-side — never from the payload.
- Credentials per channel are set via `POST /api/v1/channels/{id}/connect` (stored encrypted) and cleared on `disconnect`.
- Telegram webhook registration is automatic when `Telegram:PublicBaseUrl` is configured (setWebhook/deleteWebhook); WhatsApp URLs are configured in the Meta app console.
- Signature failures return 401/403 and persist nothing — alert on spikes (see §2).

## 5. Rate limiting

Auth endpoints (`/api/v1/auth/login|refresh|logout`) are fixed-window limited per client
(`RateLimiting:AuthPermitLimit` / `AuthWindowSeconds`, default 30/min). Webhook endpoints are
excluded (providers have their own retry behavior and are protected by signature verification).
Disable only for troubleshooting: `RateLimiting:Enabled=false`.

## 6. Performance baseline (dev box, in-memory + local Postgres)

| Scenario | Result (Phase 9 smoke) |
|----------|------------------------|
| Webhook storm | 80 concurrent signed deliveries → **80×200 in 644 ms (~124 req/s)** |
| Ingest drain | 80 events → conversation + messages committed in **~4.4 s** (worker batch 20 / 200 ms) |
| Test suite | 147/147 green (unit + architecture + integration with Testcontainers) |

Re-run the baseline after significant changes; investigate before it regresses > 2×.

## 7. Known limitations / follow-ups

- Inbound media download is best-effort (16 MB cap); failed downloads leave the message without an attachment (by design).
- Presence keys expire after 24 h; typing/presence are broadcast-only (no persistence).
- SignalR is single-instance (no backplane yet); plan Redis/Azure SignalR backplane before scale-out (ADR-0006).
- RLS is not enabled — see [ADR-0012](decisions/0012-rls-evaluation.md) for the evaluation and triggers to revisit.
- Provider sandbox end-to-end (real Meta/Telegram accounts) still pending externally-owned apps; fixture + stub-transport coverage is complete.
- Media malware scanning integration point exists (`MediaScanStatus`) but no scanner is wired yet.
