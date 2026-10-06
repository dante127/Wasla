# Wasla — API Contract

The API is the product surface for the future Next.js/React frontend and mobile clients.
It is RESTful, versioned, DTO-based, and frontend-agnostic (§34, §50, §52).
This document defines conventions, error handling, pagination, and the MVP endpoint
catalog. FluentValidation rules and exact DTO shapes are finalized per phase; this file
is the contract skeleton.

---

## 1. Conventions

| Topic | Rule |
|-------|------|
| Base path | `/api/v1/...` (versioning in path; breaking changes → `/v2`) |
| Media type | `application/json`; errors as `application/problem+json` |
| Naming | camelCase properties; kebab/camel resource names; plural nouns |
| IDs | UUID strings |
| Enums | Serialized as strings (`"Open"`), parsed case-insensitively |
| Timestamps | ISO 8601 UTC (`2026-10-06T07:19:00Z`) |
| DTOs only | EF entities are never exposed; request/response DTOs are explicit types |
| No wrappers | No `{ success, data }` envelopes (§52); standard HTTP semantics |
| Localization | `Accept-Language` respected for user-facing messages; business `code`s in error extensions are stable/language-neutral |
| Idempotency | `Idempotency-Key` header on non-idempotent sends (§17) |
| OpenAPI | Generated OpenAPI 3 document for all endpoints; UI in non-production environments |

---

## 2. Errors — RFC 9457 ProblemDetails (§33)

```json
{
  "type": "https://wasla.app/errors/conversation-not-found",
  "title": "Conversation not found",
  "status": 404,
  "detail": "The conversation does not exist or you do not have access to it.",
  "instance": "/api/v1/conversations/3f2a...",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": []
}
```

Rules:

- `type` URIs are stable documentation links (`https://wasla.app/errors/{code}`);
  the machine-readable `code` also appears in `extensions.code` when useful.
