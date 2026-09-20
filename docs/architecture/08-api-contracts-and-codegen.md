# API Contracts & Client Codegen (JSON Schema)

## The principle

Every model that crosses a public-facing boundary — a request or response
body for the Admin/Tenant API, the Logs Ingestion API, or the device-token
endpoint — is defined **once**, as a **JSON Schema** file, and that file is
the single source of truth. Hand-written DTOs that can silently drift from
what the API actually accepts are what we're avoiding.

This isn't a new idea for this codebase — it's already how `Dzaba.Org.Contracts`
works today, this document just makes the pattern explicit and says it
applies to every future public contract too.

## What already exists

- Schemas live under `src/contracts/json/` (currently `organization.json`,
  `create_organization.json`, `role.json`, `permission.json`,
  `log_event.json`).
- `build/JsonSchema.targets` is a shared MSBuild target that runs
  **NSwag's `jsonschema2csclient`** at build time to generate a strongly
  typed C# class per schema file, straight into the relevant `*.Contracts`
  project (see `Dzaba.Org.Contracts.csproj`). The generated class is
  regenerated on every build and never hand-edited.

```xml
<JsonSchema Include="..\..\..\contracts\json\organization.json"
            Link="organization.json"
            OutputFile="Organization.cs"
            ClassName="Organization" />
```

## The convention going forward

Any new public contract (e.g. the `Device` and push-notification payloads
from [`06-notifications.md`](06-notifications.md)) follows the same three
steps:

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
   schemas — not a second, separately maintained copy of the shapes.
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
