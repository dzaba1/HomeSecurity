# ADR-0007: RabbitMQ as the message bus

## Status

Accepted

## Context

Log ingestion needs to be decoupled from log processing: the Ingestion API
must stay thin and fast (accept, validate, acknowledge), while the actual
enrichment/detection work in the Processing Workers can take longer and scale
independently. That requires a durable queue between them.

Kafka was considered at one point (for a Keycloak-events-to-audit-log idea),
but that idea itself was resolved to use a webhook onto the same broker
instead of introducing a second one (see
[`04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md)
context and prior discussion) — there's no scenario in this system that
actually needs Kafka's log-retention/replay/partition model.

## Decision

Use **RabbitMQ** as the one message bus for the whole system:

- Ingestion API → RabbitMQ → Processing Workers (log batches)
- Processing Workers → RabbitMQ → Notification Service (`UnknownDeviceDetected`
  and future domain events)

Reasons over Kafka, for this project specifically:
- Simpler to run and reason about for a single-broker, moderate-throughput
  workload — no ZooKeeper/KRaft, no partition/consumer-group tuning.
- Its consumer/ack/dead-letter-queue model maps directly onto "process this
  message once, retry on failure, park it if it keeps failing" — exactly
  what's needed here, without needing log-replay semantics.
- Has a dedicated, broker-agnostic `IMessageBus` abstraction
  (`Dzaba.HomeSecurity.MessageBroker.Contracts`) with the EasyNetQ-backed
  implementation in its own separate project
  (`Dzaba.HomeSecurity.MessageBroker.RabbitMQ`), so no service's
  application/controller code — or even its `PackageReference` list — ever
  touches RabbitMQ/EasyNetQ directly; swapping brokers later would only
  touch that one implementation project, and both projects are reusable
  as-is by future ingestion services.

## Consequences

- Durable queues with dead-letter queues configured for anything that can't
  be processed after retries, so failures are visible rather than silently
  dropped.
- Because RabbitMQ (like virtually any broker) gives **at-least-once**
  delivery, consumers must be written to tolerate redelivery — see
  [`07-caching-and-idempotency.md`](../architecture/07-caching-and-idempotency.md)
  for how Redis is used to make the Processing Worker's handling of a
  redelivered message a no-op.
- One broker for the whole system keeps operational surface small — no
  second message-bus technology to run, monitor, or explain in the
  portfolio unless it's actually earning its place.
- Message payloads (e.g. `LogBatchMessage`) are JSON-Schema-first, same as
  HTTP contracts — see
  [`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md).
  A producer and its consumer(s) each generate their own binding from the
  same schema file rather than sharing a compiled package, which keeps a
  future non-.NET consumer (e.g. a Go Processing Worker) free to generate
  its own client from that same schema without needing anything from this
  codebase's C# projects.
