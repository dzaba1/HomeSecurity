# ADR-0008: HTTPS-only for every public-facing service

## Status

Accepted

## Context

Every externally reachable endpoint in this system — the Admin UI/BFF, the
Logs Ingestion API, and the device-token endpoint — is meant to be easy and
safe for an outside party (a browser, a mobile app, an agent, eventually a
third-party integrator) to talk to. Plaintext HTTP is also simply
incompatible with several decisions already made: OIDC/PKCE flows and secure
cookies (`oauth2-proxy`, ADR-0003) require HTTPS to be meaningful, and bearer
tokens (human or device, ADR-0004) must never travel in the clear.

## Decision

Every public-facing service is **HTTPS-only**, in every environment,
including local development — no plaintext fallback to design or code
against. Concretely:

- TLS is terminated at the edge (a single reverse proxy / ingress in front of
  the cluster), not hand-rolled per service. This is a ready-made piece of
  infrastructure (e.g. an ingress controller such as Traefik or nginx-ingress
  in Kubernetes, or a managed cloud load balancer), not custom code.
- Certificates are issued and renewed automatically via **cert-manager +
  Let's Encrypt** (or the cloud provider's equivalent) — no manual
  certificate handling anywhere.
- `Strict-Transport-Security` (HSTS) is enabled so that once a client has seen
  the service over HTTPS once, it will refuse to fall back to plain HTTP.
- Any HTTP request that does arrive at the edge is redirected to HTTPS, not
  served.
- Local development uses locally-trusted certificates (e.g. via `mkcert`)
  rather than disabling TLS, so "works on my machine over HTTP" is never a
  source of surprises in a real environment.

Internal, non-public traffic (e.g. between the Ingestion API and RabbitMQ,
inside the cluster's own network boundary) is out of scope for this decision
— it can be added later if the deployment's trust boundary calls for it, but
isn't required for "public-facing."

## Consequences

- No service in this system ever needs to implement its own TLS handling —
  it's entirely offloaded to the ingress/edge layer, in line with the
  project's "less custom code" principle.
- Any client integrating with the system (a browser, the mobile app, a future
  third-party) only ever needs to support one scheme.
- Adds one more piece of ready-made infrastructure to run (the ingress
  controller + cert-manager), which is a reasonable and standard cost for a
  public system.
