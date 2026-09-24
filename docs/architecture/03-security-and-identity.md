# Security & Identity

There are two completely different "who is this" problems in this system, and
they're deliberately solved differently:

1. A **human** logging into the Admin UI.
2. An **agent app** (running on a home PC/laptop/phone) pushing logs on a
   device's behalf.

Keycloak only ever answers the first question. It has no concept of routers,
devices, or agents.

## 1. Human login: Keycloak + a Backend-for-Frontend

Keycloak is a ready-made, self-hosted identity provider (OIDC/OAuth2). It
handles account creation, passwords, MFA, and issuing tokens — none of that is
custom code in this project.

### Why not have the browser talk to Keycloak directly?

The standard pattern for a single-page app would be: the browser redirects to
Keycloak, logs in, and gets a token back that it stores and attaches to API
calls. The problem is that anything sitting in the browser (JS memory,
`localStorage`) can be stolen if the frontend ever has an XSS bug — and given
that this token *is* the user's identity, that's a serious blast radius.

### The chosen pattern: Backend-for-Frontend (BFF)

Instead, we put a small proxy in front of the Admin UI whose only job is the
login handshake. We use **`oauth2-proxy`** — a ready container, configured,
not coded:

- The browser only ever gets a normal **HttpOnly session cookie** (JavaScript
  cannot read it, XSS can't steal it).
- `oauth2-proxy` is the one that actually talks to Keycloak, holds the real
  tokens, and forwards authenticated requests to the Admin UI backend / APIs.

```
Browser --(cookie)--> oauth2-proxy --(OIDC)--> Keycloak
                          |
                          v
                    Admin UI / APIs
```

### What PKCE is, and why it matters here

PKCE ("pixy") is a small extra safety check baked into the OIDC login flow.
Before redirecting the user to Keycloak, `oauth2-proxy` generates a random
secret and keeps it. Keycloak will only hand back a valid login result to
whoever presents that matching secret. This stops an attacker who intercepts
the redirect from completing the login themselves. We don't implement this —
`oauth2-proxy` (and every standard OIDC client) does it automatically once
enabled.

### Where Keycloak sits on the network

Keycloak is **not** fully internal, and it isn't fully public either — it has
two faces:

- **Public (browser-reachable):** the hosted login/registration/account pages
  and the OIDC front-channel, i.e. `/realms/*` and `/resources/*`. A user's
  browser is redirected here by `oauth2-proxy`, so it must be reachable from
  the Internet (behind the TLS-terminating reverse proxy). There are no custom
  register/login endpoints in our APIs on purpose: users type their password
  only into Keycloak's own page, never into our code.
- **Internal only:** the admin console and Admin API (`/admin/*`, the
  `master` realm), `/metrics`, `/health`, and the management port. The reverse
  proxy must not route these. Server-to-server calls (`oauth2-proxy` and the
  APIs fetching the token endpoint / JWKS) use the internal network address.

Because the `iss` claim in every token has to be one stable string, Keycloak is
configured with a fixed public hostname (`KC_HOSTNAME`) rather than deriving it
from the request. Back-channel endpoints (`KC_HOSTNAME_BACKCHANNEL_DYNAMIC`)
still resolve to whichever address the caller used, so services keep talking to
Keycloak over the internal network while validating the public issuer.

## 2. Agent/device authentication: short-lived device tokens

Agents are not people and there could eventually be a large number of them
(one per PC/laptop/phone, per device). Two options were considered:

- **A Keycloak client per device**, using OAuth2 Client Credentials. Rejected:
  Keycloak's admin console and client management aren't built for potentially
  thousands of clients, and per-device revocation would mean deleting Keycloak
  clients via its Admin API for every single device.
- **A tiny, purpose-built device-token endpoint**, owned by this product, not
  Keycloak. **Chosen.**

Flow:

1. When a device/agent is registered (via the Admin UI), the backend
   generates a per-device secret (random, high-entropy) and stores only its
   hash — the same way you'd store a password.
2. The agent authenticates to a small token endpoint with that secret and
   receives a short-lived JWT (5–15 minutes) carrying `tenant_id`, `device_id`,
   and a narrow scope like `logs:write`.
3. Any API that agents call (the Logs Ingestion API, and `Devices.Service`'s
   router-config endpoint) validates that JWT with the same off-the-shelf
   `JwtBearer` middleware used for human tokens — just a different
   audience and a symmetric signing key instead of Keycloak's discovery
   document. No bespoke validation code. Each service has **its own
   audience and key**, and a service that also serves humans registers
   device tokens as a second, named authentication scheme; the claim names,
   scope policy and handler are shared code
   (`Common/Dzaba.HomeSecurity.DeviceAuth`). See
   [ADR-0018](../decisions/0018-per-service-device-token-audience-and-key.md).
4. **Revocation** is just deleting/rotating the device's secret in our own
   DB — since tokens are short-lived, a revoked device is locked out within
   minutes without needing a token-blocklist.

This is the one piece of genuinely custom authentication code in the system,
and it's intentionally small: verify a hash, mint a signed JWT. Everything
else defers to Keycloak or `oauth2-proxy`.

### Alternative considered and rejected: OIDC Dynamic Client Registration

Keycloak supports devices self-registering as OAuth clients (no custom code at
all). It was rejected for the same reason as the per-device client option
above — it's a real, spec-compliant capability, but Keycloak's client model
isn't designed to scale to per-end-user-device granularity. Worth knowing
about; not worth using here.

## Summary

| Actor | Talks to | Holds |
|---|---|---|
| Human, via browser | `oauth2-proxy` | HttpOnly session cookie only |
| `oauth2-proxy` | Keycloak | Real OIDC tokens (server-side) |
| Agent app | This product's device-token endpoint | Long-lived device secret (never sent after initial pairing except to re-mint tokens), short-lived JWT |
| Any API | — | Validates JWTs (human or device) with standard `JwtBearer` middleware |
