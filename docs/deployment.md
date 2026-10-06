# Wasla — Deployment & Operations

This document defines environments, containerization, configuration, CI/CD, health
checks, and observability targets. It prepares Wasla for development, staging, and
production (§61) with a GitHub-hosted repository and GitHub Actions as the initial CI.

Related: [security.md §7](security.md#7-secrets-management),
[ADR-0003](decisions/0003-postgresql-source-of-truth-redis-cache-queues.md).

---

## 1. Environments

| Aspect | Development | Staging | Production |
|--------|-------------|---------|------------|
| Purpose | Local work + tests | Pre-prod verification, demos | Live customers |
| Config source | `.env` / user-secrets (placeholders in `.env.example`) | Secret manager + env vars | Secret manager + env vars |
| Database | Dockerized PostgreSQL (local) | Managed PostgreSQL | Managed PostgreSQL (HA, backups, PITR) |
| Redis | Dockerized | Managed Redis | Managed Redis (HA) |
| Storage | MinIO container (S3-compatible) | S3-compatible bucket | S3-compatible bucket (private) |
| Provider apps | Sandbox test numbers/bots | Staging apps/bots | Production apps/bots |
| Log level | Debug | Information (+ selected Debug) | Information/Warning, redacted |
| Feature flags | All enabled | Per plan defaults | Per tenant/plan |
| Data | Seed data (§49, synthetic only) | Synthetic | Real (PII rules apply) |

Rule: **no production secret ever appears outside the production secret store**; staging
and development use separate provider applications and databases.

---

## 2. Containers

Services (docker compose, §46):

| Service | Image | Notes |
|---------|-------|-------|
| `wasla-api` | built from `src` Dockerfile | API + background workers (MVP single process) |
| `postgres` | `postgres:17` (or current stable) | dev volume; healthcheck |
| `redis` | `redis:7` (or current stable) | cache/queues; healthcheck |
| `minio` | `minio/minio` (optional profile) | local S3-compatible storage |

Dockerfile (multi-stage, outline):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# restore, publish (release)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# non-root user, copy publish output, EXPOSE 8080
```

- The application must run locally with `docker compose up` where practical; the API
  waits for dependencies via healthchecks (compose `depends_on: condition: service_healthy`).
- Migrations: local `dotnet ef database update`; containers do not auto-migrate on
  startup (explicit step, §47).
- Secrets enter containers via environment variables; never baked into images.

---

## 3. Configuration (§44)

Strongly typed options, one group per concern; bound with validation on startup
(fail fast on missing critical config):

| Options group | Key settings |
|---------------|--------------|
| `DatabaseOptions` | connection string, pool sizes, command timeout |
| `RedisOptions` | connection, prefix |
| `StorageOptions` | endpoint, bucket, credentials (via secret ref), signed URL TTL |
| `JwtOptions` | issuer, audience, signing key ref (asymmetric), lifetimes |
| `WhatsAppOptions` | app secret ref, API base, per-channel defaults |
| `TelegramOptions` | API base, webhook base URL |
| `EmailOptions` / `SmsOptions` | provider endpoints + credential refs (future adapters) |
| `OpenTelemetryOptions` | exporter endpoint, sampling, service name |
| `RateLimitingOptions` | per-surface defaults |
| `OutboxOptions` | batch sizes, retry budget, retention |

- Environment variable mapping follows .NET conventions
  (`WASLA_Database__ConnectionString`, etc.); `.env.example` documents placeholders
  only (Phase 1 artifact).
- Raw string configuration access is confined to the composition root.

---

## 4. CI/CD (GitHub Actions)

Repository: `dante127/Wasla`. Pipeline stages (§61), in order; critical failures block
later stages and any deploy:

1. **Restore & build** — `dotnet restore`, `dotnet build -c Release -warnaserror`
2. **Unit tests** — `dotnet test tests/Wasla.UnitTests`
3. **Architecture tests** — dependency rules, provider isolation, endpoint-permission map
4. **Integration tests** — Testcontainers: PostgreSQL + Redis; includes tenant isolation
   suite (release-blocking) and webhook idempotency suite
5. **Security checks** — `dotnet list package --vulnerable --include-transitive`;
   secret scanning (e.g. gitleaks); fails on critical findings
6. **Docker build** — build and tag `wasla-api` image
7. **Migration validation** — generate idempotent SQL script; fail on destructive ops
   without explicit label; apply to a scratch database
8. **Deploy** — staging on `main` (auto), production behind manual approval;
   migrations as a separate pre-deploy step; health probes gate traffic

Never deploy if critical tests fail. Images are versioned (git SHA + semver tag) for
rollback: rollback = redeploy previous image (migrations are written forward-compatible
where feasible; destructive migrations require a documented plan).

---

## 5. Branching & release flow

- Trunk-based: short-lived branches → PR → `main`; `main` is always deployable.
- Phase work lands as coherent commits (`Phase N: <summary>`), matching the roadmap.
- Tags for milestones (`phase-1-foundation`, ...); releases cut from `main`.

---

## 6. Observability (§32)

| Pillar | Implementation |
|--------|----------------|
| Logging | Serilog → structured JSON to stdout (containers), sinks configured per environment; fields: `TenantId`, `UserId`, `CorrelationId`, `RequestId`, `ConversationId`, `ChannelId`, `ProviderMessageId` |
| Tracing | OpenTelemetry traces (ASP.NET Core, HttpClient, EF Core, Npgsql, Redis, custom spans for webhook processing & message send) exported via OTLP |
| Metrics | OpenTelemetry metrics + HTTP server metrics; business metrics below |
| Health | §7 |

Business/technical metrics tracked (§32): HTTP request duration, webhook processing
duration, message sending latency, message failures, queue depth, job failures,
SignalR connections, database duration — plus outbox backlog age and dead-letter count
([events.md §8](events.md#8-operations--observability)).

Monitoring checklist (§62): API availability, database, Redis, queue, webhook failures,
message failures, provider errors, CPU, memory, latency, error rate — each mapped to a
metric/dashboard panel + alert threshold (thresholds set in Phase 9 hardening baseline).

---

## 7. Health checks (§62)

| Endpoint | Semantics | Checks |
|----------|-----------|--------|
| `/health` | Full report (authenticated in production or restricted to ops network) | Aggregates below |
| `/health/live` | Liveness — process is alive | No external dependencies |
| `/health/ready` | Readiness — can serve traffic | PostgreSQL connectivity, Redis connectivity, storage reachability, migrations applied |

Probes: orchestrator liveness → `/health/live`; readiness → `/health/ready`.
Ready flips to unhealthy when critical dependencies fail; failing pods are drained,
alerting fires.

---

## 8. Backups, retention, disaster recovery

- PostgreSQL: managed backups + PITR (staging/prod); restore drill in Phase 9.
- Object storage: lifecycle policy per tenant policy; audit retention per policy.
- Outbox/messages retention windows tracked per [database.md §11](database.md#11-retention-and-growth).
- RPO/RTO targets: defined with the commercial launch (placeholder: RPO ≤ 15 min,
  RTO ≤ 2 h for production).

---

## 9. Operational runbook (initial)

- **Channel down** (`Degraded`): check provider status + channel health job output;
  rotate credentials if invalid; notifications inform admins automatically.
- **Webhook backlog alert**: inspect `inbox_events` backlog, worker health, provider
  outage; workers scale horizontally (safe: claim-based).
- **Outbox dead letters**: inspect error, fix root cause, re-drive (idempotent).
- **Message send failures spike**: check provider error taxonomy distribution; policy
  rejections (24h window) are user-facing, not incidents.
- **Migration failure in deploy**: deployment aborts before app start; database left at
  previous version; restore forward-compatibility plan; never retry blindly.
