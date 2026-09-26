# HomeSecurity

HomeSecurity watches your home Wi-Fi so you always know what's connected to it.
A companion app periodically checks your router for connected devices and sends you a push notification when something new shows up.

## Functionalities

- Create an account and an organization that represents your home, with all data scoped to it.
- Add your router's address and admin login once in the admin panel; the credentials are stored securely and never shown back.
- Install and pair the companion app on a device that's regularly on your home network.
- The app logs into your router in the background and securely reports which devices are connected.
- Get a push notification when an unknown device joins your network, and mark it as "this is mine" or investigate.

## Security & compliance

Multi-tenancy, RBAC, and encryption at rest are already part of the
architecture (see [`docs/architecture/`](docs/architecture)). A dedicated
security/audit event trail and a GDPR/ISO-27001-aligned compliance model are
designed in
[`docs/architecture/16-auditing-and-compliance.md`](docs/architecture/16-auditing-and-compliance.md)
— not built yet, but planned as part of the same design, not an
afterthought.
