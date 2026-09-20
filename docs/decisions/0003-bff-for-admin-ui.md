# ADR-0003: Backend-for-Frontend (`oauth2-proxy`) in front of the Admin UI

## Status

Accepted

## Context

The Admin UI needs to authenticate its users against Keycloak. The
conventional SPA pattern (browser holds the access/refresh tokens directly)
exposes those tokens to any XSS vulnerability in the frontend, since anything
reachable by JavaScript can be exfiltrated by injected JavaScript.

## Decision

Front the Admin UI with `oauth2-proxy`, a ready-made identity-aware reverse
proxy. It performs the OIDC Authorization Code + PKCE flow against Keycloak
and issues the browser an HttpOnly session cookie. The browser never sees a
raw access/refresh token.

## Consequences

- No custom token-handling code in the Admin UI frontend or its backend.
- One more container to deploy in front of the Admin UI.
- Slightly less control over the exact login UX than rolling our own; judged
  an acceptable trade for the security default.

See [`03-security-and-identity.md`](../architecture/03-security-and-identity.md).

## Alternatives considered

- Duende.BFF — a licensed .NET library doing the same job in-process. Rejected
  in favor of a free, ready-made container to keep license/cost surface at
  zero and keep the concern fully outside our own codebase.
- SPA with tokens in browser storage — rejected due to XSS exposure.
