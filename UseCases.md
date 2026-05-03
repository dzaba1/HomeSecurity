# Use cases

## Purpose

I want to have a cloud system which receives home router logs and sends back security notifications.

Home devices like PCs, mobiles, or TVs should be able to get router logs periodically and send them to the system.

Security event to notify:
* New connected device with unknown IP / MAC 

Notifications should go to the mobile application.

## Backend

Here I'm puting some desired backend use cases. Frontend use cases and UI/UX design will be in a separate document.

* Users can login and manage their organizations.
* Users are able to register devices into their organizations.
* API and users are able to create API tokens

### Architecture

```mermaid
flowchart TB
  subgraph External
    Client[Customer Router / Device]
    Mobile[Mobile App]
    AdminUI["Management UI (web)"]
    PushGateway[FCM / APNs]
  end

  subgraph Cloud[Kubernetes / Cloud Services]
    API[API Gateway / Ingress]
    Auth[Auth & API-Key Service]
    AuthDB[(Auth DB — Postgres)]
    Ingest["Logs Ingestion Service (stateless)"]
    Redis["Redis Cache (optional)"]
    Queue[RabbitMQ / Event Bus]
    Worker["Processing Workers / Consumers (autoscaled)"]
    DeviceDB[(Device & Organization DB — Postgres)]
    EventsStore[(Event Archive — S3 / Blob)]
    Notifier[Notification Service]
    Management[Management Service / Org & Device API]
    Observability[Prometheus + Grafana / OpenTelemetry]
    Tracing[Distributed Trace Collector]
    Secrets["Secrets Manager (Vault / KeyVault)"]
  end

  Client -->|HTTPS + API Key / mTLS| API
  API --> Auth
  Auth --> AuthDB
  API --> Ingest
  AdminUI -->|HTTPS + OAuth2| Auth
  AdminUI --> Management
  Management --> AuthDB
  Mobile -->|Register Device / View Alerts| Management
  Management --> DeviceDB
  Ingest -->|cache| Redis
  Ingest -->|publish| Queue
  Queue --> Worker
  Worker --> DeviceDB
  Worker --> EventsStore
  Worker --> Notifier
  Notifier -->|push| PushGateway
  Management -->|publish| Queue
  Cloud --> Observability
  Cloud --> Tracing
  API -.-> Secrets
  Worker -.-> Secrets
  Management -.-> Secrets
```

Components & responsibilities

- **API Gateway:** TLS termination, rate limiting, auth, ingress routing, request validation.
- **Auth & API-Key Service:** Issue/manage API keys and tokens, support OAuth2 for users and API keys for routers, enforce scopes and rate-limits.
- **Logs Ingestion Service:** Stateless HTTP/HTTPS front-end that validates and normalizes router logs and publishes messages to the event bus; keep minimal CPU work.
- **Event Bus (RabbitMQ):** Durable queueing for decoupling ingestion from processing; use persistence and dead-letter queues.
- **Processing Workers:** Consumer pool that enriches, deduplicates, classifies logs, applies detection rules (e.g., unknown device), writes events to archive and device DB, and emits notification events.
- **Device & Organization DB (Postgres):** Canonical store for organizations, devices, owners, provisioning tokens and mappings; strong constraints and indexes for lookups.
- **Event Archive (S3/Blob):** Append-only storage for raw and processed events for replay, compliance, and analytics.
- **Notification Service:** Aggregates and throttles alerts, sends mobile push (FCM/APNs), and persistent notifications.
- **Management Service / UI:** Org and device management, token provisioning/rotation, device revocation, audit views and admin APIs.
- **Cache (Redis):** Short-lifetime caches for lookups, rate-limiting counters, and locks (optional).
- **Observability:** Metrics (Prometheus), logs (structured JSON to centralized store), distributed tracing (OpenTelemetry), health checks and alerting.

- **Auth DB (Postgres):** Stores user identities, hashed credentials, API key metadata, refresh-token hashes and audit records for authentication/authorization operations.

Identity storage & options

There are two common approaches for identity storage and authentication:

- **Managed Identity Provider (recommended for quick secure setup):** Use a managed service such as Auth0, Azure AD B2C, or AWS Cognito to handle user/password storage, MFA, password resets, and compliance features. The `Auth` service delegates authentication to the provider and stores only minimal mapping state (e.g., external id → org_id).

