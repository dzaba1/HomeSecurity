# HomeSecurity — Architecture & Design Docs

This folder is the design record for the project: what we're building, why, and the
decisions behind it. It's meant to read like an internal engineering wiki, not
marketing copy — it should stay honest about trade-offs and about what's still
just a plan vs. what's actually built.

## Guiding principles

These apply to every decision recorded here:

1. **Less code, more ready-made pieces.** Prefer a well-known container, library,
   or platform feature over writing and maintaining custom code, especially for
   security-sensitive concerns (auth, crypto, session handling). Custom code is
   justified only where the product's own domain logic requires it.
2. **Clean Architecture** for anything we do write: domain/application logic has
   no dependency on frameworks, databases, or transport; infrastructure concerns
   (EF Core, Keycloak, RabbitMQ, HTTP) are plugged in at the edges.
3. **Multi-tenant from day one.** Every piece of domain data belongs to a tenant
   (organization); isolation is enforced in code and verified by tests, not
   assumed.
4. **Document the "why", not just the "what".** See [`decisions/`](decisions) for
   the individual Architecture Decision Records (ADRs).

## Contents

- [`architecture/01-overview.md`](architecture/01-overview.md) — system context,
  high-level components, and how they fit together.
- [`architecture/02-multi-tenancy.md`](architecture/02-multi-tenancy.md) — tenant
  model and data isolation strategy.
- [`architecture/03-security-and-identity.md`](architecture/03-security-and-identity.md) —
  Keycloak, human login (BFF), and device/agent authentication.
- [`architecture/04-roles-and-permissions.md`](architecture/04-roles-and-permissions.md) —
  the permission catalog / custom roles model.
- [`architecture/05-agents-and-ingestion.md`](architecture/05-agents-and-ingestion.md) —
  the router-log agent applications and the ingestion pipeline.
- [`architecture/06-notifications.md`](architecture/06-notifications.md) — the
  MVP feature: push notifications for unknown devices on the network.
- [`decisions/`](decisions) — individual ADRs, one decision per file, numbered in
  the order they were made.

## Current status

The system is in active design/prototyping. The `Dzaba.HomeSecurity.Auth` and
`Dzaba.Org` projects in `src/dotnet` are experimental scaffolding from earlier
iterations and are expected to be reshaped or replaced as the decisions in this
folder are implemented — see [ADR-0006](decisions/0006-defer-auth-platform-extraction.md)
for the current stance on splitting auth out of this repo entirely.
