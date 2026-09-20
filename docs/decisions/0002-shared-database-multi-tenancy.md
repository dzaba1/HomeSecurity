# ADR-0002: Shared database with `tenant_id` for multi-tenancy

## Status

Accepted

## Context

We need to isolate each organization's data (devices, logs, incidents) from
every other organization's. Options range from full physical separation
(database-per-tenant) to a single shared schema with a discriminator column.

## Decision

Use a single shared database. Every tenant-owned entity carries a `tenant_id`
column. Isolation is enforced via EF Core global query filters resolved from
the authenticated identity, not from client-supplied input.

## Consequences

- Cheapest option to run and scale; matches the pattern used by most
  mainstream SaaS products at this scale.
- Isolation is a software guarantee, not a physical one — a query filter bug
  is the main risk. Mitigated with dedicated automated tests that assert
  cross-tenant access is impossible for every tenant-owned entity.
- Revisit only if a real compliance requirement demands physical data
  separation for a specific tenant.

See [`02-multi-tenancy.md`](../architecture/02-multi-tenancy.md).
