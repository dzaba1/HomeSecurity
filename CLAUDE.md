# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state (important)

This repo is currently **design-first, not implementation-first**. All
previously-written code lives under `old/` and predates the architecture now
recorded in `docs/`. Treat `docs/` as the target design and `old/` as
**read-only reference material** — useful for seeing patterns already tried
(multi-tenancy via EF Core global query filters, JSON-Schema-driven
contracts, NUnit test style), but not a codebase to build on top of or modify.
`old/TODO.md` and `old/UseCases.md` are earlier, rougher drafts of what
`docs/` now formalizes more precisely — prefer `docs/` when the two disagree.

Before writing new application code, check whether the work should start
fresh (matching the target architecture in `docs/`) rather than touching
`old/`.

## Documentation map

Read `docs/README.md` first — it states the guiding principles (prefer
ready-made components over custom code, Clean Architecture, multi-tenant from
day one, contract-first with JSON Schema, HTTPS-only everywhere) that every
design decision here follows.

`docs/product/` describes the product from a user's point of view.
`docs/architecture/` covers, one topic per file: the system context/component
overview; the multi-tenancy model; human vs. device/agent identity and auth;
the roles/permissions (RBAC) model; the agent app and log ingestion flow; the
unknown-device push notification feature; Redis's use for permission caching
and idempotency; and the JSON-Schema-first approach to public API contracts
and client codegen. `docs/decisions/` holds one ADR per file, numbered in
decision order — check here before revisiting a choice that's already been
evaluated.

## General coding principles

- **Register DI services as transient by default**, regardless of language/DI
  container. Reach for a scoped or singleton lifetime only when a service
  actually holds state that must be shared across resolutions within a
  scope/across the app (e.g. a connection multiplexer, a per-request tenant
  context) — not as a default choice.

## Architecture principles that apply to new code

1. **Prefer a ready-made container/library over custom code**, especially for
   auth, crypto, and session handling. Custom code is justified only where it
   implements this product's own domain logic.
2. **Clean Architecture**: domain/application logic has no dependency on
   frameworks, databases, or transport; infrastructure (EF Core, Keycloak,
   RabbitMQ, HTTP) is plugged in at the edges.
3. **Multi-tenant from day one**: every piece of domain data carries a
   `tenant_id`/`TenantId`; isolation is enforced by EF Core global query
   filters and verified by dedicated tenant-leak tests, never assumed.
4. **Contract-first**: any new public-facing model gets a JSON Schema file
   first; C# (and eventually TS/Kotlin/Swift) clients are generated from it,
   never hand-written.
5. **HTTPS-only**, in every environment including local dev.
6. **Every microservice must be independently buildable.** Each service gets
   its own solution file containing only its own projects plus the shared
   library projects it references — never a mono solution that every
   service's build/CI depends on. A repo-root convenience solution covering
   everything may still exist for local IDE use, but pipelines target each
   service's own solution, never the repo-root one.

## C# guidance

- **Central Package Management with transient pinning**: manage NuGet package
  versions via `Directory.Packages.props`
  ([CPM](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)),
  and explicitly pin transient dependencies there too rather than letting them
  float.
- Always use **`.ConfigureAwait(false)`** on awaited calls in library/service
  code.
- Don't use [`volatile`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile)
  for thread safety — prefer proper synchronization primitives
  (`lock`, `Interlocked`, etc.).
- Use **Serilog** for logging.
- Use **Microsoft.Extensions.DependencyInjection** for DI.
- Prefer utilities from [Dzaba.Utils](https://github.com/dzaba1/Dzaba.Utils)
  over hand-rolling equivalents, when applicable.
- On every public method/constructor, validate arguments with the
  `Argument...` exception helpers (`ArgumentNullException.ThrowIfNull`,
  `ArgumentException.ThrowIfNullOrEmpty`,
  `ArgumentOutOfRangeException.ThrowIfNegative`, etc.) for nullability,
  emptiness, and range — not hand-written `if`/`throw` checks.
- **Scaffold new projects with the `dotnet` CLI**, not hand-written files —
  `dotnet new sln --format slnx`/`classlib`/`webapi`/`nunit`, then
  `dotnet sln add` and `dotnet add reference`/`dotnet add package`. Use the
  `.slnx` (XML) solution format, not the legacy `.sln` format. Hand-edit the
  generated files afterward as needed, but let the SDK generate the initial
  `.csproj`/`.slnx` so they match what the installed SDK actually expects.
