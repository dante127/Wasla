# ADR-0010: PostgreSQL full-text search first, behind a search port

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Architecture (Phase 0)

## Context

Wasla needs search early (customers, conversations, later messages) but should not
introduce operational complexity before scale demands it. Elasticsearch/OpenSearch
adds infrastructure, sync pipelines, and failure modes; the product spec explicitly
says: start with PostgreSQL, do not add Elasticsearch initially.

## Decision

- Implement search on **PostgreSQL**: full-text search (`tsvector` + GIN) and
  **pg_trgm** for fuzzy/partial matching (customer names, phone digits), always
  tenant-scoped in the predicate.
- Access search through an application port (`ISearchService`); query-side code never
  embeds raw SQL search directly in modules.
- Message-content search ships in a later phase using the same mechanism scoped per
  conversation/tenant.
- **Revisit triggers** (documented): search latency > budget at realistic data volume,
  cross-entity relevance requirements beyond SQL capabilities, or analytics-style
  faceting demands → introduce a dedicated engine behind the same port without domain
  changes.

## Consequences

**Positive**
- No new infrastructure; one source of truth; transactional consistency between data
  and search results.
- Arabic + English text handled via appropriate configurations and normalization
  (documented per index).

**Negative / costs**
- Ranking features are more limited than dedicated engines (acceptable for MVP scope).
- Index maintenance (GIN write cost) requires awareness; generated columns keep sync
  automatic.

## Alternatives considered

- **Elasticsearch/OpenSearch now** — rejected: operational cost, sync complexity,
  explicitly discouraged by the product spec for this stage.
- **LIKE/ILIKE only** — rejected: insufficient relevance and performance at scale;
  trigram + FTS cover both fuzzy and ranked needs.
