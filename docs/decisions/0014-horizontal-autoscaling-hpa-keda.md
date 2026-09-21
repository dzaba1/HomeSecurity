# ADR-0014: Horizontal autoscaling via Kubernetes HPA, KEDA for the queue consumer

## Status

Accepted

## Context

[ADR-0011](0011-zero-downtime-rolling-deployments.md) fixed a replica
*floor* (at least 2) for zero-downtime deploys and involuntary-disruption
tolerance, but deliberately left "how many replicas run under load" out of
scope. The services in this system split into two load shapes:

- Request-driven services (Ingestion API, Notification Service, Admin
  UI/BFF) where load shows up as CPU and request rate on the pod.
- The Processing Worker, which pulls from a RabbitMQ queue
  ([ADR-0007](0007-rabbitmq-as-message-bus.md)) rather than serving
  requests, so its CPU can be idle while a real backlog builds up — the
  signal that matters for it is queue depth, which plain Kubernetes
  autoscaling can't read.

## Decision

- Request-driven services use a plain Kubernetes **`HorizontalPodAutoscaler`**
  against CPU utilization (a custom request-rate metric later, if needed) —
  no extra component, since HPA already ships with Kubernetes and already
  fits this load shape.
- The Processing Worker uses **KEDA** (`ScaledObject` with the `rabbitmq`
  scaler, `queueLength` trigger) to scale on actual queue depth, including
  scaling to zero when idle. KEDA drives the same underlying HPA mechanism;
  it only supplies the metric HPA can't read on its own.
- Every `HorizontalPodAutoscaler`/`ScaledObject`'s `minReplicas` respects the
  HA floor from [ADR-0011](0011-zero-downtime-rolling-deployments.md) in any
  environment where uptime during load spikes matters; only non-critical
  environments allow scale-to-zero.

See [`14-scalability.md`](../architecture/14-scalability.md) for the full
design, including why stateful dependencies (Postgres, RabbitMQ, Redis) — not
pod count — are the actual scaling limit in this system.

## Alternatives considered

- **Custom metrics adapter (e.g. `prometheus-adapter`) feeding a plain HPA
  off a RabbitMQ queue-depth metric scraped via Prometheus** — achieves the
  same outcome as KEDA but requires standing up and maintaining a
  Prometheus-to-external-metrics bridge and hand-writing the metric query,
  where KEDA ships a maintained RabbitMQ scaler purpose-built for exactly
  this. Rejected as more moving parts for no benefit, against the project's
  first principle (prefer a ready-made component).
- **Hand-rolled controller polling the queue and calling the Kubernetes API
  to set replica count** — explicitly the kind of custom code the project's
  first principle exists to avoid; KEDA already does this, maintained and
  battle-tested.
- **Fixed replica count, sized for peak load, no autoscaling** — simplest to
  reason about, but wastes capacity (and cost) outside peak, and doesn't
  scale to zero in non-production environments where idle time dominates.
  Rejected once KEDA's scale-to-zero made "pay only for backlog" the cheaper
  default at this project's scale.

## Consequences

- Two autoscaling mechanisms in the cluster (plain HPA, and KEDA driving
  HPA) rather than one — an accepted cost because they solve genuinely
  different load shapes; using CPU-based HPA for the Processing Worker would
  under-scale it during a real backlog, and using KEDA everywhere would add
  a RabbitMQ-shaped dependency to services that don't need one.
- KEDA becomes a cluster-wide operator dependency (CRDs + controller) that
  every environment running the Processing Worker needs installed.
- Autoscaling now shifts the practical bottleneck onto the stateful
  dependencies each replica shares (Postgres connections above all) — see
  [`14-scalability.md`](../architecture/14-scalability.md) for the mitigation
  path (PgBouncer) once that becomes real.
