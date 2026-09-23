# API Contracts & Client Codegen (JSON Schema)

## The principle

Every model that crosses a process boundary — a request or response body for
the Admin/Tenant API, the Logs Ingestion API, or the device-token endpoint,
**and** a message published to RabbitMQ for another service to consume — is
defined **once**, as a **JSON Schema** file, and that file is the single
source of truth. Hand-written DTOs that can silently drift from what the API
actually accepts (or what a consumer actually needs to parse) are what we're
avoiding. This applies just as much to message-bus payloads as to HTTP
bodies: a `LogBatchMessage` published to RabbitMQ is exactly as much a public
contract as an HTTP request body — it's consumed by a different service,
possibly written in a different language, and needs the same "one source of
truth, generate a binding per consumer" treatment.

This isn't a new idea for this codebase — it's already how `Dzaba.Org.Contracts`
works today, this document just makes the pattern explicit and says it
applies to every future contract, HTTP or message-bus, too.

## What already exists

- Schemas live under `src/contracts/json/`: the Org API's models
  (`organization.json`, `create_organization.json`, `role.json`,
  `permission.json`, plus a few more following the same shape), the Logs
  Ingestion API's request (`log_event.json`), and its message-bus
  counterpart (`log_batch_message.json`).
- `build/JsonSchema.targets` is a shared MSBuild target that runs
  **NSwag's `jsonschema2csclient`** at build time to generate a strongly
  typed C# class per schema file, straight into the relevant `*.Contracts`
  project. The generated class is regenerated on every build and never
  hand-edited.

```xml
<JsonSchema Include="..\..\..\contracts\json\organization.json"
            Link="organization.json"
            OutputFile="Organization.cs"
            ClassName="Organization" />
```

## The convention going forward

Any new contract — HTTP or message-bus — follows the same three steps:

1. Add a `.json` file under `src/contracts/json/` describing the shape
   (`$schema`, `type`, `properties`, `required`, `additionalProperties:
   false`).
2. Reference it from the owning `*.Contracts` project via a `<JsonSchema>`
   item, same as the existing entries.
3. Never hand-write or hand-edit the generated C# class — if the shape needs
   to change, the schema changes, and the class regenerates.

This gets us, for free: no drift between "what the schema says" and "what the
code actually does," and request-body validation that can be driven directly
from the same schema instead of hand-written validation attributes.

### Reusing a shape across multiple schemas

A message-bus contract and the HTTP contract that produces it often share
part of their shape (the Logs Ingestion API's `log_event.json` and its
`log_batch_message.json` both carry the same per-device log entries). Don't
copy-paste the shared shape between the two files — pull it into its own
schema file instead and `$ref` it by filename from both:

```
src/contracts/json/
  network_device.json   ← "library" schema: a device seen on the LAN
  log_entry.json         ← "library" schema: one normalized log line
  device_logs.json       ← "library" schema: $ref's the two above
  log_event.json         ← HTTP contract: $ref's device_logs.json
  log_batch_message.json ← message-bus contract: $ref's device_logs.json
```

Two things to know about how this interacts with the C# codegen:

- **A `$ref`'d schema's generated class is named after its *filename*, not
  its `title`.** All schema files in this directory follow the same naming
  convention (snake_case, matching the older Org schemas) rather than
  switching case just for files meant to be `$ref`'d — the tradeoff is that
  the generated C# type for e.g. `network_device.json` comes out as
  `Network_device`, not `NetworkDevice`. That's an accepted, deliberate
  choice (consistency of the schema directory over prettier generated
  names) — don't "fix" it by renaming just the library schemas to
  PascalCase, and don't hand-edit the generated class to rename it either.
- **When two `<JsonSchema>` entries in the same project both (transitively)
  `$ref` the same library schema, generate that library schema as its own
  `<JsonSchema>` entry too, and add its class name to the other entries'
  `ExcludedTypeNames`** (a `JsonSchema.targets` metadata item, comma
  separated). Without this, each entry that pulls in the shared schema
  regenerates its own copy of the type, which collides as a duplicate C#
  type once both live in the same namespace. See
  `Dzaba.HomeSecurity.LogsIngestion.Contracts.csproj` for a worked example
  (`network_device.json`/`log_entry.json`/`device_logs.json` generated once,
  `log_event.json` and `log_batch_message.json` both exclude them).

### Message-bus contracts don't share a compiled binding

Unlike two HTTP clients that might reasonably share a generated NuGet
package, a message's producer and its consumer(s) each generate their own
C# (or Go, or Python, or whatever) binding from the same schema file — they
don't reference one shared assembly. That's the point of using JSON Schema
here instead of, say, a shared `.Messages` project referenced by every
service: the schema file is the contract, not the generated code, so a
consumer written in a different language never needs a C# assembly at all.

## Beyond C#: generating clients in other languages

`jsonschema2csclient` only covers the C# side (used internally, e.g. by the
Admin UI backend or between .NET services). The stated goal — quickly
generating **client libraries** for integrators — extends further: a
TypeScript client for a web integration, or Kotlin/Swift clients for the
mobile app and future third-party mobile integrations.

The plan for that layer:

1. Describe each public API's endpoints (paths, methods, status codes) as an
   **OpenAPI document**, with each request/response body referencing the
   *same* JSON Schema files under `src/contracts/json/` as its component
   schemas — not a second, separately maintained copy of the shapes. Each
   major API version ([`12-api-versioning.md`](12-api-versioning.md)) gets
   its own OpenAPI document; for the Org API, that document also carries the
   HATEOAS `_links` envelope ([`13-hateoas-public-api.md`](13-hateoas-public-api.md)),
   which lives at the OpenAPI layer rather than in the per-model JSON Schema
   files.
2. Feed that OpenAPI document into **`openapi-generator`** (a mature,
   widely-used, ready-made tool) to produce a client SDK in whichever
   language a given integration needs — TypeScript, Kotlin, Swift, another
   C# client, etc.

This keeps exactly one hand-maintained source of truth per model (the JSON
Schema file) while making "generate a client for language X" a config-driven,
one-command operation rather than something anyone writes by hand.

```
src/contracts/json/*.json  (source of truth, one file per model)
        │
        ├──> NSwag jsonschema2csclient ──> C# classes (internal .NET use)
        │
        └──> referenced by an OpenAPI spec ──> openapi-generator ──> TS / Kotlin / Swift / … clients
```

## Alternatives considered

- **Hand-write OpenAPI first, generate JSON Schema from it** — rejected;
  it's backwards from the existing, working pattern in this codebase, and
  would mean the "source of truth" for a model's shape lives inside a larger
  API-level document instead of a focused, reusable schema file.
- **Protobuf/gRPC contracts** — much stronger for internal service-to-service
  calls, but a worse fit for public-facing HTTP APIs that need to be easy for
  arbitrary external integrators (including simple `curl`/browser clients) to
  consume — JSON Schema over plain HTTPS/JSON keeps the barrier to
  integration low, which is the whole point per [ADR-0008](../decisions/0008-https-everywhere.md).
