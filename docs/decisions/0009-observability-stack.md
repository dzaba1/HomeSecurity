# ADR-0009: Observability stack — Serilog, OpenTelemetry, Prometheus, Grafana

## Status

Accepted

## Context

Every service in the design (Ingestion API, Processing Workers, Notification
Service, Admin UI/BFF) needs the same answer for logging, health reporting,
metrics, and tracing — see
[`09-observability.md`](../architecture/09-observability.md) for how each
piece is used. Nothing under `docs/` had picked a concrete stack for this
yet.

Per this project's first principle, the goal is to assemble ready-made,
widely-used pieces rather than build a logging/metrics framework — none of
this is the product's own domain logic.

## Decision

- **Logging**: **Serilog**, per [`CLAUDE.md`](../../CLAUDE.md), with
  structured (JSON) console output only. Shipping logs off the container
  (e.g. via Promtail/Fluent Bit into Loki) is left to the deployment
  environment, not configured per service.
- **Metrics and tracing**: the **OpenTelemetry .NET SDK**, using its
  standard ASP.NET Core / `HttpClient` / runtime instrumentation packages,
  exported via an **OpenTelemetry Collector** over OTLP.
- **Metrics storage/query**: **Prometheus**, fed either directly (Prometheus
  exporter scrape endpoint) or via the Collector's Prometheus exporter.
- **Trace storage**: **Grafana Tempo** (or Jaeger — an equivalent, ready-made
  container; not worth pinning further until an actual deployment target is
  chosen).
- **Dashboards**: **Grafana**, reading Prometheus (metrics), Loki (logs), and
  Tempo (traces) as its three data sources — the standard "LGTM" open-source
  observability stack.
- **Health checks**: ASP.NET Core's own
  `Microsoft.Extensions.Diagnostics.HealthChecks`, with the community
  `AspNetCore.HealthChecks.*` packages per dependency (Postgres, RabbitMQ,
  Redis) rather than hand-written probes, exposed as separate
  liveness/readiness endpoints for Kubernetes.

## Alternatives considered

- **A hosted/managed APM (Datadog, Application Insights, New Relic, etc.)**
  — would remove the Collector/Prometheus/Grafana operational surface
  entirely, and is a reasonable choice for a team with budget for it. Rejected
  here because this is a portfolio-scale project: the cost isn't justified,
  and OpenTelemetry's whole point is that instrumentation code doesn't need
  to change if a hosted backend is adopted later — only the exporter
  configuration would.
- **ELK (Elasticsearch/Logstash/Kibana) for logs** — a proven stack, but
  heavier to operate (JVM-based, more moving parts) than Loki for a project
  whose log volume doesn't need Elasticsearch's full-text search power. Loki
  is designed to pair with Prometheus/Grafana specifically, so picking it
  keeps the whole stack on one query/dashboard tool instead of two.
- **Rolling a custom `/health` endpoint by hand** — rejected outright; this
  is exactly the kind of well-solved, non-domain-specific problem the
  project's first principle says to buy rather than build, and the
  community `AspNetCore.HealthChecks.*` packages already cover every
  dependency this system has (Postgres, RabbitMQ, Redis).
- **Jaeger instead of Tempo** — functionally comparable; Tempo is listed as
  the default only because it shares Grafana's storage conventions
  (object-storage-backed, no separate index to run), not because Jaeger was
  found lacking. Revisit if a deployment target makes one clearly cheaper to
  run than the other.

## Consequences

- No service hand-rolls logging formatting, health-check logic, or a metrics
  pipeline — all four pillars (logs, health, metrics, traces) come from
  libraries already maintained upstream.
- Every service takes on the same, small set of dependencies (Serilog,
  OpenTelemetry SDK + instrumentation packages, `AspNetCore.HealthChecks.*`),
  so the pattern established for the Ingestion API is copy-paste for the
  Processing Worker, Notification Service, and Admin UI/BFF rather than
  reinvented per service.
- Trace context has to be explicitly propagated across the one boundary that
  isn't a direct HTTP call — RabbitMQ — via message headers, since
  OpenTelemetry's automatic instrumentation only covers HTTP and DB calls out
  of the box. This is called out explicitly in
  [`09-observability.md`](../architecture/09-observability.md) so it isn't
  missed when the Ingestion API and Processing Worker are actually built.
- Adds four more ready-made containers to run locally/in the cluster
  (Prometheus, Loki, Tempo, Grafana, plus the OpenTelemetry Collector) — a
  reasonable, standard cost for the local Docker Compose stack to carry
  alongside the rest of the system.
