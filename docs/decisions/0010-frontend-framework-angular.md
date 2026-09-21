# ADR-0010: Angular for the Admin UI frontend

## Status

Accepted

## Context

The Admin UI (see [`01-overview.md`](../architecture/01-overview.md)) is the
only browser-facing frontend in the system, and no frontend framework had
been picked yet. Two things already decided elsewhere narrow the choice
before it's even made:

- [ADR-0003](0003-bff-for-admin-ui.md): `oauth2-proxy` handles the entire
  login flow and hands the browser an HttpOnly session cookie. **The
  frontend never touches a token, stores credentials, or implements any auth
  flow of its own** — the framework choice has nothing to do with auth
  capability, since all three mainstream candidates are equally fine with
  "just make same-origin HTTP calls."
- [`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md):
  request/response shapes are generated from JSON Schema via an OpenAPI
  document and `openapi-generator`, never hand-written.

What the Admin UI actually needs to do, per
[`mvp-features.md`](../product/mvp-features.md), is a fairly ordinary
line-of-business CRUD/dashboard job: create an organization, list/manage
devices, mark a device as recognized, show alerts. Nothing here needs
SEO, server-side rendering, or a public marketing surface — it's entirely
behind an authenticated session.

Per this project's first principle (prefer a ready-made piece over custom
code) and the fact that nobody on this project currently has deep, current
frontend experience, the framework that wins should minimize the number of
*other* decisions (routing, forms, HTTP client, dependency injection, test
runner) a newcomer has to research and wire together by hand.

## Decision

Use **Angular** (current LTS release, via the Angular CLI) for the Admin UI.

Reasons, in order of weight:

1. **Batteries included.** Angular ships router, forms, an HTTP client, and
   a dependency-injection container as part of the framework itself, not as
   separate ecosystem choices. That directly serves principle 1: fewer
   library decisions to make, fewer ways to wire them together wrong, and
   one official place (the Angular docs) to look things up instead of
   several competing community opinions.
2. **First-class codegen fit.** `openapi-generator` ships a dedicated
   `typescript-angular` generator that emits Angular-injectable,
   `HttpClient`-based services directly from the OpenAPI document described
   in [`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md) —
   no hand-written wrapper or data-fetching layer needed on top, the
   generated service just gets injected like any other Angular service.
   The generic `typescript-fetch`/`typescript-axios` generators (what
   React or Vue would consume) still need that glue code hand-written.
3. **Conceptual overlap with the existing backend.** The C# side already
   leans on constructor-injected services and Clean Architecture layering.
   Angular's DI-based structure is the closest mainstream frontend
   equivalent, which should shorten the learning curve for someone new to
   frontend work but coming from that background.
4. **TypeScript-native.** Angular is written in and designed around
   TypeScript, not JavaScript with types bolted on, so it lines up cleanly
   with the generated, strongly-typed contracts from item 2.
5. **Predictable, long-term maintenance.** Google-backed release cadence
   and `ng update` for guided major-version upgrades suit a project meant to
   be maintained over years, not optimized for a large team shipping fast
   with maximum library flexibility.

## Alternatives considered

- **React** — the largest ecosystem and hiring pool, and extremely
  flexible. Rejected as the *default* here precisely because that
  flexibility cuts against principle 1: React itself only renders
  components, so routing, forms, HTTP/data-fetching, and state management
  are each a separate library someone unfamiliar with the current
  ecosystem has to research, choose, and glue together. A very reasonable
  choice to revisit if the team grows and wants that flexibility or a
  bigger talent pool.
- **Vue** — a gentler learning curve than either alternative and good
  documentation. Rejected mainly because its ecosystem for enterprise-grade
  admin/data-grid components is smaller than React's or Angular's, and
  `openapi-generator` has no first-class Vue client generator either — it
  lands in the same "generic client + hand-written wiring" bucket as React.
- **Svelte/SvelteKit** — excellent developer experience and small bundle
  sizes, but the smallest ecosystem of the four for ready-made
  enterprise/admin components, meaning the most custom glue code of any
  option here — directly against principle 1.
- **A meta-framework with SSR (Next.js, Nuxt, Angular SSR/Analog)** —
  rejected. The Admin UI has no public or SEO-facing pages; every page sits
  behind the `oauth2-proxy` session (ADR-0003). SSR would add a Node.js
  server runtime and rendering complexity that buys nothing here.

## Consequences

- The Admin UI project is scaffolded with `ng new`, standalone components
  (no `NgModule`s), and TypeScript strict mode on.
- The client-codegen step in
  [`08-api-contracts-and-codegen.md`](../architecture/08-api-contracts-and-codegen.md)
  targets `openapi-generator`'s `typescript-angular` generator for the
  Admin UI's client, rather than a generic TypeScript client.
- Component-library choice (e.g. Angular Material) and other
  implementation-level detail are left to
  [`10-frontend.md`](../architecture/10-frontend.md) rather than pinned in
  this ADR.
- If the team grows significantly and wants broader frontend hiring
  flexibility, or a second, public/SEO-facing surface is added later, this
  decision is worth revisiting — neither condition holds today.
