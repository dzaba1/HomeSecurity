# ADR-0013: HATEOAS (HAL) for the Org (Admin/Tenant) API

## Status

Accepted

## Context

[`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md)
already commits to generating multi-language clients for third-party
integrators against the Org API — that's the system's actual "public
integratable API," as opposed to the Logs Ingestion API, which exists only
for the first-party agent app to write log batches. Without hypermedia, both
the Admin UI and any third-party client have to hand-encode URL structure
and re-implement the permission-gated business rules from
[`04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md)
about what's currently allowed (e.g. whether a device can be acknowledged
right now). [ADR-0012](0012-uri-based-api-versioning.md) also means multiple
API versions can be live at once, which a client that navigates via links
instead of hardcoded URLs is more resilient to.

## Decision

- The **Org (Admin/Tenant) API** returns **HAL** (`application/hal+json`)
  representations: every resource carries a `_links` object (`self`, related
  resources, and state-dependent action links that are only present when the
  caller is currently permitted and the resource's state allows it).
- A discovery entry point (`GET /api/v1/orgs/{orgId}`) links to every
  top-level collection the caller can currently reach.
- The **Logs Ingestion API** and the device-token endpoint stay **plain
  JSON** — narrow, single-purpose, first-party-only endpoints with nothing
  to navigate and no benefit from a hypermedia envelope.
- Link `href` values carry the version prefix from
  [ADR-0012](0012-uri-based-api-versioning.md); HAL does not get its own
  second versioning scheme via the media type.
- Link generation uses a ready-made HAL serialization helper (evaluated at
  implementation time), not hand-rolled URL string-building, per this
  project's first principle.

## Alternatives considered

- **JSON:API** — a more complete spec (includes, relationships, sparse
  fieldsets, filtering), but heavier than this system's current surface
  needs. Revisit if the Org API grows relationship-querying needs that
  would actually use `include`.
- **Siren** — supports explicit "actions" alongside links, a closer
  conceptual match for the permission-gated-action case, but has
  meaningfully weaker tooling/client-library support than HAL. Revisit if
  action affordances become a bigger part of the API.
- **A custom, ad-hoc `links` array** — rejected outright; reinvents an
  already-standardized wheel.
- **No hypermedia (plain REST + OpenAPI as the only discovery mechanism)** —
  the simplest option and the right one for the Ingestion API's single
  endpoint, but rejected for the Org API specifically because that's the
  surface this project has already committed to generating third-party
  clients against.

## Consequences

- Admin UI and any generated third-party client navigate via links instead
  of hardcoded URL templates, reducing coupling to a version's URI layout.
- Client-side duplication of permission rules (e.g. "can this device be
  acknowledged") collapses into "is the link present."
- A small, fixed payload-size and implementation cost (the `_links` object
  on every Org API representation); no such cost on the Ingestion API,
  which doesn't carry it.
- Full detail in [`13-hateoas-public-api.md`](../architecture/13-hateoas-public-api.md).
