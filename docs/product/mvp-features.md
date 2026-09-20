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
without needing to be a networking expert or log into their router's admin
page to find out.

## What's included in the MVP

### 1. Create an account and an organization

Sign up and create an **organization** — this represents your home (or, if
you manage security for a few different households/locations, you can have
more than one). Everything the system tracks — devices, notifications — is
scoped to your organization, and only people you invite can see it.

### 2. Install the app

Install the companion app on a device that's already on your home network and
stays connected reasonably often — a phone, a laptop, or a PC. You pair it
with your organization once during setup. This app is what actually looks at
your router on your behalf; you don't need to change anything on the router
itself or know how to access its admin page.

### 3. The app keeps an eye on your router for you

Once installed, the app periodically checks your router to see which devices
are currently connected, and sends that information to the system securely.
This happens automatically in the background — no ongoing effort from you
after the initial setup.

### 4. Get notified when a new device shows up

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
You install the app on a device at home
        │
        ▼
The app checks your router periodically, in the background
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
