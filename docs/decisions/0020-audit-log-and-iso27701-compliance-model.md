# ADR-0020: Dedicated audit-event stream, and ISO/IEC 27701 as the GDPR compliance model

## Status

Accepted

## Context

Two related gaps had been flagged but never actually designed:

1. [`09-observability.md`](../architecture/09-observability.md) explicitly
   deferred "security/audit logging as a distinct stream" from operational
   logging, and [ADR-0007](0007-rabbitmq-as-message-bus.md) already noted, in
   passing while choosing RabbitMQ, that a Kafka-based
   "Keycloak-events-to-audit-log idea" had been resolved to "use a webhook
   onto the same broker instead" — but nothing since then made that concrete.
2. [`15-router-credentials.md`](../architecture/15-router-credentials.md) and
   [ADR-0015](0015-centralized-encrypted-router-credentials.md) both already
   *promise* that every router-credential fetch is written to "the
   security/audit log stream" — a forward reference to a design that didn't
   exist yet.
3. No document addressed GDPR (personal data this system holds: emails,
   MAC addresses, router credentials, push tokens) or which security
   framework the design as a whole is meant to be auditable against.

## Decision

**A dedicated audit-event stream, separate from operational logging, on the
existing RabbitMQ broker — no second broker, and no second exchange.** Every
service publishes audit events (role/membership changes, router-credential
fetches, device acknowledgements, device pairing/revocation) to the shared
`homesecurity.events` exchange under `audit.<action>` routing keys
(e.g. `audit.role.assigned`); Keycloak's own admin/login events reach the
same exchange via its built-in event-listener SPI webhook, posted to a thin
internal endpoint that does no interpretation of its own. A new
**`Audit.Service`** is the sole consumer (a queue bound to `audit.#`), and —
following the precedent [ADR-0016](0016-devices-service-owns-its-own-database.md)
already set — owns its **own, effectively append-only database**, rather
than writing into `Org.Service`'s or `Devices.Service`'s schema. Full design:
[`16-auditing-and-compliance.md`](../architecture/16-auditing-and-compliance.md).

Four decisions make that stream trustworthy rather than best-effort:

- **Transactional outbox on the publishing side.** A publishing service
  writes the audit event into an outbox table *in the same database
  transaction* as the change it describes; a background relay then publishes
  it to RabbitMQ. Unlike the coarse `*.changed` notifications (published
  inline after `SaveChanges`, where a broker outage silently drops the
  message), an audit event can't be lost between "the change committed" and
  "the event was published."
- **Idempotency by database constraint, not Redis.** `Audit.Service` dedupes
  redelivered messages with a unique index on the event id; a duplicate is
  acknowledged and dropped. The Redis `SETNX` pattern from
  [`07-caching-and-idempotency.md`](../architecture/07-caching-and-idempotency.md#2-idempotent-event-processing)
  isn't used here — it would add a Redis-outage failure mode for no extra
  safety over the constraint that has to exist anyway.
- **Tamper-evidence via a hash chain.** Each stored event carries the hash of
  the previous event in its chain, so an edit or a deletion in the middle of
  the trail is detectable, going beyond the `INSERT`/`SELECT`-only database
  grant that prevents it in the first place.
- **Erasure by unlinking, not rewriting.** Events store an opaque actor
  token, with the token → real-identity mapping in a separate table. Erasing
  a person deletes the mapping; the events themselves are never modified
  (which would break both the append-only grants and the hash chain), yet
  they no longer point at anyone.

This is deliberately **not** the same mechanism as the coarse
`access.changed`/`router.changed`/`device.changed` notification events from
[`07-caching-and-idempotency.md`](../architecture/07-caching-and-idempotency.md#3-coarse-something-changed-notification-events):
those say "something changed, go re-check current state" and have no
consumer yet; audit events are the immutable record of the change itself,
retained on their own schedule, and never used to reconstruct current
state.

**ISO/IEC 27701 (the Privacy Information Management System extension to
ISO/IEC 27001) is adopted as the GDPR compliance model**, rather than a
bespoke checklist. ISO/IEC 27001's Annex A controls already map onto
decisions made throughout this repo (RBAC, encryption at rest, HTTPS-only,
logging); ISO/IEC 27701 layers the privacy-specific obligations (data
subject rights, retention limitation, breach evidence) directly on top of
that same control set, so GDPR compliance and the security ISMS stay one
coherent model instead of two documents that drift apart over time.
Certification itself is explicitly **not** pursued now — same "not now,
revisit when it hurts" posture as [ADR-0006](0006-defer-auth-platform-extraction.md)
— this decision is about which model the design follows, not about engaging
an auditor.

## Consequences

- `Audit.Service` becomes the fourth .NET service, with its own solution and
  database per this project's "every microservice independently buildable"
  principle ([`CLAUDE.md`](../../CLAUDE.md)) — not built yet; this ADR and
  [`16-auditing-and-compliance.md`](../architecture/16-auditing-and-compliance.md)
  are the design to build it against.
- Two new permission-catalog entries join
  [`04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md):
  `audit.view` and `data_subject_request.manage`.
- Retention is per-event-category, not one global TTL, since GDPR's storage
  limitation principle and security-audit retention needs pull in different
  directions — see
  [`16-auditing-and-compliance.md`](../architecture/16-auditing-and-compliance.md#4-retention-immutability-and-access)
  for the proposed periods.
- The right to erasure is met by deleting the actor-token mapping for an
  erased user, leaving the audit events themselves in place, since the audit
  trail's evidentiary value is the reason it's retained at all — a
  deliberate, documented middle ground rather than either extreme (keeping
  raw personal data indefinitely, or deleting security-relevant evidence on
  request).
- `Org.Service` and `Devices.Service` each gain an audit outbox table and a
  relay, and services need to know *who* is acting — a shared "current
  actor" abstraction (human user or device, resolved from the request's
  claims) is injected rather than a user id being threaded through every
  service method.
- `Org.Service` and `Devices.Service` haven't been deployed anywhere, so
  their EF migrations are regenerated as a single fresh `InitialCreate`
  rather than stacked with incremental migrations for the new permissions and
  outbox tables.
- `15-router-credentials.md` and `09-observability.md`'s forward references
  to "the security/audit log stream" now resolve to this document instead of
  an undesigned idea.

## Revisit when

A real customer or regulator interaction (an actual data subject request, a
breach, or a deal that requires SOC2/ISO certification) makes this worth
building — at that point,
[`16-auditing-and-compliance.md`](../architecture/16-auditing-and-compliance.md)
is the design to implement against, not a redesign starting point.
