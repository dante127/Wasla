# ADR-0008: S3-compatible media storage; metadata only in PostgreSQL

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Channels carry images, video, audio, and documents. Storing binaries in PostgreSQL
bloats backups, hurts cache behavior, and complicates serving. The spec is explicit:
never store large media in PostgreSQL; use S3-compatible object storage with validation
and scanning hooks.

## Decision

- Media bytes live in **S3-compatible object storage** (AWS S3, MinIO locally or any
  S3-compatible provider). PostgreSQL stores **metadata only** (`MediaFile`: storage
  key, filename, content type, size, hash, scan status).
- **Private buckets**, tenant-prefixed keys (`tenant/{tenantId}/...`), opaque
  identifiers — user-provided filenames are never trusted for pathing or serving.
- Download/serve via **short-lived signed URLs** issued by the API after authorization.
- Ingestion pipeline validates MIME type (content-based sniffing), extension allowlist,
  and size limits; records `ScanStatus` with a **virus/malware scanning integration
  point** (quarantine on infected).
- All storage access goes through an `IFileStorage` port; providers are swappable
  without touching domain code.

## Consequences

**Positive**
- Database stays lean; backups and migrations stay fast.
- Media serving scales independently; CDN integration is straightforward later.
- Isolation via tenant-prefixed keys + signed URLs integrates with the tenant security model.

**Negative / costs**
- Requires storage infrastructure + lifecycle policies (versions, retention, orphan
  cleanup job for files whose metadata rows were deleted).
- Signed URL TTL trade-off (short TTL vs UX) — handled by on-demand re-issue.

## Alternatives considered

- **Files on app server disk** — rejected: not durable/scalable across instances.
- **Large objects in PostgreSQL** — rejected: explicitly forbidden by product spec;
  operations pain.
- **Public buckets** — rejected: privacy/compliance risk for customer content.
