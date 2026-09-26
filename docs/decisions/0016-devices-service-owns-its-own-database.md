# ADR-0016: Devices.Service owns its own database

## Status

Accepted

## Context

A new microservice, `Devices.Service`, is being introduced to manage
`Router` (encrypted credentials, per
[ADR-0015](0015-centralized-encrypted-router-credentials.md)) and the
network-`Device` concept from
[`06-notifications.md`](../architecture/06-notifications.md). The first draft
put both entities in the same shared Postgres database and shared `Data`
project that `Org.Service` already uses for its own `DeviceCredential`
entity (the agent-auth credential from
[ADR-0004](0004-device-agent-authentication.md), a different thing from the
network `Device` despite the name collision this ADR's implementation also
resolves).

That draft was consistent with how this project's `Data` project is
documented today — "shared across the Org and DeviceAuth services, one
migration history avoids two services racing on schema changes" — but it's
in real tension with independently-deployable microservices: two services
sharing a physical database and migration history can't ship a schema
change independently, can't scale or choose storage differently, and blur
who's allowed to write which table. `Devices.Service` is the point where
this pattern either solidifies as this project's default or gets corrected,
since it's the first genuinely new bounded context added after `Org` and
`LogsIngestion`.

This is a different axis from [ADR-0002](0002-shared-database-multi-tenancy.md):
that ADR is about the *tenant-isolation* strategy (one database, `tenant_id`
column, per service) and stays exactly as-is. This ADR is about whether a
second *service* shares that database, which ADR-0002 never actually
addressed.

## Decision

**`Devices.Service` gets its own database.** `Router`, `Device`, and a new
`AgentRouterBinding` (the "which agent credential polls which router"
pairing link, moved off `DeviceCredential` since a cross-database foreign
key isn't possible) live in `Devices.Service`'s own Postgres database and
migration history, with no database-level foreign key to `Organization` —
`TenantId` is a plain, unconstrained column there, same as any other
cross-service reference.

This has a direct consequence: `Devices.Service` loses direct SQL access to
`Membership`/`Role`/`UserRole`/`RolePermission`, which it needs for tenant
validation and permission checks. Two mechanisms cover that gap:

1. **Synchronous call-through, cached in the already-shared Redis.**
   `Org.Service` stays the sole writer of that data and exposes
   `GET /api/v1/orgs/{orgId}/access-context`, authenticated by forwarding
   the caller's own bearer token (token relay — no new service-account
   auth scheme). `Devices.Service` reads the same `perms:{tenantId}:{userId}`
   Redis cache `Org.Service` already populates, and calls that endpoint
   itself only on a genuine cache miss. See
   [`07-caching-and-idempotency.md`](../architecture/07-caching-and-idempotency.md).
   This is the mechanism `Devices.Service` actually relies on for
   correctness.
2. **Domain events, as a low-cost hedge, not a substitute for (1).** This
   started as coarse `access.changed`/`router.changed`/`device.changed`
   notifications; they were later replaced by one domain event per operation
   (`membership.added`, `role.assigned`, `router.updated`, …) carrying the
   extended data — see
   [ADR-0020](0020-audit-log-and-iso27701-compliance-model.md). Any service
   may subscribe; the audit service is the first. The reasoning is unchanged:
   designing the integration point made the gap obvious and the seam costs
   little to add now, following the same "publish now, consume later"
   precedent `LogsIngestion.Service` already set with `logs.ingested` (see
   [ADR-0007](0007-rabbitmq-as-message-bus.md)). Deliberately not
   event-carried state transfer: a consumer still calls the owning service's
   API for current data, never reconstructs state from the event stream
   alone.

An alternative considered and rejected for now: **event-driven replication**
— `Devices.Service` consuming `Org.Service`'s membership/role-change events
to maintain its own local read copy, fully decoupling runtime availability.
Rejected because it's meaningfully more machinery (a real consumer,
eventual-consistency handling) than this service's initial CRUD-only scope
calls for. The events published under (2) exist partly so this option stays
open without further changes to `Org.Service` if it's ever taken.

## Consequences

- `Router`/`Device`/`AgentRouterBinding` have no DB-level FK to
  `Organization`, `DeviceCredential`, or any other Org.Service-owned table.
  `AgentRouterBinding.DeviceCredentialId` is an opaque cross-service
  reference (the agent's device-token `device_id` claim), not a real
  foreign key — referential integrity for it is enforced by the calling
  agent's JWT claim, not Postgres.
- `Devices.Service`'s admin API now has a genuine runtime availability
  dependency on `Org.Service` when the Redis cache is cold: if `Org.Service`
  is down and the cache is empty for a given (tenant, user), Devices'
  permission checks fail rather than silently succeeding or falling back to
  a local query. This is an accepted, visible tradeoff of choosing (1) over
  full event replication, not a hidden gap.
- This does **not** retroactively change `Org.Service`'s and
  `DeviceCredential`'s existing shared-`Data`-project arrangement — that
  stays as documented. This ADR only sets the default for `Devices.Service`
  and any service added after it.
  (Since then, the shared `Data` project has been folded into `Org.Service`,
  its only user, so `DeviceCredential` now lives in Org.Service's own project.)
- The permission catalog itself (the `Permission`/`RolePermission` seed
  data) stays centrally owned and seeded by `Org.Service` even for
  permission keys that gate `Devices.Service`'s own endpoints
  (`router.view`, `router.manage`, `device.view`, `device.manage`) — see
  [`04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md).
  Only *checking* a permission moves off direct DB access for a
  non-owning service; the catalog itself is still one cross-cutting
  concern seeded in one place.

## Revisit when

The sync call-through's availability coupling proves painful in practice —
same "not now, revisit when it hurts" posture as
[ADR-0006](0006-defer-auth-platform-extraction.md). At that point, the
event-driven replication alternative above (and the events already being
published under decision (2)) is the natural next step, not a redesign.
