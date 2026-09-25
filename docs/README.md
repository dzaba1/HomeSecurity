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
5. **HTTPS-only for anything public-facing**, in every environment including
   local dev — see [ADR-0008](decisions/0008-https-everywhere.md).
6. **Contract-first, JSON Schema as the source of truth** for every
   public-facing model, with client code generated from it rather than
   hand-written — see
   [`architecture/08-api-contracts-and-codegen.md`](architecture/08-api-contracts-and-codegen.md).
7. **High availability by default.** Every service deploys via rolling
   updates without stopping the system — see
   [`architecture/11-deployment-and-availability.md`](architecture/11-deployment-and-availability.md).
8. **Every public API is versioned**, and the API meant to be integrated
   against (the Org/Admin-Tenant API) is HATEOAS — see
   [`architecture/12-api-versioning.md`](architecture/12-api-versioning.md) and
   [`architecture/13-hateoas-public-api.md`](architecture/13-hateoas-public-api.md).
9. **Every service scales out horizontally, on a signal that fits its own
   load shape**, not just up — see
   [`architecture/14-scalability.md`](architecture/14-scalability.md).

## Contents

- [`product/mvp-features.md`](product/mvp-features.md) — what the product does,
  written for potential users/customers rather than engineers.
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
- [`architecture/07-caching-and-idempotency.md`](architecture/07-caching-and-idempotency.md) —
  Redis: permission caching and idempotent event processing.
- [`architecture/08-api-contracts-and-codegen.md`](architecture/08-api-contracts-and-codegen.md) —
  JSON Schema as the source of truth for public contracts, and generating
  client libraries from it.
- [`architecture/09-observability.md`](architecture/09-observability.md) —
  logging, health checks, metrics, and distributed tracing across every
  service.
- [`architecture/10-frontend.md`](architecture/10-frontend.md) — the Admin
  UI: framework choice, project structure, and how it talks to the backend.
- [`architecture/11-deployment-and-availability.md`](architecture/11-deployment-and-availability.md) —
  zero-downtime rolling deployments and what every service needs to tolerate
  them.
- [`architecture/12-api-versioning.md`](architecture/12-api-versioning.md) —
  URI-based versioning for every public API.
- [`architecture/13-hateoas-public-api.md`](architecture/13-hateoas-public-api.md) —
  HATEOAS (HAL) for the Org (Admin/Tenant) API.
- [`architecture/14-scalability.md`](architecture/14-scalability.md) —
  horizontal autoscaling per service, and the stateful dependencies that
  actually limit it.
- [`architecture/15-router-credentials.md`](architecture/15-router-credentials.md) —
  how an org admin provisions a router's login in the Admin UI, and how
  agents fetch it, encrypted at rest, to log into the router themselves.
- [`decisions/`](decisions) — individual ADRs, one decision per file, numbered in
  the order they were made.

## Current status

The system is in active development. Three services exist in `src/dotnet`, each
independently buildable with its own solution file: `Org` (organizations,
memberships, roles/permissions), `LogsIngestion` (the agents' log intake) and
`Devices` (routers with encrypted credentials, network devices, and the
agent's router-config fetch — with its own database, per
[ADR-0016](decisions/0016-devices-service-owns-its-own-database.md)). What they
have in common lives in `src/dotnet/Common` as small shared libraries rather
than copies: domain types and permission keys, permission/tenant authorization
(with a cache and an Org-API-backed permission source), device-token
authorization, Redis cache and Data Protection wiring, the RabbitMQ message
bus, the per-service database provider, HAL and other web-API plumbing,
observability, and test utilities.

The first non-.NET service, `logs-processor` in `src/go/LogsProcessor`, is a Go
consumer of the log-ingestion queue — chosen on purpose to show services on the
message bus can use different technologies and share only the JSON-Schema
contract ([ADR-0019](decisions/0019-go-logs-processor-polyglot-consumer.md)).
For now it only takes messages off the queue; its processing method is an
empty stub.

Not built yet: the Admin UI, the device-token endpoint, agent pairing (the
`AgentRouterBinding` rows are only created directly in tests), the actual
processing of ingested logs (unknown-device detection), and push notification
delivery. See
[ADR-0006](decisions/0006-defer-auth-platform-extraction.md) for the current
stance on splitting auth out of this repo entirely.
