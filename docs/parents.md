# For parents — how the report works

This is a one-page guide to the report your kid will email or hand you. It explains what the numbers mean, what they don’t mean, and what controls you have.

## What the app records

Every few seconds, on the kid’s PC, the app writes a small row to a local database with:

- the **process name** (e.g. `chrome`, `Cursor`, `Minecraft.Windows`),
- the **window title** (e.g. *“GitHub – chrome”*, *“Khan Academy – Algebra 1 – chrome”*),
- whether the user has touched the **mouse or keyboard** within the idle threshold (defaults to **1 minute**, configurable),
- whether the **workstation was locked**,
- whether other apps are **producing audio** (so a Discord call alongside Minecraft is attributed to **both**),
- a derived **context** value when possible: site host (`khanacademy.org`), YouTube channel/video, IDE project folder.

If, and only if, **screenshots are turned on** in Settings, it also captures a downscaled JPEG every few minutes, encrypts it on disk, runs on-device OCR, and stores the recognized text. Screenshots and their text never leave the PC unless an HTML report is exported with “Include thumbnails” checked.

## What the dashboard / HTML report shows

- **Summary** — total active time, idle time, locked time, and a “voice/mic activity” line.
- **Hourly timeline** (single day) or **7-day heatmap** (week view), colored by category.
- **By category** — totals plus a list of which rule matched. Double-click in the desktop dashboard to drill into evidence (process + window title + first time seen).
- **By app** — the same totals broken down by process name, with active vs. idle.
- **Highlights** — top YouTube channels, top sites, top IDE projects.
- **On-screen signals** *(only present when the report was generated with screenshots enabled)* — the most-frequent meaningful keywords from OCR text and, optionally, thumbnail images.

## What the numbers mean (and don’t)

- “**Active**” = a window had focus AND there was input within the idle threshold.
- “**Idle**” = a window had focus, but no input for the threshold (default **1 minute**).
- “**Locked**” = the Windows lock screen is up; the kid is away.
- A process can earn **active time without focus** if Settings ▸ Audio detection is on and that process was producing sound (e.g., Discord during a game).
- The classification “category” is just a label your kid (or the suggested defaults) attached to a process / title / context. It is not a moral judgment.

## What you, as a parent, can lock down

- **Settings ▸ Require a PIN** — turning this on forces a PIN to open the dashboard, change settings/rules, or exit the app. The kid still sees what the dashboard shows them, but cannot rewrite the rules to relabel “Roblox” as “Studying.”
- **Settings ▸ Start with Windows** — keeps the app running so reports stay continuous.
- **Settings ▸ Screen captures** — your call. Off by default. When on you also choose retention (default **7 days**) and a list of processes to **never** screenshot (banking, password managers, etc.).

## Data location & deletion

Everything lives under `%LocalAppData%\WhatAmIDoing\` on the kid’s PC:

- `activity.sqlite3` — the database.
- `screens\` — DPAPI-encrypted JPEGs, only if screenshots are enabled.
- `logs\` — daily crash log.

Deleting that folder fully resets the app (and erases the activity history, including any forgotten PIN).

## Limits to be honest about

- It only sees the **foreground window title** unless screenshots are enabled. A title like *“New tab – chrome”* is genuinely ambiguous.
- A web page / app that lies in its title (or has no title) lands in the “other / Chrome” bucket. Adding a rule fixes it going forward, not retroactively.
- It does not currently read browser tab URLs directly — it relies on the title bar — so a stealthy browser profile or a private window can hide a site from category labels (it will still show up as time on `chrome.exe`).
- Screenshots are downscaled (max 1280px wide, JPEG quality 70) so OCR is approximate. It is good for *“did this homework site appear at all today?”* and bad for fine print.
