# Wasla — Development Guide

How we build Wasla: local setup, standards, testing strategy, and the Definition of
Done. This file grows as Phases land; it is the reference for every contributor
(and every agent session working this repository).

---

## 1. Prerequisites (§46, §61)

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | **10.0** | Install before Phase 1 (the current dev machine has runtime only — SDK required) |
| Docker Desktop | current | Compose runs postgres/redis/minio |
| EF Core CLI | `dotnet tool install --global dotnet-ef` | Migrations |
| Git | current | Trunk-based workflow |
| IDE | Visual Studio 2022+ / Rider / VS Code | `.editorconfig` enforced |

---

## 2. Getting started (target flow — artifact of Phase 1)

```bash
git clone https://github.com/dante127/Wasla.git
cd Wasla
cp .env.example .env                 # fill placeholders (never commit real values)
docker compose up -d postgres redis minio
dotnet ef database update            # per-module migrations (script provided)
dotnet run --project src/Wasla.Api    # API + workers
```

Seed data (§49) is applied by a development seeder command (`--seed` flag or a dedicated
tool) — idempotent, synthetic-only:

- Tenant: **Demo Company** (en + ar culture available).
- Users: Owner, Manager, Agent (roles seeded), Teams: Sales, Support.
- Customers: several realistic-but-synthetic profiles with WhatsApp & Telegram
  identities; conversations across both channels; tags: VIP, New Lead, Complaint.

---

## 3. Solution conventions

- Structure and dependency rules: [architecture.md §6–§7](architecture.md#6-module-boundaries-and-dependency-rules); they are enforced by architecture tests — a PR that violates them fails CI.
- Module pattern: `X.Domain` (aggregates, VOs, domain events) → `X.Application`
  (commands/queries/handlers, validators, **Contracts**) → `X.Infrastructure`
  (EF mappings, external integrations). Host wires everything via `IModule`.
- New module checklist: projects + namespaces + `DbContext` (own schema) + `IModule`
  registration + architecture test additions + docs touch.
- Providers: only `Wasla.Channels.Infrastructure` may reference provider SDKs.

---

## 4. Coding standards (§54)

- SOLID, explicit naming, small focused classes; no god services/controllers.
- No `GenericRepository<T>` / `GenericService<T>` / `BaseController<T>` ceremony
  unless justified by real value.
- Domain rules in domain; orchestration in application handlers; I/O in infrastructure.
- Async all the way; `CancellationToken` on every async boundary.
- No magic strings/numbers — constants, options, or enums. Permission codes come from
  the catalog type, not literals scattered in code.
- Errors: `Result`/`Error` for expected failures; exceptions for bugs. Never leak
  exceptions to clients (ProblemDetails pipeline).
- Logging: structured, with scoped fields; follow the never-log list
  ([security.md §8](security.md#8-never-log-list-31)).
- EF: projections for reads, no N+1, no tracked graphs for list screens, explicit
  `AsNoTracking` where appropriate; raw SQL only via approved system queries.
- Commits: `Phase N: <coherent summary>`; small, reviewable increments per §67.

---

## 5. Testing strategy (§48)

| Suite | Scope | Tooling |
|-------|-------|---------|
| **Unit** | Domain rules, value objects, validators, authorization rules, template renderer, adapter mapping (pure parts) | xUnit (+ FluentAssertions-style assertions) |
| **Integration** | API + PostgreSQL + Redis via Testcontainers: auth flows, tenant isolation (mandatory), webhooks (signature, replay, duplicates), message pipeline idempotency, outbox dispatch, pagination | xUnit + `WebApplicationFactory` + Testcontainers |
| **Architecture** | Dependency direction, module isolation, provider SDK isolation, endpoint→permission mapping, no raw SQL in modules | architecture test library (e.g. NetArchTest/ArchUnitNET — chosen in Phase 1) |
| **Security-focused** | The 10-scenario isolation matrix ([multi-tenancy.md §8](multi-tenancy.md#8-mandatory-isolation-test-plan-48-of-spec)); redaction checks | integration + unit |

Rules:

- Critical-path features require integration tests, not only unit tests (§66).
- Test data: synthetic only (§49). Fixtures for provider payloads are anonymized
  recordings ([channels.md §10](channels.md#10-testing-expectations-per-adapter)).
- CI is the arbiter: local green ≠ done; pipeline green on the PR is done.

---

## 6. Definition of Done (§66)

A feature is **not** complete when it compiles. Checklist for every feature:

- [ ] Domain logic exists and encodes the business rules
- [ ] Input validation exists (FluentValidation) at boundaries
- [ ] Authorization exists: endpoint declares permissions; policies enforce
- [ ] Tenant isolation holds: filters + guards + (if query-heavy) isolation test updated
- [ ] Database migration exists and applies cleanly forward
- [ ] API endpoint exists, DTOs only, documented in OpenAPI
- [ ] Error handling: ProblemDetails paths verified for failure modes
- [ ] Logging where useful (with correlation + tenant fields; no sensitive data)
- [ ] Unit tests + integration tests for critical paths exist and pass
- [ ] No obvious N+1 (query review/projection evidence)
- [ ] No secrets committed (CI secret scan)
- [ ] Architecture boundaries valid (architecture tests)
- [ ] Documentation updated (`docs/` + ADR if a decision changed)

---

## 7. Agent working rules (§67)

For any assistant/automation contributing to this repo:

1. Inspect before modifying — read the module, contracts, and tests first.
2. Make small coherent changes; don't mix unrelated refactors into a phase change.
3. Build after significant changes; fix compilation errors immediately.
4. Run the relevant test suites; report commands and results honestly.
5. Do not hide warnings; treat warnings as errors in CI builds.
6. Do not silently change requirements; record deviations explicitly.
7. Do not introduce dependencies without justification (spec stack is the baseline).
8. Explain important architectural decisions via ADRs.
9. Prefer simple production-grade solutions over clever ones.
10. If a **requirement conflict** appears: STOP and surface it before destructive
    changes.

---

## 8. ADR process

- One decision per file in `docs/decisions/`; template: Context / Decision /
  Consequences / Status (plus Alternatives when relevant).
- Numbering: `NNNN-slug.md`, next free number; index table in
  [decisions/README.md](decisions/README.md) must be updated in the same commit.
- Status lifecycle: `Accepted` → `Superseded by ADR-XXXX` (never delete history).

---

## 9. Documentation duties (§68)

`docs/` is part of the product. When behavior changes, the corresponding document
(architecture, domain-model, database, security, channels, webhooks, events, api,
deployment, development) is updated in the same PR. Reports and phase evidence are kept
outside the repo (delivery artifacts), while durable knowledge lives here.
