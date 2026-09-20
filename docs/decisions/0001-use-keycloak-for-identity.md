# ADR-0001: Use Keycloak for human identity

## Status

Accepted

## Context

We need login, password management, MFA, and token issuance for human users
of the Admin UI. Writing this ourselves is a large, security-critical surface
with well-known off-the-shelf solutions.

## Decision

Use a self-hosted **Keycloak** instance (backed by Postgres) as the identity
provider for all human users, via standard OIDC/OAuth2 flows. Keycloak owns
nothing about tenants, devices, or product-level permissions — only "who is
this person."

## Consequences

- No custom password storage, MFA, or token-signing code.
- One more container to operate (Keycloak + its own Postgres).
- All authorization (tenant membership, roles, permissions) is deliberately
  kept in our own data model, not Keycloak — see
  [`04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md).
