# Deployment & High Availability

## The requirement

The system must be deployable **without stopping**: rolling out a new version
of any service — Ingestion API, Org (Admin/Tenant) API, Processing Workers,
Notification Service, Admin UI/BFF — is a routine, frequent operation, not an
event that needs a maintenance window. This document fixes the mechanism that
makes that true, and calls out the one consequence that ripples into the
public API design: [`12-api-versioning.md`](12-api-versioning.md) and
[`13-hateoas-public-api.md`](13-hateoas-public-api.md) both exist because of
what's decided here.

This document is about staying up at whatever replica count a service
already runs at — it fixes a *floor* (at least 2 replicas, never dropping
below it during a rollout or a node drain). It is not about handling more
load by adding replicas beyond that floor, or removing them when load drops
— that's [`14-scalability.md`](14-scalability.md), which builds on the same
stateless-`Deployment` foundation this document establishes.

## Deployment target: Kubernetes rolling updates

Every custom service already assumes a Kubernetes-shaped runtime —
liveness/readiness endpoints, an ingress edge for TLS
([ADR-0008](../decisions/0008-https-everywhere.md)), horizontally scalable
stateless processes. Zero-downtime deploys build on the same assumption
rather than introducing a separate mechanism:

- Every service is a Kubernetes **`Deployment`** using the standard
  **`RollingUpdate`** strategy (`maxUnavailable: 0`, `maxSurge: 1` or higher):
  new pods are started *before* old ones are terminated, so total capacity
  never drops during a rollout.
- A new pod only receives traffic once it passes **`/health/ready`**
  (see [`09-observability.md`](09-observability.md)) — readiness gating is
  what stops a rollout from routing requests to a pod that's still starting
  up or can't reach Postgres/RabbitMQ/Redis yet.
- Every service runs at **least 2 replicas** in any environment that needs to
  stay up during a deploy. A single-replica service can't roll without a gap,
  no matter what the update strategy says. This is a floor, not a cap — see
  [`14-scalability.md`](14-scalability.md) for how (and on what signal) each
  service scales beyond it under load.
- A **`PodDisruptionBudget`** (`minAvailable: 1` at minimum, tuned per
  service's replica count) protects the same guarantee against *involuntary*
  disruption — node drains, cluster autoscaling, node upgrades — not just
  application deploys.

## What this requires of every service

Rolling updates only give zero downtime if the services being rolled can
actually tolerate it:

- **Stateless processes.** No service holds session or request state
  in-process. Human session state already lives server-side in
  `oauth2-proxy`/Redis, not in an Admin UI/BFF pod
  ([`03-security-and-identity.md`](03-security-and-identity.md)); the same
  rule applies to every future service — anything that must survive across
  requests goes in Postgres or Redis, never a pod's memory, so any replica
  can serve any request and a pod can be killed mid-rollout without losing
  anything but that one in-flight call (which the client retries).
- **Backward-compatible database migrations.** During a rollout, the old and
  new pod versions run against the **same** database schema simultaneously.
  A migration that both versions can tolerate is applied *with* the deploy
  that needs it (e.g. adding a nullable column); a migration an old version
  can't tolerate (renaming/dropping a column, tightening a constraint) is
  split across two deploys using the standard **expand/contract** pattern:
  first ship the new column/table alongside the old one (expand, old code
  keeps working), deploy the code that uses only the new shape, then remove
  the old column/table in a later deploy once no running version reads it
  (contract). This is operational discipline, not new infrastructure — no
  extra tooling beyond the EF Core migrations already planned.
- **Backward-compatible message contracts.** The same version-skew window
  applies across RabbitMQ ([ADR-0007](../decisions/0007-rabbitmq-as-message-bus.md)):
  an old Processing Worker pod can still be consuming messages published by a
  new Ingestion API pod (or vice versa) for the duration of a rollout, so a
  breaking change to a message's shape follows the same expand/contract
  discipline as a DB migration, not a hard cutover.
- **Versioned public APIs.** The same version-skew reasoning applies to
  anything a *client outside the cluster* calls — the Admin UI, the agent
  app already installed on users' machines, and any third-party integrator.
  A rollout can (briefly, or for as long as a deprecated version is kept
  around on purpose) have two versions of the same API answering requests
  behind the same Kubernetes `Service`. See
  [`12-api-versioning.md`](12-api-versioning.md) for how that's handled —
  it's the direct consequence of this document's guarantee, not a separate
  concern.

## Alternatives considered

- **Blue/green deployment** (two full parallel environments, switch traffic
  at the load balancer once the new one is verified) — gives the same
  zero-downtime guarantee and a faster rollback, but doubles infrastructure
  cost for the duration of every deploy and needs an external traffic-switch
  step this project doesn't otherwise need. Rolling updates plus readiness
  gating already meet the requirement at a fraction of the operational cost,
  which fits a portfolio-scale system better. Worth revisiting if rollback
  speed ever becomes a real incident-response problem.
- **Recreate strategy** (tear down all old pods, then start new ones) —
  rejected outright; it causes downtime by definition, which is exactly what
  this document exists to avoid.
- **Canary releases** (route a small percentage of traffic to the new
  version before a full rollout) — a genuinely useful refinement on top of
  rolling updates, not an alternative to them. Not needed for v1 at this
  traffic scale; the versioning discipline in
  [`12-api-versioning.md`](12-api-versioning.md) is what actually protects
  against a bad deploy breaking clients, which is canary's usual justification.

## Consequences

- No service can assume it's the only version of itself running — every
  service's code, schema, message contracts, and public API must tolerate
  running alongside the previous version for the duration of a rollout.
- This is enforced by two things this document doesn't itself define: health
  checks that actually detect a broken pod
  ([`09-observability.md`](09-observability.md)) and versioned public
  contracts that let old and new API versions coexist on purpose
  ([`12-api-versioning.md`](12-api-versioning.md)).
- Recorded as [ADR-0011](../decisions/0011-zero-downtime-rolling-deployments.md).

## Related documents

- Health checks that gate a rollout: [`09-observability.md`](09-observability.md)
- API versioning (the direct consequence for public contracts):
  [`12-api-versioning.md`](12-api-versioning.md)
- HATEOAS for the public integratable API: [`13-hateoas-public-api.md`](13-hateoas-public-api.md)
- Horizontal scalability beyond the HA floor: [`14-scalability.md`](14-scalability.md)
- HTTPS-only edge: [ADR-0008](../decisions/0008-https-everywhere.md)
- RabbitMQ as the message bus: [ADR-0007](../decisions/0007-rabbitmq-as-message-bus.md)
- Decision record: [ADR-0011](../decisions/0011-zero-downtime-rolling-deployments.md)