- **Self-hosted Auth service + Auth DB (Postgres):** Keep full control by running your own `Auth` service with a dedicated `AuthDB`. Store only hashed passwords and hashed API keys; never store plaintext secrets. Recommended schema sketches:

  - `users` (id, org_id, email, password_hash, password_algo, is_active, created_at, last_login)
  - `api_keys` (id, owner_id, key_hash, scopes, created_at, revoked_at)
  - `refresh_tokens` (id, user_id, token_hash, issued_at, revoked_at)
  - `audit_log` (id, actor_id, action, target, timestamp, details)

Security best-practices (self-hosted)

- Use Argon2id (or bcrypt if unavailable) with a unique salt per password; consider a global pepper in `Secrets` for defense-in-depth.
- Hash API keys on issuance and only show the raw value once; compare using constant-time checks.
- Store refresh tokens hashed and support immediate revocation paths; propagate revocation events via the `Queue` so `Worker`/`Auth` caches can invalidate.
- Limit login attempts and enforce rate-limiting at the `API Gateway`.
- Keep encryption keys and secrets in a centralized `Secrets` manager (Vault/KeyVault) and instrument audit logs for credential operations.

Multi-tenant mapping

Keep canonical organization metadata in `Device & Organization DB`. Store `org_id` in `AuthDB` as an FK (or keep a stable mapping field) so `Auth` can resolve user→org and apply org-scoped policies. Synchronize between DBs via transactional updates or via events published to `Queue` for eventual consistency.

Management & device lifecycle dataflow

1. Organization creation: an admin uses the `Management UI` (or Management API) to create an organization; `Management` writes org record to `Device & Organization DB` and issues initial admin credentials.
2. Device registration: an admin or end-user registers a device via `Management` (web or mobile). `Management` creates a device record with owner/org mapping and issues a provisioning token (or API key) stored encrypted in `Secrets` and the DB.
3. Device provisioning: the device (or router acting on behalf of devices) uses the provisioning token / API key to authenticate with the `API Gateway` when sending logs. The `Auth` service validates keys and looks up device→org mapping in `Device & Organization DB`.
4. Ingestion & enrichment: `Ingest` receives validated logs and publishes normalized messages to `RabbitMQ`. `Worker` consumers enrich messages using `Device & Organization DB` (owner, expected MACs/IPs) and apply detection rules.
5. Unknown device detection: if a log indicates a device (MAC/IP) not present in the registry, `Worker` marks an incident, writes event to `EventsStore`, updates `DeviceDB` (optionally create a pending device entry), and publishes a notification event.
6. Notification delivery: `Notifier` aggregates alerts, respects throttling/aggregation rules, and pushes to `PushGateway` (FCM/APNs). The mobile app can also query `Management` for audit and incident details.
7. Revocation & rotation: admins can revoke provisioning tokens or delete devices via `Management`; `Auth` enforces revocation immediately, and `Management` may publish revocation events to `Queue` for downstream processing.

Dataflow summary

Routers push logs → `API Gateway` (auth & validation) → `Ingest` publishes to `RabbitMQ` → `Workers` consume, enrich (via `Device & Organization DB`), apply rules → write to DB/archive and emit notification events → `Notification Service` delivers to mobile push gateway. Organization and device management operations flow through `Management` → `Device & Organization DB`, which is consulted by both `Auth` and `Worker` components.
* Auth & API-Key Service: Issue/manage API keys and tokens, support OAuth2 for users and API keys for routers, enforce scopes and rate-limits.
* Logs Ingestion Service: Stateless HTTP/HTTPS front-end that validates and normalizes router logs and publishes messages to the event bus; keep minimal CPU work.
* Event Bus (RabbitMQ): Durable queueing for decoupling ingestion from processing; use persistence and dead-letter queues.
* Processing Workers: Consumer pool that enriches, deduplicates, classifies logs, applies detection rules (e.g., unknown device), writes events to archive and device DB, and emits notification events.
* Device Registry (Postgres): Canonical store for devices, owners, and organization mappings; strong constraints and indexes for lookups.
* Event Archive (S3/Blob): Append-only storage for raw and processed events for replay, compliance, and analytics.
* Notification Service: Aggregates and throttles alerts, sends mobile push (FCM/APNs), and persistent notifications.
* Management Service / UI: Org and device management, token management, audit views.
* Cache (Redis): Short-lifetime caches for lookups, rate-limiting counters, and locks (optional).
* Observability: Metrics (Prometheus), logs (structured JSON to centralized store), distributed tracing (OpenTelemetry), health checks and alerting.