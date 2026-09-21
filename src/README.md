# Local dev backing services

Docker Compose stack for the infrastructure services the target architecture
(`docs/architecture/`) depends on. There's no application code yet (this repo
is design-first — see the root `CLAUDE.md`), so this only provisions backing
services, not the app itself.

## First-time setup

Nothing in this stack's credentials is committed to git. Generate them
locally before first use:

```bash
cd src
pwsh ./init.ps1     # writes .env and config/keycloak/realm-export.json
docker compose up -d
docker compose ps   # wait for everything to report healthy
docker compose down # stop (data persists in named volumes)
```

`init.ps1` skips silently if `src/.env` already exists. Pass `-Force` to
regenerate everything — but if the stack has already booted once, run
`docker compose down -v` **first**: Keycloak persists the imported realm
(including the old client secret) in its own Postgres database, so
`--import-realm` won't overwrite an existing realm on its own, and a
regenerated secret would stop matching what's actually stored.

See `.env.example` for the shape of the generated `.env` (usernames/DB names
are fixed; passwords and secrets are random per environment). To look up a
generated credential (e.g. the Keycloak admin password), read it straight
out of `src/.env` after running the script.

## Services

| Service | URL / port | Purpose |
|---|---|---|
| postgres-keycloak | `localhost:5432` | Keycloak's own database |
| postgres-app | `localhost:5433` | App database (tenants/devices/logs) |
| keycloak | http://localhost:8080 | Identity provider (OIDC) |
| oauth2-proxy | http://localhost:4180 | Identity-aware proxy in front of the Admin UI |
| rabbitmq | http://localhost:15672 (UI), `5672` (AMQP) | Message broker |
| redis | `localhost:6379` | Permission cache / idempotency store |
| adminer | http://localhost:8081 | Postgres browser — connect using either postgres service's host/credentials from `.env` |

## Keycloak realm

`config/keycloak/realm-export.json.template` is committed; `init.ps1` renders
it into `config/keycloak/realm-export.json` (gitignored, real secret filled
in), which is auto-imported on first boot (`--import-realm`) and creates the
`home-security` realm with:
- an `oauth2-proxy` confidential client (secret matches `OAUTH2_PROXY_CLIENT_SECRET` in `.env`)
- a `home-security-api` bearer-only client, ready for the future API

No users are pre-created — add one from the Keycloak admin console
(`localhost:8080` → `home-security` realm → Users) to test a login through
oauth2-proxy.

## oauth2-proxy is a placeholder

There's no Admin UI service to protect yet, so oauth2-proxy is wired up
end-to-end against Keycloak but points `--upstream` at `static://200` (a
built-in stub that just returns 200 after login succeeds). Once the Admin UI
exists, swap `--upstream` for its address.

## Not included

TLS/ingress and push notifications (FCM) are real-environment/cloud concerns
per ADR-0008 and `docs/architecture/06-notifications.md` — not modeled here.
Realm SSL is set to `none` for plain-HTTP local dev convenience; this compose
stack does not attempt local HTTPS (see ADR-0008 for the `mkcert` approach if
that's ever needed for this stack too).
