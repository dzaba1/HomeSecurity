# ADR-0015: Store router credentials centrally, encrypted, fetched by agents on demand

## Status

Accepted

## Context

Agents log into the router itself (its admin web page or SNMP) to read the
connected-device list — see
[`05-agents-and-ingestion.md`](../architecture/05-agents-and-ingestion.md).
That requires the router's own username/password (or SNMP community
string), a credential distinct from the per-device secret an agent uses to
authenticate to our cloud ([ADR-0004](0004-device-agent-authentication.md)).

Two places this credential could live were considered:

1. **Agent-local only** — the user types the router's login directly into
   the agent app on first run; it never leaves that machine.
2. **Centrally, entered by an org admin in the Admin UI, encrypted at rest,
   and fetched by agents at runtime.**

Option 1 is simpler and keeps a third-party credential out of our cloud
entirely, but it means the account owner can't manage or rotate the router
login unless they're also the person running the agent, and every
new/reinstalled agent needs it re-typed by hand. The product wants router
management to be an org-admin-panel action, not something scattered across
whichever device happens to run an agent — the same reasoning that already
puts device pairing under Admin UI control.

## Decision

Store router credentials centrally:

- A `Router` is a tenant-scoped resource, managed via the Admin UI, requiring
  `router.manage` permission to write.
- The password/community string is encrypted at rest using the ASP.NET Core
  Data Protection API (key ring persisted in the existing Redis instance),
  never stored or logged in plaintext, and never re-displayed by the Admin
  UI once saved (write-only, like the device secret).
- An agent's short-lived JWT gains a `router:read` scope; a new Router
  Config endpoint resolves `tenant_id` + `device_id` from that JWT to the
  linked `Router`, decrypts the secret server-side, and returns it over
  HTTPS to that device only.
- Agents keep the credential in memory for the polling session rather than
  persisting it long-term, and re-fetch it periodically / on router login
  failure, so a password rotated in the Admin UI takes effect without
  re-pairing any agent.

Full design, data model, and sequence diagram:
[`15-router-credentials.md`](../architecture/15-router-credentials.md).

## Consequences

- The cloud backend now holds a real third-party credential (the router's
  own login), which is a larger blast radius than the random secrets we
  mint ourselves for devices — mitigated by encryption at rest, write-only
  exposure through the API, `router.manage`-gated writes, and audit logging
  of every credential fetch (never its value).
- Router management becomes centralized and admin-driven: adding/rotating a
  router's login is one Admin UI action that every linked agent picks up
  automatically, instead of a per-machine, manually re-entered step.
- A dedicated secrets manager (Vault / cloud KMS) is deferred in favor of an
  encrypted database column for now, consistent with
  [ADR-0006](0006-defer-auth-platform-extraction.md)'s stance on deferring
  infra-heavy choices until a concrete deployment target exists; worth
  revisiting then.
