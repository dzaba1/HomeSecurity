# ADR-0019: The logs consumer is a Go service (polyglot services on the bus)

## Status

Accepted

## Context

[ADR-0007](0007-rabbitmq-as-message-bus.md) put RabbitMQ between the Logs
Ingestion API and whatever processes log batches, and
[`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md)
made the message contract JSON-Schema-first so that a producer and its
consumers each generate their own binding instead of sharing a compiled
package. Every service so far is C#, which leaves that independence untested.

The first consumer of `logs.ingested` was needed, and it only has to take
messages off the queue for now — real processing comes later.

## Decision

Build the consumer as a **Go service**, `src/go/LogsProcessor` (`logs-processor`),
to demonstrate that in a SaaS/cloud setup services on a message bus can use
different technologies and stay independent of each other. The only thing it
shares with the C# producer is the JSON Schema in `src/contracts/json/`.

- **Contract:** Go types are generated from `log_batch_message.json` (and the
  schemas it `$ref`s) with `go-jsonschema`, pinned as a `tool` in the module's
  `go.mod`. The output is committed; CI re-runs `go generate` and fails on a
  diff, so the schema and the bindings cannot drift.
- **Own queue and dead-lettering:** the service declares its own durable
  *quorum* queue `homesecurity.logs-processor`, bound to the `homesecurity.events`
  exchange with routing key `logs.ingested`. The queue has
  a delivery limit and a dead-letter exchange/queue (`.dlx` / `.dlq`), which is
  the "retry, then park it in a DLQ" behaviour ADR-0007 asks for without any
  custom retry code. Malformed messages are dead-lettered immediately; a
  processing error requeues.
- **A language-neutral exchange:** EasyNetQ's default `PubSub` names an exchange
  after the .NET message type (`Namespace.Type, Assembly`), which would make
  every non-.NET consumer depend on a C# type name. `RabbitMqMessageBus` therefore
  publishes every domain event to one explicitly named durable topic exchange,
  `homesecurity.events` (`ExchangeNames.Events`), and the routing key
  (`logs.ingested`, `membership.added`, ...) identifies the event. This changes the
  exchange for the other publishers too (Org, Devices) - they all go through the
  same bus class - and no consumer of the old per-type exchanges exists.
- **Libraries:** `amqp091-go` for RabbitMQ and the standard library's `log/slog`
  for structured JSON logging (the `ILogger<T>` counterpart) — no custom
  framework.
- **Independently buildable:** own `go.mod`, Dockerfile and CI job, per the
  "every microservice builds on its own" rule.
- **Scope now:** the processing method is an empty stub with a TODO. No Redis
  idempotency, no database, no downstream events yet.

## Consequences

- This is the repo's first non-C# code; CI gets a Go reusable workflow next to
  the .NET one, and the Go toolchain is needed to work on it.
- **Ownership caveat:** [ADR-0016](0016-devices-service-owns-its-own-database.md)
  says whatever writes `Device` rows lives inside `Devices.Service`. When real
  processing arrives, this Go service must hand its results to Devices via an
  API or an event rather than write Devices' database — or unknown-device
  detection moves into `Devices.Service` and this service keeps only the
  non-device parts of the pipeline.
- The consumer is stateless, so it still scales horizontally on queue length
  (KEDA, [ADR-0014](0014-horizontal-autoscaling-hpa-keda.md)).
