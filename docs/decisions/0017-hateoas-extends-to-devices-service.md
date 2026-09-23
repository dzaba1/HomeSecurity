# ADR-0017: HATEOAS extends to Devices.Service's admin API

## Status

Accepted

## Context

[ADR-0013](0013-hateoas-for-org-api.md) requires HAL for "the Org
(Admin/Tenant) API" and names exactly two plain-JSON exceptions that
existed at the time: the Logs Ingestion API and the device-token endpoint.
`Devices.Service` (see
[ADR-0016](0016-devices-service-owns-its-own-database.md)) adds a second,
physically separate service with its own admin-facing CRUD surface
(`Router`/`Device` management) — ADR-0013's text never said whether that
requirement was meant to cover only the one service that existed when it
was written, or "the Org/Admin-Tenant API" as a category. Left unresolved,
a future reader could reasonably assume either answer, and it's a real
scope decision worth one paragraph in the audit trail rather than a
silent, undocumented choice made once inside `Devices.Service`'s code.

## Decision

ADR-0013's HAL requirement is read as applying to **any admin-facing,
Admin-UI-navigated resource API**, not just the one service it was written
against. Concretely:

- `Devices.Service`'s admin CRUD (`/api/v1/orgs/{orgId}/routers`,
  `/api/v1/orgs/{orgId}/devices`) returns HAL, following the exact same
  `_links`/`_embedded` envelope and link-generation approach `Org.Service`
  already uses.
- `Devices.Service`'s agent-facing endpoint
  (`GET /devices/{deviceId}/router-config`) stays plain JSON, joining
  ADR-0013's existing list of narrow, single-purpose, first-party-only
  exceptions (alongside the Logs Ingestion API and the device-token
  endpoint) — same reasoning: nothing to navigate, no benefit from a
  hypermedia envelope.
- `Org.Service`'s new `GET /api/v1/orgs/{orgId}/access-context` (see
  ADR-0016) is also plain JSON for the same reason: it's a
  service-to-service integration point an agent or browser client never
  calls, not a resource anyone navigates to.

## Consequences

- Every future admin-facing service inherits ADR-0013's HAL requirement by
  default, without needing its own ADR to say so — only genuinely
  non-navigable, machine-to-machine endpoints (agent-facing, or
  service-to-service) get the plain-JSON exception, and that exception is
  now explicitly a category, not a fixed list of two services.
- A third-party client or the Admin UI can navigate `Devices.Service`'s
  resources the same way it navigates `Org.Service`'s — no special-casing
  needed at the client layer for which service happens to serve a given
  link.
