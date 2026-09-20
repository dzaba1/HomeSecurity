# ADR-0004: Purpose-built short-lived tokens for agent/device authentication

## Status

Accepted

## Context

Agent applications (running on home PCs/laptops/phones) need to authenticate
to the Logs Ingestion API. Unlike human users, there could eventually be a
large number of these, one per registered device, and they are machine
identities, not people.

Two Keycloak-native options were considered:

1. A Keycloak client (OAuth2 Client Credentials) per device.
2. OIDC Dynamic Client Registration (devices self-register as Keycloak
   clients).

Both push per-device identity management into Keycloak, whose client model
and admin console are not designed for that granularity of scale.

## Decision

Build a small, purpose-specific device-token endpoint, owned by this product:

- A device is provisioned with a random, high-entropy secret; only its hash
  is stored.
- The agent exchanges that secret for a short-lived (5–15 min) JWT scoped to
  `tenant_id` + `device_id` + a narrow permission (`logs:write`).
- The Ingestion API validates this JWT with the same standard `JwtBearer`
  middleware used for human tokens, just a different issuer/audience.
- Revocation = rotate/delete the device secret; no blocklist needed because
  tokens expire quickly.

## Consequences

- This is the one place in the system with genuinely custom
  authentication/token-issuing code — kept deliberately small (validate a
  hash, sign a JWT).
- Scales to a large number of devices without touching Keycloak's client
  management.
- We explicitly accept not using Keycloak for this piece, in exchange for
  operational simplicity at scale.

See [`03-security-and-identity.md`](../architecture/03-security-and-identity.md).
