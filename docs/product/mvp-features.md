# MVP Features

This document describes the product from a user's point of view: what you can
do with it, and why it's useful. For how it's built, see
[`../architecture/`](../architecture).

## The problem

Most people have no idea what's actually connected to their home Wi-Fi. A
neighbor guessing a weak password, a smart-TV or IoT gadget nobody remembers
adding, or a guest's phone that never left the network — normally you'd never
know unless you dig through your router's admin page yourself, which almost
nobody does.

## Who it's for

Anyone who wants a simple answer to "is my home network still just mine?" —
without needing to be a networking expert or spend time poking around their
router's admin page themselves. You give the system your router's login
once, and it does the poking around for you from then on.

## What's included in the MVP

### 1. Create an account and an organization

Sign up and create an **organization** — this represents your home (or, if
you manage security for a few different households/locations, you can have
more than one). Everything the system tracks — devices, notifications — is
scoped to your organization, and only people you invite can see it.

### 2. Add your router

In the admin panel, add your router: its address on your home network, and
the same admin login (username and password) you'd already use if you opened
its settings page yourself. You enter it once; the system stores it securely
and never shows it back to you again. You don't need to change anything on
the router itself — this just tells the system how to look at it on your
behalf.

### 3. Install the app

Install the companion app on a device that's already on your home network and
stays connected reasonably often — a phone, a laptop, or a PC. You pair it
with your organization once during setup and it's automatically linked to
the router you added above. You never type the router's login into the app
itself — it fetches what it needs securely once it's paired.

### 4. The app keeps an eye on your router for you

Once installed, the app periodically logs into your router using the
credentials you provided, checks which devices are currently connected, and
sends that information to the system securely. This happens automatically in
the background — no ongoing effort from you after the initial setup. If you
ever change your router's password, just update it in the admin panel and
the app picks up the change on its own.

### 5. Get notified when a new device shows up

If the system sees a device on your network it hasn't seen before, you get a
**push notification** on your phone immediately: something new just joined
your Wi-Fi. From there, you can mark it as "this is mine" (a device you
recognize — your new smart speaker, a visiting family member's laptop) so you
won't be asked about it again. If you don't recognize it, that's your signal
to go investigate — e.g. change your Wi-Fi password.

## How it fits together

```
You create an account & organization
        │
        ▼
You add your router (address + login) in the admin panel
        │
        ▼
You install the app on a device at home and pair it
        │
        ▼
The app fetches the router login securely and checks the router periodically, in the background
        │
        ▼
The system compares what it sees against devices it already knows
        │
        ▼
New device? → You get a push notification → You confirm or investigate
```

## What's deliberately *not* in the MVP

To keep the first version small and shippable, the following are intentionally
left out for now — they're natural next steps, not oversights:

- Automatically blocking or disconnecting an unrecognized device (the system
  observes and alerts; it doesn't yet act on your router).
- Tracking more than "is this device known or not" — no history dashboards,
  usage graphs, or bandwidth monitoring yet.
- Support for multiple people managing the same organization with different
  permission levels (that's coming — see
  [`../architecture/04-roles-and-permissions.md`](../architecture/04-roles-and-permissions.md)
  for the design — just not required for the first usable version).
- A security/audit trail of who did what in your organization (added a
  router, changed a role, fetched a credential), and handling data-privacy
  requests (exporting or deleting your data) in a GDPR-aligned way — the
  design exists (see
  [`../architecture/16-auditing-and-compliance.md`](../architecture/16-auditing-and-compliance.md)),
  but it isn't built, and isn't needed for a single-owner household to get
  value from the MVP.
- Notification preferences (quiet hours, digesting multiple alerts into one,
  etc.) or alternate channels like email/SMS.
- Any router vendor–specific integration — the app works by observing what a
  router already exposes, not by requiring a specific brand.

## Why this scope

Each MVP step above exercises a real, necessary part of the system end to
end — account/tenant creation, a working client app, real data flowing into
the backend, and a real notification reaching a user — without requiring
anything speculative to be built first. It's the smallest slice that is
actually useful to a real person on day one.
