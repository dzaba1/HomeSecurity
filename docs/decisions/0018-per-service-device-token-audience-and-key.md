# ADR-0018: Each service validates device tokens against its own audience and signing key

## Status

Accepted

## Context

[ADR-0004](0004-device-agent-authentication.md) has the agent exchange its
device secret for a short-lived JWT carrying `tenant_id`, `device_id` and a
narrow scope, validated by the standard `JwtBearer` middleware. When that
was written the only consumer was the Logs Ingestion API (`logs:write`).
`Devices.Service` is a second consumer: its agent-facing router-config
endpoint needs `router:read` (see
[`15-router-credentials.md`](../architecture/15-router-credentials.md)).

Three things had to be settled for more than one service to accept device
tokens:

1. Which key and audience each service validates against.
2. How a service that *also* authenticates humans (Keycloak) accepts both.
3. Where the shared token-handling code lives, so it isn't copied per service.

## Decision

- **Per-service audience and signing key.** Each service validates device
  tokens against its own `Authentication:DeviceTokens:Audience` and
  `Authentication:DeviceTokens:SigningKey` (`home-security-logs-ingestion`
  and `home-security-devices` today). A service therefore only ever holds
  the key for tokens minted *for it*; leaking one service's key does not let
  an attacker forge tokens for another. Locally, `init.ps1` generates one
  key per service (`LOGS_DEVICE_TOKENS_SIGNING_KEY`,
  `DEVICES_DEVICE_TOKENS_SIGNING_KEY`).
- **Issuer validation stays off, as in ADR-0004's design**: device tokens
  have no OIDC discovery endpoint; the symmetric key plus audience is the
  trust anchor.
- **A service that also serves humans registers device tokens as a second,
  named scheme** (`DeviceToken`, in `Devices.Service`) next to its default
  Keycloak scheme, and its agent endpoints opt in with
  `[Authorize(AuthenticationSchemes = "DeviceToken", Policy = "<scope>")]`.
  A human token can't satisfy such an endpoint: it fails signature/audience
  validation, or has no scope.
- **Shared code lives in `Common/Dzaba.HomeSecurity.DeviceAuth`**: the claim
  names (`tenant_id`, `device_id`, `scope`), the `ClaimsPrincipal`
  accessors, the scope requirement/handler, and
  `AddDzabaHomeSecurityDeviceScopePolicy(scope)`. Test helpers for minting
  device tokens live in the shared `TestUtils`.

## Consequences

- The device-token endpoint, when built, must mint a token **per audience**
  (or a multi-audience token signed for each key it targets); it is the one
  component that holds all the keys. That is a deliberate concentration in
  the one piece of custom authentication code ADR-0004 already accepts.
- Rotating a service's key is local to that service and the token endpoint.
- Adding a third consuming service is configuration plus one
  `AddDzabaHomeSecurityDeviceScopePolicy` call, not new auth code.
- The named-scheme registration is still written inline in
  `Devices.Service`'s `Program.cs`; it moves into `DeviceAuth` when a second
  service needs the same shape.
