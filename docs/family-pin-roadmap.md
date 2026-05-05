# Family PIN — future direction (uninstall, recovery)

This note captures **product intent** that is **not fully implemented** in v1. It exists so later work (email recovery, installer hooks) stays aligned with how families use the app.

## Goals

1. **Destructive actions require the parent PIN** when “Require a PIN…” is enabled — not only Settings / Rules / tray Exit, but also anything that effectively **removes monitoring** or **deletes the app / all data** from the child’s normal path.
2. **PIN recovery without wiping history** — parents forget PINs. Recovery should eventually use something **outside the app on this PC**, e.g. a **verified parent email** (or similar) set up ahead of time, so reset is deliberate and auditable.
3. **Tie (1) and (2) together** — turning on strong PIN protection should eventually **require** (or strongly encourage) a recovery channel so families are not stuck choosing between “kid locked in” and “delete `activity.sqlite3` and lose everything.”

## What “delete the app” means in practice

| Action | Who runs it | PIN in v1 | Future idea |
|--------|-------------|-----------|-------------|
| Tray **Exit** | User | Yes, if PIN on | Keep |
| **Settings / Rules** | User | Yes, if PIN on | Keep |
| **Windows Settings → Apps → Uninstall** (Inno `unins000.exe`) | OS / user | **No** (installer does not ask our app) | Optional: custom uninstall step that launches the app or a small helper to confirm PIN / parent email before removing `{app}` |
| **Delete `%LocalAppData%\WhatAmIDoing\` in File Explorer** | User | **No** | Cannot fully prevent; PIN + recovery reduces *need* to do this |
| **In-app “Reset / remove all data / leave family mode”** (if added) | User | Should require PIN + later recovery path | Implement when email (or account) exists |

So: **true “uninstall needs PIN”** likely needs **installer + app cooperation** (or only in-app uninstall entry points we control). Email-backed reset fits **recover PIN** and **authorize destructive in-app actions** first; then extend to uninstall UX if desired.

## Suggested implementation order (later)

1. **Parent contact** — store one or more **verified emails** (sign-in link or code). No cloud dependency in v1 spec until you choose a backend.
2. **PIN reset flow** — request link → verify email → set new PIN (log `upgrade` / `security` row in lifecycle or dedicated audit table).
3. **Gate new destructive UI** — any “clear history”, “remove from this PC”, or “open uninstaller” button: `EnsurePinUnlocked` + eventually “forgot PIN?” → email flow.
4. **Inno uninstall** (optional hard mode) — e.g. run app with `--uninstall-confirm` before removing files; document that power users can still delete folders (no DRM).

## Privacy

Recovery email handling implies a **small amount of PII** and possibly a **server** or **family Microsoft account** — that should be spelled out in the privacy notice before shipping.
