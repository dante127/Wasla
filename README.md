# Wasla — وصلة

> **One Inbox. Every Conversation.**

Wasla is a production-grade, multi-tenant, omnichannel CRM SaaS platform. It lets
businesses manage customer conversations from WhatsApp, Telegram, Instagram,
Facebook, Email and SMS inside one unified workspace — with a single Customer 360
view, a unified agent inbox, teams, tickets, tasks, and analytics.

The platform is a **modular monolith** built on **ASP.NET Core 10 / .NET 10**,
**PostgreSQL**, **Redis**, **SignalR** and **S3-compatible object storage**, designed so
that every communication channel plugs in through an independent **channel adapter**.
The core CRM domain never knows which provider a message came from.

**Design rule (non-negotiable):** a channel must never leak its provider-specific
implementation into the core CRM domain. Provider complexity lives at the edges.

---

## Project status

| Phase | Scope | Status |
|-------|-------|--------|
| 0 | Architecture: ADRs, domain model, ERD, boundaries, API contract, security & multitenancy strategy, event model | ✅ Complete |
| 1 | Foundation: solution, projects, PostgreSQL, Redis, configuration, logging, OpenTelemetry, health checks, Docker, CI | ✅ Complete |
| 2 | Identity & Tenancy | ✅ Complete |
| 3 | Customers (Customer 360) | ✅ Complete |
| 4 | Conversations (unified inbox core) | ✅ Complete |
| 5 | WhatsApp channel adapter | ✅ Complete |
| 6 | Telegram channel adapter | ⏳ Next |
| 7 | Real-time (SignalR) | ⏳ Planned |
| 8 | Analytics (basic) | ⏳ Planned |
| 9 | Hardening (security, isolation, perf, failure testing) | ⏳ Planned |

Post-MVP (designed for, not built): Campaigns, Automation, Bot Builder, Commerce,
Billing, AI capabilities, Instagram/Facebook/Email/SMS adapters, advanced analytics.

---

## Repository layout

```text
Wasla/
├── docs/                          # Architecture & engineering documentation
│   └── decisions/                 # Architecture Decision Records (ADRs)
├── src/
│   ├── Wasla.Api/                 # ASP.NET Core host (composition root)
│   ├── Wasla.BuildingBlocks/      # Shared kernel (no business logic)
│   └── Modules/                   # 12 modules × Domain / Application / Infrastructure
├── tests/
│   ├── Wasla.UnitTests/
│   ├── Wasla.IntegrationTests/
│   └── Wasla.ArchitectureTests/
├── docker-compose.yml             # postgres, redis, minio (profile), api
└── .github/workflows/ci.yml       # build, test, docker image
```

The solution structure is defined in
[docs/architecture.md](docs/architecture.md) and locked as decisions in
[docs/decisions/](docs/decisions/README.md).

---

## Quickstart (development)

Requirements: **.NET SDK 10**, **Docker**.

```bash
# 1. Infrastructure (PostgreSQL + Redis)
docker compose up -d postgres redis

# 2. Apply migrations + seed development data
#    (tenant "demo"; users owner@wasla.dev / manager@wasla.dev / agent@wasla.dev; password: Dev@Wasla123)
dotnet run --project src/Wasla.Api -- --seed

# 3. Run the API (development configuration)
dotnet run --project src/Wasla.Api
# health endpoints: /health/live, /health/ready, /health

# Full stack in containers (API at http://localhost:8080)
docker compose up --build
```

Host ports: PostgreSQL on 5433 and Redis on 6379 (5433 avoids clashing with machines that already run PostgreSQL locally on 5432).

Configuration uses strongly-typed options with `WASLA_`-prefixed environment
overrides (see `.env.example`). Development defaults target `localhost` — see
`src/Wasla.Api/appsettings.Development.json`.

---

## Documentation index

| Document | Contents |
|----------|----------|
| [docs/architecture.md](docs/architecture.md) | System overview, modular monolith, module boundaries, solution structure, pipeline, principles |
| [docs/domain-model.md](docs/domain-model.md) | Bounded contexts, aggregates, entities, value objects, invariants, domain events |
| [docs/database.md](docs/database.md) | PostgreSQL model, ERD, tables, constraints, indexing strategy, search, migrations |
| [docs/multi-tenancy.md](docs/multi-tenancy.md) | Tenant model, resolution, enforcement layers, isolation test plan |
| [docs/security.md](docs/security.md) | AuthN/AuthZ, permission catalog, rate limiting, secrets, risk register |
| [docs/channels.md](docs/channels.md) | Channel adapter abstraction, provider isolation, WhatsApp & Telegram mapping |
| [docs/webhooks.md](docs/webhooks.md) | Inbound webhook pipeline, signature verification, idempotency, replay protection |
| [docs/events.md](docs/events.md) | Domain vs integration events, Outbox/Inbox, background jobs, SignalR mapping |
| [docs/api.md](docs/api.md) | API conventions, versioning, errors, pagination, MVP endpoint catalog |
| [docs/deployment.md](docs/deployment.md) | Environments, Docker, configuration, CI/CD, health checks, observability |
| [docs/development.md](docs/development.md) | Prerequisites, workflow, standards, testing strategy, DoD |
| [docs/roadmap.md](docs/roadmap.md) | MVP scope, phase plan, risk register, open questions |
| [docs/decisions/README.md](docs/decisions/README.md) | ADR index (10 initial decisions) |

---

## Architecture principles

1. **Modular monolith first** — one deployable, strict module boundaries, extraction-ready.
2. **Provider isolation** — channel providers exist only inside channel adapters (see [ADR-0005](docs/decisions/0005-channel-adapter-abstraction.md)).
3. **Multi-tenant by construction** — `TenantId` everywhere, defense in depth, mandatory isolation tests (see [ADR-0002](docs/decisions/0002-multi-tenancy-shared-schema.md)).
4. **Correctness under failure** — Outbox + Inbox patterns, idempotency, at-least-once processing with safe consumers (see [ADR-0004](docs/decisions/0004-outbox-inbox-background-workers.md)).
5. **Observable by default** — structured logs, metrics, traces, health checks from day one.
6. **Frontend-agnostic API** — clean REST API (`/api/v1`), DTOs only, RTL/Arabic-ready localization later.

## License

Proprietary — all rights reserved (placeholder; to be finalized).
