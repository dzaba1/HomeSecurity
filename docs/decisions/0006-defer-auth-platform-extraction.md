# ADR-0006: Defer extracting auth into a separate, product-agnostic repo

## Status

Accepted (deferred, not rejected)

## Context

The identity/tenant/role concerns described in
[`03-security-and-identity.md`](../architecture/03-security-and-identity.md)
and [`04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md)
are largely product-agnostic — they don't know what a "router" or "device
log" is. That makes them a candidate for a standalone `saas-auth-platform`
repo, reusable across future products, exposing:

- Keycloak + `oauth2-proxy` config as code
- A generic Tenant/Organization/Membership API
- The Role/Permission engine from ADR-0005

## Decision

**Not now.** We will keep building this inside the `HomeSecurity` repo for the
time being, since:

- The current C# scaffolding (`Dzaba.HomeSecurity.Auth`, `Dzaba.Org`) is
  explicitly experimental and can be freely reshaped or deleted — there's no
  cost to keeping it colocated while the design is still moving.
- Splitting into two repos adds real overhead now (contract versioning
  between repos, two docker-compose sets for local dev) for a payoff
  (reusability across other products) that doesn't exist yet — there's only
  one product.
- We'd rather get the domain model (tenants, roles, permissions, device
  tokens) right once, proven against a real product, before generalizing it.

## Revisit when

- The permission/role engine and tenant model have stabilized against real
  usage in this product, **and**
- There's a second product (or a concrete plan for one) that would actually
  reuse it.

At that point, the split described above should be mechanical: the
product-agnostic pieces are already cleanly separated internally (Keycloak
config, `oauth2-proxy` config, generic Tenant/Role/Permission tables vs.
HomeSecurity's own Device/Log/Incident tables), so extraction is a matter of
moving projects/config, not a redesign.
