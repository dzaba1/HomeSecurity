# Multi-tenancy

## Model: shared database, `tenant_id` discriminator

Every tenant (called an **Organization** in the product) shares the same
database and the same application instances. Every row of tenant-owned data
(devices, logs, incidents, roles, memberships) carries a `tenant_id`. This is
the default multi-tenancy pattern for SaaS products (GitHub orgs, Stripe
accounts, Slack workspaces all work this way) and it's the cheapest to run and
scale.

Rejected alternatives:

- **Database-per-tenant** — strongest isolation, but expensive to operate
  (migrations, backups, connection pooling multiply per tenant) and massive
  overkill at this project's scale. Reconsider only if a real customer ever
  requires physical data separation for compliance reasons.
- **Schema-per-tenant** — a middle ground, but still multiplies migration and
  connection complexity for isolation benefits we don't need yet.

## Enforcing isolation

Isolation is enforced in the application layer, not by physical separation:

- EF Core **global query filters** on every tenant-owned entity, keyed off a
  `tenant_id` resolved from the current request context. A query that forgets
  to filter by tenant simply can't happen — the filter is applied to *every*
  query against that entity automatically.
- The current tenant is resolved from the **authenticated identity**, not from
  a client-supplied value, wherever a human user is involved (see
  [`03-security-and-identity.md`](03-security-and-identity.md)). A request
  header naming the tenant is only trusted for machine-to-machine calls (agent
  → ingestion) where the tenant is bound to the device's own credential.

## The trade-off we're accepting

A bug in a query filter, or a manual query that bypasses it, can leak data
across tenants. We mitigate this the same way staff-level teams do in
production: **automated "tenant leak" tests** that assert, for every
tenant-owned entity, that a user from tenant A can never read or write a row
belonging to tenant B — not by hoping the filter is always applied correctly.

## What is *not* tenant-scoped

Identity itself (a user's login, password, MFA) lives in Keycloak and is
tenant-agnostic — the same person can belong to more than one organization.
Tenant membership (which organizations a user belongs to, and with what role)
is the join between "who you are" (Keycloak) and "what you can do here" (this
system's own data) — see [`04-roles-and-permissions.md`](04-roles-and-permissions.md).

## A second, separate axis: do *services* share a database?

"Shared database" above is entirely about the tenant-isolation strategy
*within* one service's own database — it says nothing about whether two
different microservices share a physical database with each other.
`Org.Service` currently owns its tables (including the device-auth
`DeviceCredential` entity) through a `Data` project documented as "shared
across the Org and DeviceAuth services" — a real, separate axis of
coupling worth naming even though only one service actually uses it today:
two services sharing a migration history can't deploy schema changes
independently.

Going forward, the default for a *new* service is the opposite: its own
database, no cross-service database-level foreign keys, tenant validity
enforced at the API edge rather than a shared FK to `Organization`. The
first example is `Devices.Service` (`Router`/`Device` management) — see
[ADR-0016](../decisions/0016-devices-service-owns-its-own-database.md) for
the decision and the cross-service tenant/permission-check mechanism this
requires. This ADR doesn't retroactively change `Org.Service`'s existing
arrangement, only the policy for services added after it.
