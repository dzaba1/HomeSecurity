# ADR-0011: Zero-downtime deployments via Kubernetes rolling updates

## Status

Accepted

## Context

The system needs to be deployable without stopping — releasing a new version
of any service (Ingestion API, Org API, Processing Workers, Notification
Service, Admin UI/BFF) should never require a maintenance window. Nothing
under `docs/` had settled a concrete deployment mechanism for this yet, even
though [`09-observability.md`](../architecture/09-observability.md) already
assumes a Kubernetes-shaped runtime (liveness/readiness endpoints) and
[ADR-0008](0008-https-everywhere.md) already assumes an ingress edge in
front of the cluster.

## Decision

- Every service is a Kubernetes **`Deployment`** using the **`RollingUpdate`**
  strategy (`maxUnavailable: 0`, `maxSurge: 1` or higher): new pods start and
  pass readiness before old ones are removed, so total serving capacity never
  drops during a rollout.
- New pods gate on the existing **`/health/ready`** endpoint
  ([`09-observability.md`](../architecture/09-observability.md)) before
  receiving traffic — no new health mechanism, the one already designed for
  observability is what a rollout relies on.
- Every service runs **at least 2 replicas** wherever uptime during a deploy
  matters — a single replica can't roll without a gap regardless of strategy.
- A **`PodDisruptionBudget`** per service protects the same guarantee against
  involuntary disruption (node drains, autoscaling, node upgrades), not just
  application deploys.
- Every service is **stateless**: no session or request state lives in a
  pod's memory, so any replica can serve any request and a pod can be
  recycled mid-rollout without losing anything beyond one in-flight call.
- Database migrations and RabbitMQ message contracts that aren't compatible
  with the previous running version follow the standard
  **expand/contract** pattern across two deploys, since old and new pods run
  against the same DB/queue simultaneously for the rollout's duration.

## Alternatives considered

- **Blue/green deployment** — equally zero-downtime, with a faster rollback,
  but doubles infrastructure cost for every deploy and needs an external
  traffic-switch step. Rolling updates plus readiness gating meet the
  requirement far more cheaply, which fits this project's scale. Worth
  revisiting only if rollback speed becomes a real incident-response
  problem.
- **Recreate strategy** — rejected outright; causes downtime by definition.
- **Canary releases** — a useful refinement on top of rolling updates, not
  an alternative to them; not needed for v1 at this traffic scale. The
  actual protection against a bad deploy breaking clients comes from
  versioned public contracts (ADR-0012), which canary would only
  supplement.

## Consequences

- No service, schema, message contract, or public API can assume it's the
  only version of itself running — everything must tolerate the old and new
  version of a service coexisting for the duration of a rollout (and for as
  long as a deprecated API version is deliberately kept around).
- This is the direct reason public APIs need explicit versioning — see
  [ADR-0012](0012-uri-based-api-versioning.md) and
  [`12-api-versioning.md`](../architecture/12-api-versioning.md).
- Adds `PodDisruptionBudget` management and the expand/contract migration
  discipline to every service's operational surface — a standard,
  well-understood cost for a system that wants genuine zero-downtime
  deploys, not a bespoke one.
