# Frontend (Admin UI)

The Admin UI is the one browser-facing frontend in the system (see
[`01-overview.md`](01-overview.md)) — where a user manages their
organization, devices, and alerts (see
[`../product/mvp-features.md`](../product/mvp-features.md)). It carries no
auth logic of its own: `oauth2-proxy` (see
[`03-security-and-identity.md`](03-security-and-identity.md) and
[ADR-0003](../decisions/0003-bff-for-admin-ui.md)) has already turned "is
this user logged in" into "does this request carry a valid session cookie"
before a request ever reaches it.

## Framework

**Angular**, current LTS, via the Angular CLI. See
[ADR-0010](../decisions/0010-frontend-framework-angular.md) for why.

## Project structure

- Scaffolded with `ng new`, using standalone components (no `NgModule`s) —
  the current Angular default, and less boilerplate than the older
  module-based structure.
- Feature-based folders (`organizations/`, `devices/`, `alerts/`, …) rather
  than layer-based ones (one folder of all services, another of all
  components) — each feature keeps its own routes, components, and services
  together, and the folders map onto the product's own concepts instead of
  a generic MVC-style split.
- `core/`: cross-cutting singletons — the generated API client's providers,
  an HTTP interceptor that reacts to a `401` (session expired) by sending
  the user back through `oauth2-proxy`'s login redirect, and any global
  error handling.
- `shared/`: presentational components reused across features (e.g. a
  device-status badge), with no feature-specific logic in them.

## Generated API client

Per [`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md),
the OpenAPI document built from `src/contracts/json/*.json` feeds
`openapi-generator`'s `typescript-angular` generator:

- Output lands in its own generated folder, regenerated on every build and
  never hand-edited — the same rule already followed for the generated C#
  contracts.
- Generated services are plain `providedIn: 'root'` Angular injectables
  built on `HttpClient`, so a feature component just injects one like any
  other service. No separate data-fetching library sits on top for the
  MVP's scope.
- The frontend never hand-writes a DTO/interface for anything the backend
  already defines a schema for — if a shape needs to change, the schema
  changes and the client regenerates, same as the C# side.

## State management

No global state library (NgRx or similar) for the MVP. Angular's own
`signal`/`computed` primitives are enough for the scope in
[`mvp-features.md`](../product/mvp-features.md) — a handful of screens with
state that's mostly a direct reflection of what the backend returns.

Don't add a state-management framework speculatively; revisit only once
cross-feature shared state or non-trivial derived state actually shows up.

## Component library / styling

**Angular Material** as the base component set (forms, tables, dialogs,
navigation) — first-party, released in lockstep with Angular itself, and
built on Angular's own CDK/a11y primitives rather than a third-party
abstraction.

If a specific screen outgrows `mat-table` (for example, the device list
needs richer sorting/filtering than Material's table offers), reach for the
`@angular/cdk` table primitives, or a focused library like PrimeNG, for
just that one component rather than replacing the whole library.

## Talking to the backend

All calls are same-origin, through `oauth2-proxy`. A request without a
valid session gets redirected to Keycloak login by the proxy itself, so the
frontend only has to react to that redirect (or a `401`, via the interceptor
in `core/`) — it never implements a login flow of its own.

## Testing

- **Unit tests**: whatever `ng new` scaffolds by default for the Angular
  version in use — take the CLI's current default test runner rather than
  hand-picking or hand-configuring a different one.
- **End-to-end**: **Playwright**, covering the golden paths from
  `mvp-features.md` — create an organization, pair a device (simulated),
  see it appear in the device list, mark it as recognized.

## Build & deploy

- `ng build` produces static output only — served by a static file
  server/CDN behind `oauth2-proxy`. No Node.js server is needed at runtime,
  since there's no server-side rendering (see ADR-0010's rejected
  alternatives).
- HTTPS end-to-end per [ADR-0008](../decisions/0008-https-everywhere.md) —
  the Admin UI's own origin is served over HTTPS in every environment, not
  just the APIs it calls.

## Related documents

- [ADR-0010](../decisions/0010-frontend-framework-angular.md) — why Angular
- [ADR-0003](../decisions/0003-bff-for-admin-ui.md) — BFF for the Admin UI
- [`03-security-and-identity.md`](03-security-and-identity.md)
- [`08-api-contracts-and-codegen.md`](08-api-contracts-and-codegen.md)
