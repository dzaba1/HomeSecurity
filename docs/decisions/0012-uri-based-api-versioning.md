# ADR-0012: URI path versioning for every public API

## Status

Accepted

## Context

[ADR-0011](0011-zero-downtime-rolling-deployments.md) means a rollout
routinely has the old and new version of a service answering requests behind
the same endpoint at once. On top of that, the system's public APIs — the
Org (Admin/Tenant) API, the Logs Ingestion API, and the device-token endpoint
([`03-security-and-identity.md`](../architecture/03-security-and-identity.md))
— have callers this project doesn't fully control the update cadence of: the
agent app already installed on a user's machine, and future third-party
integrators ([`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md)).
A breaking contract change without a version boundary would break whichever
caller happens to hit it mid-rollout, or simply hasn't updated yet.

## Decision

- Every public endpoint carries its **major version in the URI path**:
  `/api/v1/...`, `/api/v2/...`.
- Implemented with **`Asp.Versioning.Http`**'s URL-segment version reader — a
  maintained, ready-made library, not hand-rolled route matching.
- Each microservice versions **independently**: the Org API can be at `v2`
  while the Ingestion API stays at `v1`, matching every service being
  independently buildable and deployable
  ([`CLAUDE.md`](../../CLAUDE.md) principle 6).
- Only breaking changes (removed/renamed/retyped fields, changed
  endpoint/status-code semantics) bump the version; additive changes (new
  optional field, new endpoint) ship in place.
- A version marked obsolete (`Deprecated = true`) emits standard
  **`Deprecation`**/**`Sunset`** response headers and keeps working for a
  minimum support window (proposed: 6 months) before removal.
- Only the presentation/API layer knows about a version; the
  domain/application layer underneath stays version-agnostic (Clean
  Architecture).

## Alternatives considered

- **HTTP header versioning** (`Api-Version` request header) — invisible in
  the URL, easy to omit, and doesn't let two versions of the same URL be
  cached/bookmarked separately — a worse fit for staying `curl`/browser
  friendly per [ADR-0008](0008-https-everywhere.md).
- **Media-type versioning** (`Accept: application/vnd.homesecurity.v1+json`)
  — the more "purist" REST answer, and would pair naturally with HATEOAS
  content negotiation, but introduces a second versioning surface once HAL
  is in play. Rejected in favor of exactly one versioning mechanism system-
  wide — see [ADR-0013](0013-hateoas-for-org-api.md).
- **Query-string versioning** (`?api-version=1`) — too easy to omit by
  accident (silently falls back to a default) and doesn't key HTTP caching
  cleanly by version.

## Consequences

- A breaking change is always a deliberate, visible URI change, never
  something silently reshaping what looks like the same endpoint.
- Each service carries `Asp.Versioning.Http` plus the cost of maintaining N
  and N-1 side by side for the deprecation window.
- Each major version gets its own OpenAPI document (reusing unchanged JSON
  Schema files from `src/contracts/json/` across versions) and its own
  generated clients — extends the pipeline in
  [`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md)
  rather than replacing it.
- Full detail in [`12-api-versioning.md`](../architecture/12-api-versioning.md).
