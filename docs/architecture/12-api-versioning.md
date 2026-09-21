# API Versioning

## Why every public API is versioned

[`11-deployment-and-availability.md`](11-deployment-and-availability.md)
establishes that services deploy without stopping the system, which means a
rollout routinely has the old and new version of a service answering
requests behind the same endpoint at the same time. On top of that, this
system's public APIs have callers this project doesn't fully control the
release cadence of:

- The **agent app**, already installed on a user's PC/laptop/phone, doesn't
  update the moment a new backend version ships.
- Any **third-party integrator** ([`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md)
  already commits to generating clients for this case) updates on its own
  schedule, not this project's.
- The Admin UI itself is usually in lock-step with its backend, but even
  that isn't guaranteed for the seconds/minutes a rollout is in flight.

A breaking change shipped without a version bump would break whichever of
these happens to be mid-request, or hasn't updated yet. Versioning is what
lets a breaking change exist without that.

This applies to every public-facing API: the **Org (Admin/Tenant) API**, the
**Logs Ingestion API**, and the device-token endpoint
([`03-security-and-identity.md`](03-security-and-identity.md)) — anything an
out-of-cluster client calls directly.

## What "breaking" means here

Not every change needs a new version. A version bump is required only for a
**breaking** change to a request or response contract:

- Removing or renaming a field, or changing its type/required-ness.
- Changing the meaning of an existing enum value, status code, or URL.
- Removing an endpoint, or narrowing what it accepts.

These are **not** breaking, and ship in place on the current version:

- Adding a new optional response field.
- Adding a new endpoint.
- Adding a new optional request field with a sensible default when omitted.

This mirrors how the JSON Schema contracts already work
([`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md)): a
schema's shape only changes when the model's shape actually changes, and a
new version only exists when the previous one can no longer serve a caller
correctly.

## The scheme: URI path versioning

Every versioned endpoint carries its major version in the path:
`/api/v1/...`, `/api/v2/...`. Implemented with **`Asp.Versioning.Http`**
(the actively maintained successor to `Microsoft.AspNetCore.Mvc.Versioning`)
and its URL-segment version reader — a ready-made library per this project's
first principle, not hand-rolled route matching.

- Each microservice's API versions independently of the others — the Org API
  can be at `v2` while the Ingestion API is still at `v1` — consistent with
  every service being independently buildable and deployable
  ([`CLAUDE.md`](../../CLAUDE.md) principle 6). There's no repo-wide "API
  version" to keep in sync.
- Only the **major** version appears in the URI. Non-breaking, additive
  changes don't need a new path and don't force every client to move.
- Clean Architecture stays intact: a version bump changes the
  presentation/API layer's request/response mapping only. The
  domain/application layer behind it has no notion of "v1" or "v2" — it's
  the controllers/endpoint handlers for each version that adapt their
  version's contract onto the same underlying use case.

## Deprecation

A version being phased out is marked obsolete in `Asp.Versioning.Http`
(`Deprecated = true` on that version's `ApiVersion`), which makes every
response from it carry the standard **`Deprecation`** and **`Sunset`**
response headers — machine-readable notice an integrator's tooling can act
on, not just a line in a changelog. The deprecated version keeps working
until its documented sunset date; it isn't removed the moment the next
version ships. A minimum support window (proposed: 6 months) applies to
every public API here, including the Ingestion API — the agent app can't be
assumed to auto-update on every user's machine any faster than a third-party
integrator can.

## Contract and codegen impact

Extends the pipeline in
[`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md):

- Each major version gets its **own OpenAPI document**. Where a schema under
  `src/contracts/json/` hasn't changed between versions, both OpenAPI
  documents reference the *same* schema file — a version bump doesn't imply
  duplicating every unrelated model. Only the models that actually changed
  get a new schema file (e.g. `device.json` stays; a breaking reshape would
  land as `device.v2.json`, referenced only from the `v2` OpenAPI document).
- `openapi-generator` runs once per version per target language, so a
  generated client (TS/Kotlin/Swift/C#) is a client *for a specific major
  version*, matching what a real integrator would pin against.

```
src/contracts/json/*.json  (per-model source of truth, shared across versions
                             where the shape hasn't changed)
        │
        ├──> v1 OpenAPI doc ──> openapi-generator ──> v1 clients
        └──> v2 OpenAPI doc ──> openapi-generator ──> v2 clients
```

## Alternatives considered

- **HTTP header versioning** (e.g. an `Api-Version` request header) — the
  "more RESTful" option in some views, but worse for this project's stated
  goal of staying easy for arbitrary integrators including plain
  `curl`/browser clients ([ADR-0008](../decisions/0008-https-everywhere.md)):
  the version is invisible in the URL, easy to forget, and two different
  versions of the same URL can't be cached or bookmarked separately.
- **Media-type versioning** (`Accept:
  application/vnd.homesecurity.v1+json`) — technically the "purist" REST
  answer, and pairs naturally with HATEOAS content negotiation, but adds a
  second versioning surface on top of the URI once HAL is introduced
  ([`13-hateoas-public-api.md`](13-hateoas-public-api.md)). Rejected in
  favor of keeping exactly one versioning mechanism system-wide.
- **Query-string versioning** (`?api-version=1`) — easy to omit by accident
  (silently falls back to a default), and not distinct enough for HTTP
  caching to key on cleanly. Rejected.

## Consequences

- A breaking change is always a deliberate, visible decision (a new URI
  segment), never something that ships quietly inside what looks like the
  same endpoint.
- Every service carries the small, fixed cost of `Asp.Versioning.Http` plus
  maintaining N and N-1 side by side for the deprecation window — an
  accepted cost, the same trade this project already made for observability
  tooling (ADR-0009) and TLS automation (ADR-0008): buy the well-solved
  problem rather than hand-roll it.
- Recorded as [ADR-0012](../decisions/0012-uri-based-api-versioning.md).

## Related documents

- Why this is needed: [`11-deployment-and-availability.md`](11-deployment-and-availability.md)
- HATEOAS for the Org (Admin/Tenant) API, and how links carry the version:
  [`13-hateoas-public-api.md`](13-hateoas-public-api.md)
- JSON Schema contracts and client codegen: [`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md)
- Decision record: [ADR-0012](../decisions/0012-uri-based-api-versioning.md)
