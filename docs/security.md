# Wasla — Security

Production-grade security is a first-class requirement (§31 of the specification).
This document defines authentication, authorization, the permission catalog, rate
limiting, secrets handling, data protection, and the security risk register.

Related: [multi-tenancy.md](multi-tenancy.md), [webhooks.md](webhooks.md),
[ADR-0007](decisions/0007-authn-jwt-oidc-permission-authorization.md).

---

## 1. Principles

1. **Deny by default** — every endpoint declares required permissions explicitly;
   missing declaration fails closed (architecture test enforces endpoint→permission
   mapping).
2. **Centralized authorization** — policies and handlers, one permission vocabulary;
   no scattered role checks in handlers or endpoints.
3. **Least privilege** — granular permission codes; roles are seeded bundles; custom
   roles later.
4. **Defense in depth** — authn, authz, tenant guards, DB constraints, tests
   ([multi-tenancy.md §4](multi-tenancy.md#4-enforcement-layers-defense-in-depth)).
5. **No secrets in source control** — environment/secret manager only (§45).
6. **Redaction everywhere** — logs, errors, and audit payloads never contain tokens,
   secrets, or message bodies beyond what the domain requires.

---

## 2. Authentication

| Aspect | Decision |
|--------|----------|
| Protocol | JWT access tokens (short-lived, 15 min) issued by Wasla Identity; OIDC-ready architecture (external IdP can be added per tenant later) |
| Claims | `sub` (user), `tenant_id`, `membership_id`, `perm` (permission codes snapshot), `jti`, standard time claims |
| Refresh tokens | Opaque, stored **hashed**, **rotating, one-time use**; reuse of a rotated token revokes the chain and raises `RefreshTokenReuseDetected` (audited, notified to the user) |
| Local passwords | If enabled: hashed with ASP.NET Core Identity password hasher (PBKDF2-SHA256, per-user salt, high iteration count); policy enforced at registration/change |
| Login protection | Strict rate limiting + temporary lockout after repeated failures; failures audited |
| Token lifetime | Access 15 min; refresh sliding up to a configured maximum session age; logout revokes refresh token(s) |
| Token storage (clients) | Web: memory + refresh via secure cookies is a frontend decision; API contract is bearer-token based |

Notes:

- The `perm` claim is an optimization; the server re-validates membership status and
  permission policies against the current role model for sensitive operations
  (stale-token protection). Permission *changes* are effective immediately for
  write-sensitive checks via a short permission cache with explicit invalidation.
- Multi-tenant: a token is scoped to exactly one tenant (`tenant_id`). Switching
  tenants re-issues tokens after membership validation.

---

## 3. Permission catalog

Closed catalog (codes are part of the API contract; typos are bugs):

| Group | Codes |
|-------|-------|
| Conversations | `conversation.read`, `conversation.create`, `conversation.update`, `conversation.assign`, `conversation.delete` |
| Messages | `message.read`, `message.send`, `message.delete` |
| Customers | `customer.read`, `customer.update`, `customer.export` |
| Tickets | `ticket.create`, `ticket.update`, `ticket.close` |
| Tasks | `task.read`, `task.manage` |
| Tags & quick replies | `tag.manage`, `quickreply.read`, `quickreply.manage` |
| Teams | `team.read`, `team.manage` |
| Channels | `channel.read`, `channel.manage` |
| Reports | `report.read` |
| Users & roles | `user.read`, `user.create`, `user.update`, `user.delete` |
| Settings | `settings.manage` |
| Audit | `audit.read` |
| Billing (future) | `billing.read`, `billing.manage` |
| Campaigns (future) | `campaign.read`, `campaign.create`, `campaign.send` |
| Bots (future) | `bot.read`, `bot.create`, `bot.publish` |

Additional rules:

- Some actions are additionally constrained by **resource-based rules** (e.g. an agent
  may only reassign conversations they can access; only team leads manage their team's
  routes) — implemented as policy handlers, still centralized.
- The catalog above extends the spec's list with the minimum codes needed by MVP
  modules (tasks, teams, channels, tags, quick replies, audit); it remains a **closed
  set** validated by tests.

---

## 4. Roles → permissions (seeded defaults)

Legend: ● full, ◐ subset (read or limited), — none.

| Permission | Owner | Admin | Manager | Agent | Support | Marketing | Viewer |
|-----------|:-----:|:-----:|:-------:|:-----:|:-------:|:---------:|:------:|
| conversation.read | ● | ● | ● | ● | ● | ◐ | ● |
| conversation.create/update | ● | ● | ● | ● | ◐ | — | — |
| conversation.assign/delete | ● | ● | ● | ◐ | — | — | — |
| message.read/send | ● | ● | ● | ● | ● | ◐ | ● |
| message.delete | ● | ● | ◐ | — | — | — | — |
| customer.read | ● | ● | ● | ● | ● | ● | ● |
| customer.update/export | ● | ● | ● | ◐ | ◐ | ◐ | — |
| ticket.* | ● | ● | ● | ◐ | ● | — | ◐ |
| task.* | ● | ● | ● | ◐ | ◐ | ◐ | ◐ |
| tag.manage / quickreply.* | ● | ● | ● | ◐ | ◐ | ◐ | — |
| team.read / team.manage | ● | ● | ◐ | — | — | — | ◐ |
| channel.read / channel.manage | ● | ● | ◐ | — | — | — | — |
| report.read | ● | ● | ● | ◐ | ◐ | ● | ● |
| user.* | ● | ● | ◐ | — | — | — | — |
| settings.manage | ● | ● | — | — | — | — | — |
| audit.read | ● | ◐ | ◐ | — | — | — | — |
| billing.* (future) | ● | ◐ | — | — | — | — | — |

Exactly how ◐ subsets resolve is pinned in Phase 2 implementation (role seeding) and
covered by authorization unit tests. Rules that must hold:

- Owner role includes every permission.
- Only Owner/Admin can manage channels and users.
- Viewer has read-only permissions everywhere (no write code path is reachable).

---

## 5. Authorization architecture

- Permission policies: `HasPermissionRequirement("conversation.read")` +
  `PermissionAuthorizationHandler` resolving the current user's effective permissions
  from claims + membership (with cache). Endpoints declare requirements via endpoint
  metadata; missing metadata for a non-anonymous endpoint fails an architecture test.
- Tenant membership check is implicit in every handler (context must be a real tenant
  context with an active membership).
- Resource-scoped checks use the same policy infrastructure with resource data
  (e.g. assignment guard), keeping logic out of endpoints/controllers.
- Service-to-service/system operations use dedicated system scopes, audited
  ([multi-tenancy.md §4](multi-tenancy.md#4-enforcement-layers-defense-in-depth)).

---

## 6. Rate limiting matrix (§59)

| Surface | Key | Suggested default (configurable) |
|---------|-----|----------------------------------|
| Login / refresh | IP + account | 10/min per IP, burst 5; stricter lockout on failures |
| Public API (general) | tenant + user | 300/min |
| Message sending | tenant + channel | Provider-aware quotas; e.g. WhatsApp tier-driven, configured per channel |
| Webhooks (inbound) | channel | Sized to provider delivery bursts; never user-throttled |
| Expensive endpoints (exports, reports) | tenant + user | 10/min, queued where possible |
| Admin ops (user/channel changes) | tenant + user | 30/min |

Rules: limits are enforced at the edge (ASP.NET rate limiting middleware + Redis-backed
counters for multi-instance correctness later); limits never apply to health endpoints;
429 responses use `ProblemDetails` + `Retry-After`.

---

## 7. Secrets management

- **Never in source control** (§45): API keys, client secrets, JWT signing keys,
  DB passwords, webhook secrets, payment secrets, SMTP credentials.
- Sources: environment variables (development container) and a secret manager in
  staging/production (e.g. cloud secret store / vault; final choice is a deployment
  decision). `.env.example` documents **placeholders only** (Phase 1 artifact).
- JWT signing: asymmetric (RS256/ES256) keys via secret store; key-ids in token headers
  enable rotation without downtime.
- Channel credentials (WhatsApp tokens, Telegram bot tokens, webhook secrets) are
  encrypted at rest with the ASP.NET Data Protection key ring (or KMS-backed provider),
  stored as references on `Channel` ([domain-model.md §10](domain-model.md#10-channels-module));
  decrypted only inside adapter infrastructure at call time.
- Secret handling rules: no secrets in logs, error messages, telemetry attributes, or
  crash dumps; redaction middleware for known-sensitive fields.

---

## 8. Never-log list (§31)

Never logged at any level: passwords, access tokens, refresh tokens, API secrets,
payment secrets, full webhook payloads (raw stored separately with retention), full
message bodies of customer conversations (log ids/types/sizes only).

Enforcement: a logging policy + Serilog destructuring policy that redacts known
sensitive types; code review checklist; integration test asserting redaction for a
sample auth flow.

---

## 9. Data protection

| Area | Approach |
|------|----------|
| Transport | TLS everywhere (HSTS at the edge; redirect HTTP→HTTPS) |
| At rest | Database and object storage encrypted at the infrastructure level; encrypted volume for secrets |
| Field-level | Channel credentials and webhook secrets encrypted (above); future: customer contact encryption keys reviewed per region |
| PII discipline | Only data needed for support is stored; exports are permissioned (`customer.export`) and audited; no PII in logs |
| Media | Served via short-lived signed URLs; buckets private; tenant-prefixed keys |
| Backups | Encrypted; restore procedure tested in hardening phase |
| Localization data | Tenant default culture used for formatting only; no hidden PII in exported labels |

---

## 10. Webhook security

Fully specified in [webhooks.md](webhooks.md): signature validation (constant-time),
replay protection, idempotency, size limits, redacted logging, per-channel secrets.

---

## 11. Secure file handling (§22)

- Validate MIME type (content sniffing, not just headers), extension allowlist, and size
  limits per tenant plan before accepting a file.
- Store with opaque keys; never trust or echo user-provided filenames.
- Malware scanning integration point: `ScanStatus` on `MediaFile`
  (`Pending → Clean | Infected | Unknown`); infected files are quarantined (no signed
  URL issuance), `AttachmentStored` blocked from delivery paths and an alert raised.
- Content-Disposition and content-type served from stored metadata, not user input.

---

## 12. Browser/API hardening

- **CORS**: allowlist per environment (the Next.js frontend origins); no wildcard with
  credentials.
- **CSRF**: bearer-token API → CSRF not applicable; if refresh tokens are delivered in
  cookies (frontend choice), SameSite=Lax/Strict + anti-CSRF for auth endpoints is
  required by contract.
- **Headers**: standard security headers at the API edge (HSTS, X-Content-Type-Options,
  Referrer-Policy, frame-ancestors via CSP where applicable).
- **Input validation**: FluentValidation at the boundary; max lengths/fan-outs on all
  collections; no mass assignment from DTOs.
- **Output**: no EF entities on the wire; DTOs only; error messages curated
  (ProblemDetails templates).

---

## 13. Audit logging (§30)

Security-sensitive actions produce append-only audit entries: user/role changes,
channel connect/disconnect, conversation assignment, customer export, campaign
actions (future), settings changes, billing changes (future), system access.
Details in [domain-model.md §14](domain-model.md#14-audit-module) and
[database.md §8](database.md#8-audit--append-only-enforcement).

---

## 14. Security risk register

| # | Risk | Impact | Mitigation | Enforced at |
|---|------|--------|------------|-------------|
| 1 | Cross-tenant data access | Critical | Six-layer isolation + mandatory test suite | Multi-tenancy §4, §8 |
| 2 | Webhook forgery / replay | High | Signature verification, dedupe, size limits | webhooks.md |
| 3 | Refresh token theft | High | Rotation + reuse detection + revocation | Identity module |
| 4 | Token theft (XSS etc.) | High | Short-lived access tokens, CORS, headers | Security §2, §12 |
| 5 | Privilege escalation via role editing | High | Last-owner protection, audit, permission catalog validation | Identity module |
| 6 | Media upload abuse (payloads, scanning bypass) | High | Validation, scanning states, quarantine, limits | §11 |
| 7 | Template injection in quick replies | Medium | Allowlist variable renderer, no expressions | Conversations module |
| 8 | Enumeration via error differences (IDOR) | Medium | 404 semantics for foreign resources, curated errors | §12 |
| 9 | Rate-limit bypass via multi-tenancy | Medium | Limits partitioned per tenant + user + IP | §6 |
| 10 | Secret leakage via logs/config | High | Never-log list, redaction, secret manager, CI secret scan | §7, §8 |
| 11 | Dependency vulnerabilities | Medium | CI dependency audit (`dotnet list package --vulnerable`), Dependabot-style updates | deployment.md CI |
| 12 | SSRF via future HTTP-request automation nodes | High (future) | Egress allowlist policy when Automation ships; design-time note | Roadmap |

---

## 15. SDLC security in CI

- Secret scanning on every PR (fails on detected credentials).
- Dependency vulnerability audit on every build; critical findings block release.
- No secrets in test fixtures — only synthetic data (§49).
- Security-relevant changes require a second reviewer (channels, identity, tenancy,
  webhooks).
