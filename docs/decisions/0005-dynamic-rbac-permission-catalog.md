# ADR-0005: Permission-catalog + customizable roles (TeamCity-style RBAC)

## Status

Accepted

## Context

We need an authorization model that can express "who can do what" per
organization, ideally without hard-coding a fixed small set of roles forever,
and without building a full policy/rules engine.

## Decision

Model authorization as:

- **Permission** — a fixed, seeded catalog of atomic permission keys
  (`device.delete`, `logs.view`, ...), owned by the product and inserted at
  deploy time.
- **Role** — a named, tenant-owned bundle of permissions. A few system-default
  roles (`Owner`, `Admin`, `Member`, `Viewer`) ship out of the box; tenants can
  also define their own custom roles as arbitrary permission sets.
- **UserRole** — assigns a role to a user, scoped to a tenant.

A permission check is a single lookup: does any role the user holds in this
tenant grant this permission key.

## Consequences

- The role/permission *engine* (the four tables and their CRUD) has no
  knowledge of what any permission key means, so it is fully reusable and
  could later be lifted into a shared auth platform without modification.
- Only the *catalog contents* are product-specific, and that's exactly the
  boundary we want.
- Effective permissions per (user, tenant) should be cached at
  login/session-start rather than joined on every request.
- Rejected: Keycloak Authorization Services (UMA2) — more "no-code" but pushes
  business authorization logic into Keycloak config, which we've decided
  should stay identity-only.

See [`04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md).
