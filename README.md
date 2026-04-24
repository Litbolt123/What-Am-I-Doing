# What Am I Doing

A Windows desktop app that records **what window is in front of you**, whether you are **idle** (no keyboard/mouse past a threshold), and turns that into **daily / 7‑day summaries** you can **export as HTML** for parents or mentors. Everything stays **on your PC** in v1 (`%LocalAppData%\WhatAmIDoing`).

It also enriches each foreground sample with:

- **Browser context** — site host (e.g. `khanacademy.org`) and YouTube video / channel.
- **IDE / editor project** — folder name from the window title (Cursor, VS Code, Visual Studio, JetBrains, Sublime).
- **Companion audio** — when a process is actively producing sound through Windows Core Audio (Discord call, music app, game audio).
- **Optional, opt‑in screenshots** with on‑device OCR — JPEGs are encrypted on disk with **DPAPI (CurrentUser)** so only your Windows account can read them, and a retention window auto‑purges old captures.

## Requirements

- Windows 10 (1809+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (only required to build/run from source)

## Run from source

```powershell
dotnet run --project src/WhatAmIDoing/WhatAmIDoing.csproj
```

The dashboard opens on launch. Closing the window sends the app to the **system tray** (notification area). **Left‑click** the tray icon (or **Open dashboard** in the right‑click menu) to bring it back. **Settings** changes idle time, sampling interval, audio detection, screenshots, auto‑start, and the family PIN. **Rules** sets categories. **Export HTML** saves a shareable report.

If the app silently re-launches into the tray instead of opening a window, that means a previous instance is already running — single-instance is enforced through a named mutex.

## Defaults

- **Idle / AFK:** no input for **2 minutes** by default (`idle_threshold_ms`). Change in **Settings…** (≈15 seconds – 2 hours).
- **Sample interval:** **5 seconds** by default (`sample_interval_ms`). Change in **Settings…** (1–120 seconds).
- **Audio detection:** on by default. Adds *companion audio* time so a Discord call alongside Minecraft is attributed to both.
- **Screen captures:** **off by default**. When enabled the default is **1 capture every 5 minutes**, **30‑day retention**, encrypted on disk.

## Automatic categories (editable)

On first launch, the app seeds a suggested rule set: common browsers, dev tools (Cursor, VS, JetBrains), YouTube / Netflix / Twitch matched in the window title, communication apps, game launchers, Office, File Explorer, plus a few `Context value contains` rules for educational YouTube channels.

In **Rules…**:

- **Add** your own rule (process equals / process contains / window title contains / context value contains).
- **Restore suggested defaults…** replaces all rules with the built-in pack (after a confirmation).
- When you add a rule with the same match type and pattern as a suggested row, only that preset row is replaced — the rest of the suggested list stays.

Source list: `src/WhatAmIDoing/Data/BuiltInDefaultRules.cs`.

## Family controls

- **Start with Windows** — toggle in Settings. It writes a per-user `HKCU\…\Run` entry and launches the app with `--minimized` so it boots straight to the tray.
- **PIN protection** — Settings ▸ *Require a PIN…*. The PIN is stored as a **PBKDF2-SHA256** hash (200,000 iterations, random salt). With a PIN set, opening the dashboard, changing settings/rules, or exiting from the tray all prompt for the PIN once per session.

If you forget the PIN, deleting `%LocalAppData%\WhatAmIDoing\activity.sqlite3` resets the app (you also lose your history — that’s the trade-off). Parent docs are in [`docs/parents.md`](docs/parents.md).

## Build a release / installer

```powershell
# 1. Build a single self-contained EXE in src\WhatAmIDoing\bin\Publish\win-x64\
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1

# 2. Open installer\WhatAmIDoing.iss in Inno Setup Compiler (jrsoftware.org/isinfo.php)
#    and click Build. Output appears in installer\Output\.
```

The Inno Setup script installs **per-user** (no admin prompt), to `%LocalAppData%\Programs\WhatAmIDoing`, and offers two checkboxes during install: **Create a desktop shortcut** and **Start when I sign in to Windows**.

## Where data lives

```
%LocalAppData%\WhatAmIDoing\
    activity.sqlite3        ← samples, rules, settings, screen-event index
    screens\                ← DPAPI-encrypted JPEGs (only if screenshots are enabled)
    logs\                   ← rolling crash log per UTC day
```

Crashes and unobserved task exceptions are appended to `logs\YYYY-MM-DD.log` so you can see what went wrong even though the app runs in the tray with no console.

## Roadmap

1. **Cloud-shareable reports** — opt-in upload of an HTML report to a parent dashboard.
2. **Screenshot-based understanding** — deeper page/scene awareness; current limits documented in `docs/screenshot-module.md`.
3. **Per-rule weekly limits** — soft alerts when, e.g., “games” passes a budget for the week.

## Repository layout

- `src/WhatAmIDoing/` — WPF UI, SQLite storage, sampling/audio/screen services, HTML export.
- `installer/` — Inno Setup script (`WhatAmIDoing.iss`).
- `scripts/publish.ps1` — produces the single-file release binary.
- `docs/` — context summary, parent-facing doc, screenshot-module design.