- Validation failures: `400` with `errors` populated as `{ "field": ["message", ...] }`.
- Auth failures: `401` (no/expired token), `403` (valid token, insufficient permission —
  only when resource existence is not secret), `404` for cross-tenant/unguessable
  resources (no existence leak, [multi-tenancy.md](multi-tenancy.md#8-mandatory-isolation-test-plan-48-of-spec)).
- Conflict (concurrency, duplicates): `409`. Payload too large: `413`. Throttled: `429` + `Retry-After`.
- Internal exceptions **never** leak: unhandled → generic `500` ProblemDetails with `traceId`;
  details go to logs/traces only.

---

## 3. Authentication

- `Authorization: Bearer <access-token>` on all endpoints except: auth endpoints,
  webhook ingress, health endpoints.
- Permission requirements are listed per endpoint below; enforcement is centralized
  ([security.md §5](security.md#5-authorization-architecture)).
- Cross-tenant access attempts behave as `404` (see §2).

---

## 4. Pagination (§35)

**Admin-style lists** (users, customers, tickets, boards, notifications):

```text
GET /api/v1/customers?page=1&pageSize=50&sort=-updatedAt
→ { "items": [...], "page": 1, "pageSize": 50, "totalCount": 132 }
```

**High-volume streams** (conversations, messages) use **cursor pagination**:

```text
GET /api/v1/conversations?limit=30&cursor=eyJ0IjoiMjAyNi0xMC0wNlQwNzoxOTowMFoiLCJpIjoiLi4uIn0
GET /api/v1/conversations/{id}/messages?limit=50&before=<cursor>
→ { "items": [...], "nextCursor": "..." | null }
```

Rules: `limit` defaults/ceilings per resource; cursors are opaque (base64 of ordering
key + tie-breaker id); offset pagination is forbidden for messages/streams.

---

## 5. Filtering & sorting

- Simple equality filters as query params: `status=Open`, `channelId=...`.
- Multi-value: `tagIds=id1,id2` (comma-separated).
- Ranges: `dateFrom`, `dateTo` (ISO 8601), `unread=true`.
- Search: `q` (server-side FTS/trigram where applicable).
- Sorting: `sort=-lastMessageAt` (leading `-` = descending); allowed sort fields are
  documented per endpoint; unknown fields → `400`.

---

## 6. Idempotency

- `Idempotency-Key` (client-generated UUID) required on message send and other
  non-idempotent POSTs invoked by agents.
- Scope: `(TenantId, EndpointKey, IdempotencyKey)`; stored with the created response.
- Replays return the **original response** (same status/body), never duplicate effects.
- Missing key on required endpoints → `400` with `code: idempotency_key_required`.

---

## 7. Endpoint catalog (MVP)

Notation: **P** = required permission; `⧗` = cursor pagination; `⌛` = idempotency key.

### Auth & session
| Method | Path | P | Notes |
|--------|------|---|-------|
| POST | `/auth/login` | — | Email+password (if local auth) → tokens + memberships |
| POST | `/auth/refresh` | — | Rotating refresh |
| POST | `/auth/logout` | auth | Revokes refresh token(s) |
| GET | `/auth/me` | auth | Profile, memberships, effective permissions |

### Users, roles, teams
| Method | Path | P |
|--------|------|---|
| GET | `/users` (page) | `user.read` |
| POST | `/users` (invite) | `user.create` |
| PATCH | `/users/{id}` | `user.update` |
| DELETE | `/users/{id}` | `user.delete` |
| PUT | `/users/{id}/roles` | `user.update` |
| GET | `/roles` | `user.read` |
| GET/POST/PATCH | `/teams`, `/teams/{id}` | `team.read` / `team.manage` |
| PUT | `/teams/{id}/members` | `team.manage` |

### Customers (Customer 360)
| Method | Path | P |
|--------|------|---|
| GET | `/customers` (page, `q`, `tagIds`) | `customer.read` |
| POST | `/customers` | `customer.update` |
| GET | `/customers/{id}` | `customer.read` |
| PATCH | `/customers/{id}` | `customer.update` |
| GET | `/customers/{id}/timeline` (page) | `customer.read` |
| GET | `/customers/{id}/conversations` (⧗) | `conversation.read` |
| POST | `/customers/{id}/identities` | `customer.update` |
| POST | `/customers/{id}/notes` | `customer.update` |
| POST/DELETE | `/customers/{id}/tags[/{tagId}]` | `customer.update` |
| POST | `/customers/{id}/merge` | `customer.update` (Phase 9+; audited) |

### Conversations (unified inbox)
| Method | Path | P | Notes |
|--------|------|---|-------|
| GET | `/conversations` (⧗) | `conversation.read` | Filters: `channelId`, `teamId`, `assignedUserId`, `status`, `priority`, `tagIds`, `unread`, `customerId`, `dateFrom`, `dateTo`, `q` |
| POST | `/conversations` | `conversation.create` | Agent-initiated outbound conversation |
| GET | `/conversations/{id}` | `conversation.read` | |
| PATCH | `/conversations/{id}` | `conversation.update` | status/priority changes (audited) |
| GET | `/conversations/{id}/messages` (⧗ `before`/`after`) | `message.read` | |
| POST | `/conversations/{id}/messages` | `message.send` | ⌛; text/media send |
| POST | `/conversations/{id}/assign` | `conversation.assign` | `{userId?}` or `{teamId?}` |
| POST | `/conversations/{id}/tags` + DELETE `/tags/{tagId}` | `conversation.update` | |
| POST | `/conversations/{id}/resolve` / `/reopen` / `/close` | `conversation.update` | status transitions |
| POST | `/conversations/{id}/notes` | `conversation.update` | internal notes + mentions |
| POST | `/conversations/{id}/read` | `conversation.read` | mark read (per user) |

### Tags & quick replies
| Method | Path | P |
|--------|------|---|
| GET/POST/PATCH | `/tags`, `/tags/{id}` | `conversation.read` / `tag.manage` |
| GET/POST/PATCH/DELETE | `/quick-replies[/{id}]` | `quickreply.read` / `quickreply.manage` |

### Channels
| Method | Path | P |
|--------|------|---|
| GET | `/channels` | `channel.read` |
| POST | `/channels` (connect; type-specific body) | `channel.manage` |
| POST | `/channels/{id}/verify` | `channel.manage` |
| PATCH/DELETE | `/channels/{id}` | `channel.manage` |

### Tickets
| Method | Path | P |
|--------|------|---|
| GET | `/tickets` (page, filters) | `ticket.read`-equivalent: `conversation.read` for MVP demo? → use `ticket.update`/read via `report.read`? **Decision:** add read alias `ticket.read` in Phase 2 catalog extension |
| POST | `/tickets` | `ticket.create` |
| GET/PATCH | `/tickets/{id}` | `ticket.read` / `ticket.update` |
| POST | `/tickets/{id}/close` | `ticket.close` |

*(Note: the catalog keeps `ticket.read` as a read code to pair with the spec's
`ticket.create/update/close`; recorded for Phase 2 role seeding.)*

### Tasks (boards)
| Method | Path | P |
|--------|------|---|
| GET/POST/PATCH | `/boards`, `/boards/{id}` | `task.read` / `task.manage` |
| POST | `/boards/{id}/columns` | `task.manage` |
| PATCH | `/columns/{id}` (reorder) | `task.manage` |
| POST | `/columns/{id}/cards` | `task.manage` |
| PATCH | `/cards/{id}` (move: `columnId` + `rank`) | `task.manage` |
| POST | `/cards/{id}/comments` | `task.manage` |

### Notifications
| Method | Path | P |
|--------|------|---|
| GET | `/notifications` (page) | auth (self) |
| POST | `/notifications/{id}/read`, `/notifications/read-all` | auth (self) |

### Reports (basic analytics)
| Method | Path | P |
|--------|------|---|
| GET | `/reports/overview?from&to` | `report.read` |
| GET | `/reports/agents?from&to` | `report.read` |
| GET | `/reports/channels?from&to` | `report.read` |

### Audit
| Method | Path | P |
|--------|------|---|
| GET | `/audit?from&to&entityType&entityId&actorUserId` (page) | `audit.read` |

### Webhooks (public — provider-facing)
| Method | Path | Notes |
|--------|------|---|
| GET/POST | `/webhooks/whatsapp/{channelId}` | verify + receive ([webhooks.md](webhooks.md)) |
| POST | `/webhooks/telegram/{channelId}` | receive |

### Health & realtime (outside `/v1` semantics)
| Method | Path | Notes |
|--------|------|---|
| GET | `/health`, `/health/live`, `/health/ready` | probes ([deployment.md](deployment.md#7-health-checks)) |
| WS | `/hubs/realtime` | SignalR ([events.md §6](events.md#6-real-time-mapping-signalr)) |

---

## 8. Representative payloads

**Conversation list (cursor):**

```json
{
  "items": [
    {
      "id": "0b1f4e5a-...",
      "status": "Open",
      "priority": "High",
      "customer": { "id": "...", "displayName": "Layla H." },
      "channel": { "id": "...", "type": "WhatsApp", "displayName": "Support line" },
      "assignedUserId": "...",
      "assignedTeamId": null,
      "tags": [{ "id": "...", "name": "VIP", "color": "#7c3aed" }],
      "lastMessageAt": "2026-10-06T07:12:44Z",
      "lastMessagePreview": "Is my order ready?",
      "unreadCount": 2
    }
  ],
  "nextCursor": "eyJ0IjoiMjAyNi0xMC0wNi0wNzoxMjo0NFoiLCJpIjoiMGIxZjRlNWEifQ"
}
```

**Conversation detail:** same shape plus `slaDueAt`, `firstResponseAt`, `resolvedAt`,
`participants`, and `customer` summary expanded.

**Send message:**

```json
POST /api/v1/conversations/{id}/messages
Idempotency-Key: 5f0c2a1e-...

{
  "type": "Text",
  "body": "Hello Layla, your order is ready for pickup."
}
```

```json
201 Created
{
  "id": "c9d3...",
  "direction": "Outbound",
  "type": "Text",
  "status": "Pending",
  "createdAt": "2026-10-06T07:20:31Z",
  "idempotencyKey": "5f0c2a1e-...",
  "attachments": []
}
```

**Validation error:**

```json
{
  "type": "https://wasla.app/errors/validation-failed",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "...",
  "errors": {
    "body": ["Message body must be 1–4096 characters."]
  }
}
```

---

## 9. Compatibility & evolution

- Additive changes within `v1`; new required request fields are not allowed in `v1`.
- Deprecation: `Deprecation`/`Sunset` headers + docs, minimum one release window.
- Webhook ingress contract is provider-defined and versioned by providers, isolated
  behind adapters ([channels.md](channels.md)).
- The OpenAPI document is the living contract for frontend generation (typed clients).
