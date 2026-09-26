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

**Every service publishes each operation it performs as a domain event, on
the existing RabbitMQ broker — no second broker, and no second exchange —
and `Audit.Service` is one of the subscribers.** A service publishes
`membership.added`, `role.assigned`, `router.credential.fetched`, … to the
shared `homesecurity.events` exchange, under a routing key equal to the event
name, wrapped in one common envelope (event id, timestamp, tenant, actor,
target, and an open `metadata` object holding that event's extended data —
`domain_event_message.json`). The events are not audit-specific: any number
of services can subscribe to the ones they care about (a cache warmer, a
notifier), and a service that wants to know what happened no longer needs a
second, weaker "something changed" message alongside. Keycloak's own
admin/login events reach the same exchange via its built-in event-listener
SPI webhook, posted to a thin internal endpoint that does no interpretation
of its own. A new **`Audit.Service`** subscribes to the events that make up
the audit trail (its own queue, binding the event names it records), and —
following the precedent [ADR-0016](0016-devices-service-owns-its-own-database.md)
already set — owns its **own, effectively append-only database**, rather
than writing into `Org.Service`'s or `Devices.Service`'s schema. Full design:
[`16-auditing-and-compliance.md`](../architecture/16-auditing-and-compliance.md).

This **replaces** the earlier coarse `access.changed`, `router.changed` and
`device.changed` notification events: none of them had a consumer, and each
domain event says strictly more (who, what, before/after) at the same call
site, so keeping both would mean two messages per operation that could
disagree. `logs.ingested`, which the Go processor consumes, is unaffected.
The rule from [`07-caching-and-idempotency.md`](../architecture/07-caching-and-idempotency.md#3-domain-events-instead-of-something-changed-notifications)
still holds for subscribers — an event says what happened, not the new
state, so a subscriber that needs current state calls the owning service's
API — it just no longer needs a second message.

Publishing is **inline, after the change has been saved**, exactly as the
replaced notifications were, and a broker outage while publishing is out of
scope for now: the change stays committed and the request fails. A
transactional outbox (write the event in the same database transaction, relay
it afterwards) was designed and rejected as more machinery than this stage
calls for; it can be added behind the same publisher if a lost audit event
ever becomes a real risk. One deliberate exception in the other direction:
the router-credential hand-out publishes *before* returning the credential,
so a failure to publish fails the request instead of handing out a credential
nobody recorded.

Three decisions make the audit trail trustworthy rather than best-effort:

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
- Publishing services need to know *who* is acting — a shared "current
  actor" abstraction (human user, device or system, resolved from the
  request's claims) is injected rather than a user id being threaded through
  every service method. It lives, with the envelope and a small publisher, in
  `Common/Dzaba.HomeSecurity.DomainEvents`.
- Existing services carry no audit-specific storage: publishing an event
  needs no schema change, so no migration beyond the permission seed.
- `Org.Service` and `Devices.Service` haven't been deployed anywhere, so
  their EF migrations were regenerated as a single fresh `InitialCreate`
  rather than stacked with an incremental migration for the new permissions.
- Personal data in event targets: an actor is pseudonymized on erasure, but
  an event whose *target* is a person (`membership.added`, `role.assigned`
  carry the affected user's id) still names them. Erasing that link is an
  open question for the retention/erasure work, not solved by the actor
  token alone.
- `15-router-credentials.md` and `09-observability.md`'s forward references
  to "the security/audit log stream" now resolve to this document instead of
  an undesigned idea.

## Revisit when

A real customer or regulator interaction (an actual data subject request, a
breach, or a deal that requires SOC2/ISO certification) makes this worth
building — at that point,
[`16-auditing-and-compliance.md`](../architecture/16-auditing-and-compliance.md)
is the design to implement against, not a redesign starting point.
