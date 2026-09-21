# HATEOAS for the Public Integratable API

## Scope: which API this applies to

This system has two public-facing HTTP APIs, and they exist for different
reasons:

| API | Callers | Shape |
|---|---|---|
| **Org (Admin/Tenant) API** | Admin UI, and — per [`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md)'s stated goal — future third-party integrators generating their own TS/Kotlin/Swift clients | Broad: organizations, devices, members, roles, alerts — the whole product surface a caller might want to *navigate* |
| **Logs Ingestion API** | The first-party agent app only | Narrow: accept a batch of log entries, nothing else — a single write, nothing to discover |

HATEOAS applies to the **Org API** — the one this project actually intends
outside parties to integrate against. It deliberately does **not** apply to
the Logs Ingestion API or the device-token endpoint: both are single-purpose
write/auth endpoints with no related resources to link to and no navigation
a hypermedia layer would help with. Adding `_links` there would be pure
overhead for zero benefit — the opposite of this project's first principle.

## Why HATEOAS, concretely

Without it, an integrator (or the Admin UI) has to hand-encode two things
that change independently of the contract itself:

1. **URL structure** — `/api/v1/orgs/{orgId}/devices/{deviceId}` has to be
   built by every client from scratch, and re-encoded by hand if it ever
   changes shape.
2. **What's currently allowed** — whether a device can be "acknowledged"
   right now depends on the caller's permissions
   ([`04-roles-and-permissions.md`](04-roles-and-permissions.md)) and the
   device's own state. Without hypermedia, that business rule has to be
   duplicated in every client that wants to decide whether to show an
   "acknowledge" button.

With HATEOAS, both of those live in the response itself: a client follows a
named link instead of building a URL, and a link's mere presence or absence
*is* the capability check — no client-side reimplementation of
`device.acknowledge`'s permission rule needed.

This also pairs directly with
[`12-api-versioning.md`](12-api-versioning.md): a client that navigates via
links, rather than hardcoding URL templates, is far less exposed to a
version's URI layout changing under it.

## The format: HAL

Responses use **HAL** (`application/hal+json`) — every representation
carries a `_links` object, e.g.:

```json
{
  "id": "3fa2...",
  "name": "Living Room TV",
  "recognized": false,
  "firstSeenAt": "2026-03-01T10:15:00Z",
  "_links": {
    "self": { "href": "/api/v1/orgs/9c1e.../devices/3fa2..." },
    "organization": { "href": "/api/v1/orgs/9c1e..." },
    "acknowledge": { "href": "/api/v1/orgs/9c1e.../devices/3fa2.../acknowledge", "method": "POST" }
  }
}
```

Here, `acknowledge` is only present because this device is currently
unrecognized *and* the caller holds `device.acknowledge` in this org — once
either condition changes, the link disappears rather than the client having
to call an endpoint that would now 403.

A collection follows the same convention with `_links.self` plus pagination
relations (`next`/`prev`), and an `_embedded` object for the items — the
standard HAL collection shape, not a custom one.

### The entry point

`GET /api/v1/orgs/{orgId}` (or a root `GET /api/v1/` before an org is
selected) returns a **discovery** representation whose `_links` point at
every top-level collection the caller can currently reach — `devices`,
`members`, `roles`, `alerts`. A client that starts here and only ever
follows links never needs a single hardcoded URL beyond that one entry
point.

### Versioning interplay

Every `href` already carries the version prefix
(`/api/v1/...`) from [`12-api-versioning.md`](12-api-versioning.md), so a
client following links transparently stays on the version it started on —
HAL doesn't get its own, second versioning scheme (e.g. via the media type);
this project keeps exactly one versioning mechanism, the URI, per that
document's decision.

## Contract-first impact

The per-resource JSON Schema files under `src/contracts/json/` continue to
describe only a resource's own domain fields (`organization.json`,
`device.json`, …) — `_links` is a transport/hypermedia concern layered on
top by the OpenAPI document, not part of the domain shape. This keeps the
same schema file reusable wherever HAL isn't relevant (e.g. a future
internal service-to-service call), and matches how
[`12-api-versioning.md`](12-api-versioning.md) already treats the OpenAPI
document, not the JSON Schema file, as the place a version's full contract
(including its envelope) is expressed.

Link generation itself is not hand-rolled: a small, ready-made HAL
serialization helper for ASP.NET Core (evaluated at implementation time —
e.g. a maintained `Hal.AspNetCore`-style package) builds the `_links` object
from a declarative link map per resource type, rather than string-concatenating
URLs in every endpoint handler. `openapi-generator`'s client output for the
Org API exposes typed link navigation (e.g. `.getLink("devices")`) so a
generated TS/Kotlin/Swift client doesn't hand-parse `_links` either.

## Alternatives considered

- **JSON:API** — a more complete spec (also standardizes includes,
  relationships, sparse fieldsets, filtering), but noticeably heavier to
  implement and consume than this system needs for its current surface.
  Revisit only if the Org API grows complex-enough relationship querying
  needs that JSON:API's `include` mechanism would actually pay for itself.
- **Siren** — supports the same links *and* explicit "actions" (method,
  fields, etc.), which is a genuinely closer match to the
  permission-gated-action use case above, but it's a much less common
  format with weaker tooling/client-library support than HAL. Revisit if
  action affordances (not just navigation links) become a bigger part of
  the API than they are today.
- **A custom, ad-hoc `links` array with no spec behind it** — rejected
  outright; it would reinvent something already standardized, which is
  exactly what this project's first principle says not to do.
- **No hypermedia at all (plain REST + the OpenAPI doc as the only
  discovery mechanism)** — the simplest option, and genuinely fine for the
  Ingestion API's single endpoint. Rejected specifically for the Org API
  because that's the surface this project has already committed to
  generating multi-language clients for third parties against, and that's
  precisely the case hypermedia's decoupling benefit is for.

## Consequences

- Admin UI and any generated third-party client navigate the Org API via
  links rather than hardcoded URL templates, reducing coupling to a given
  version's exact URI layout.
- Permission checks that used to be duplicated client-side (per
  [`04-roles-and-permissions.md`](04-roles-and-permissions.md)) collapse
  into "is this link present in the response," removing a class of
  client/server drift.
- A small, fixed payload-size and implementation cost (the `_links` object
  on every representation) — accepted the same way this project already
  accepts the JSON Schema/OpenAPI pipeline's cost in
  [`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md): buy
  the discoverability once, centrally, instead of every client re-deriving
  it.
- Recorded as [ADR-0013](../decisions/0013-hateoas-for-org-api.md).

## Related documents

- Why the Org API specifically, and not the Ingestion API: this document,
  above.
- API versioning (how a link's `href` stays version-stable):
  [`12-api-versioning.md`](12-api-versioning.md)
- Roles/permissions (what gates a state-dependent action link):
  [`04-roles-and-permissions.md`](04-roles-and-permissions.md)
- JSON Schema contracts and client codegen: [`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md)
- Decision record: [ADR-0013](../decisions/0013-hateoas-for-org-api.md)
