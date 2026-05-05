# What Am I Doing

A Windows desktop app that records **what window is in front of you**, whether you are **idle** (no keyboard/mouse past a threshold), and turns that into **daily / 7‑day summaries** you can **export as HTML** for parents or mentors. Everything stays **on your PC** in v1 (`%LocalAppData%\WhatAmIDoing`).

It also enriches each foreground sample with:

- **Browser context** — site host (e.g. `khanacademy.org`) and YouTube video / channel.
- **IDE / editor project** — folder name from the window title (Cursor, VS Code, Visual Studio, JetBrains, Sublime).
- **Companion audio** — when a process is actively producing sound through Windows Core Audio (Discord call, music app, game audio).
- **Optional, opt‑in screenshots** with on‑device OCR — JPEGs are encrypted on disk with **DPAPI (CurrentUser)** so only your Windows account can read them, and a retention window auto‑purges old captures.

## Install (Windows — for anyone)

You **do not** need Git or the .NET SDK to run the app.

1. On GitHub, open this project’s [**Releases**](./releases) page (from the repo home page: **Releases** in the sidebar, or **Code** and look for **Releases** on the right).
2. Under the latest release, download **`WhatAmIDoing-Setup-….exe`**.
3. Run the file and follow the wizard: **notice** (liability / data), **license** (terms), then **Express** or **Advanced** install. **Advanced** can optionally turn on stricter idle defaults and/or enable screen captures for the first run (you can change everything later in Settings). If this PC does not already have the **Microsoft .NET 8 Desktop Runtime** (x64), the wizard asks whether to install it; if you choose **Yes**, Microsoft’s own installer runs (with any **UAC** prompt), not a hidden background step.

It installs **for your user only** (no administrator prompt) into `%LocalAppData%\Programs\WhatAmIDoing`.

After install, start **What Am I Doing** from the Start menu; it can also start in the tray. Your activity data is stored separately under `%LocalAppData%\WhatAmIDoing\`.

**First run looks like “nothing happened”?** The app is **tray-first**: the main window may be behind other apps, and on Windows 11 the tray icon is often under the **^** “Show hidden icons” chevron. Click **^**, look for **What Am I Doing**, then **left-click** the icon to open the dashboard. If **`%LocalAppData%\WhatAmIDoing`** is **missing or empty** (no `logs` folder, no `activity.sqlite3`), Windows probably never started the .NET part of the app: install the **.NET 8 Desktop Runtime (x64)** from Microsoft — not the smaller “.NET Runtime” only package and not the ASP.NET runtime. The correct download is labeled **Desktop** on [https://aka.ms/dotnet/download](https://aka.ms/dotnet/download). If the folder exists, open `logs\` and check today’s `app-*.log`. **Do not rely on “Run as administrator”** for the shortcut — the app is installed per-user under your profile; elevation is not required and can confuse which profile is active.

> **Maintainers:** bump `<Version>` in **`Directory.Build.props`**, then **`git push origin vX.Y.Z`** so it matches `Version` (e.g. `v1.0.1` when Version is `1.0.1`). CI fails if they disagree. Until you push that tag, there is **no** Releases download — use **Actions → Windows installer** (manual run) and grab the **artifact**, or follow **Publishing…** under [GitHub Releases](#github-releases-for-people-who-only-download) below.

## Requirements

- Windows 10 (1809+) or Windows 11 (x64). The **Releases** setup bundles the **.NET 8 Desktop Runtime** and installs it when missing — you do **not** need to install .NET yourself first.
- [.NET 8 SDK](https://dotnet.microsoft.com/download) — **only if** you build or run from source

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
- **PIN protection** — Settings ▸ *Require a PIN…*. The PIN is stored as a **PBKDF2-SHA256** hash (200,000 iterations, random salt). With a PIN set, **Settings** and **Rules** ask once per session after a correct entry; **Exit** from the tray asks every time. The dashboard and HTML export do not require the PIN.

If you forget the PIN, deleting `%LocalAppData%\WhatAmIDoing\activity.sqlite3` resets the app (you also lose your history — that’s the trade-off). Parent docs are in [`docs/parents.md`](docs/parents.md).

## Build a release / installer

**Version (single source):** edit **`Directory.Build.props`** at the repo root (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`).  
`dotnet publish`, the built EXE, Inno’s `AppVersion`, and `scripts\get-version.ps1` all use that via MSBuild — **do not** duplicate version numbers in the `.csproj` or Inno script for releases.

The **Releases** installer ships a **framework-dependent** single-file app plus a copy of Microsoft’s **.NET 8 Desktop Runtime (x64)** offline installer. End users get a normal wizard (terms, liability notice, Express vs Advanced). On the **Ready to Install** page, if Desktop **8.x** is missing, setup asks for permission; **Yes** launches Microsoft’s installer visibly (you can approve **UAC**). **No** still copies the app; you can install .NET yourself later from Microsoft. Silent command-line installs (`/VERYSILENT`) still use a quiet runtime install for automation. **Developers** use the **.NET 8 SDK** and **Inno Setup 6** on the machine that *builds* the setup.

For a **portable / dev** self-contained EXE without Inno, use **`scripts\publish.ps1`** — that build does not depend on the shared Desktop Runtime.

### Optional: install build tools automatically (developers)

If you are missing the **.NET 8 SDK** or **Inno Setup 6**:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-build-prerequisites.ps1
```

That tries **winget** first, then **Chocolatey**, and refreshes `PATH` in the session. You can also run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -InstallPrerequisites
```

…to run that script automatically before publish + Inno.

### One step (recommended)

From the repo root — downloads the Desktop Runtime bundle into **`installer\prereq\`** (via **winget** unless the file is already there), publishes a **framework-dependent** single-file EXE (`publish-installer.ps1`), then runs Inno’s `ISCC.exe` with **`/DAppVersion=`** from MSBuild (`get-version.ps1`):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Use **`-SkipFetch`** if you already placed **`installer\prereq\DesktopRuntime-8-x64.exe`** by hand. Use **`-SkipPublish`** to reuse an existing **`src\WhatAmIDoing\bin\Publish\win-x64\WhatAmIDoing.exe`** from a prior `publish-installer.ps1` run.

If `ISCC.exe` is still not found, set **`INNO_SETUP_ROOT`** to the folder that contains it (for example `C:\Program Files (x86)\Inno Setup 6`).

The generated setup EXE is under **`installer\Output\`** (for example `WhatAmIDoing-Setup-1.0.2.2.exe`). That folder is gitignored.

### GitHub Releases (for people who only download)

**Maintainer walkthrough (tags, naming, troubleshooting):** [`docs/releasing.md`](docs/releasing.md).

The workflow [`.github/workflows/release-windows.yml`](.github/workflows/release-windows.yml) runs on **push of a tag** `v*` (example: `v1.0.1`) and on **manual** **Actions → Windows installer → Run workflow**.

- **Tag push:** reads **`<Version>`** from `Directory.Build.props` via MSBuild, **fails if the tag does not match** (e.g. tag `v1.0.1` requires Version `1.0.1`), then builds, uploads an artifact, and creates a **GitHub Release** with the setup EXE attached.
- **Manual run:** same build and artifact; **no** Release — download the **artifact** from the workflow run instead.

Bump **`Directory.Build.props`**, commit, then tag with the same numeric version (`v` + `Version`).

#### Why the Releases page looks empty

**Pushing commits to `main` does not create a release.** Nothing appears under [**Releases**](./releases) until either:

1. You **push a version tag** (recommended — see below), which triggers CI and **Publish GitHub Release**, or  
2. You **manually** create a release in the GitHub UI (**Code → Releases → Create a new release**) and attach files yourself.

Until then, testers can use **Actions → Windows installer → Run workflow** and download the **installer artifact** from the completed run (not under Releases).

#### Publish the Windows installer as a GitHub Release (first time)

1. Put the version you want in **`Directory.Build.props`** (`<Version>` must match the tag, e.g. `1.0.0` → tag **`v1.0.0`**). Commit and push to GitHub.  
2. Create and push the tag on that commit:

```powershell
git checkout main   # or your branch that has the commit
git pull
git tag v1.0.0      # must match Version: 1.0.0 → v1.0.0
git push origin v1.0.0
```

3. Open **Actions** and wait for **Windows installer** on the tag to finish successfully.  
4. Open [**Releases**](./releases) — you should see **`v1.0.0`** with **`WhatAmIDoing-Setup-….exe`** attached.

Alternatively: **GitHub → Releases → Draft a new release → Choose tag → Create new tag** `v1.0.0` on your latest commit, then publish — but you still need CI to build the installer unless you upload the EXE by hand. Tag push + CI is the intended path.

### Manual steps

```powershell
# 0. Bundled .NET Desktop Runtime (stable filename for Inno)
powershell -ExecutionPolicy Bypass -File .\scripts\fetch-installer-prerequisites.ps1

# 1a. Framework-dependent single-file EXE (for the family installer)
powershell -ExecutionPolicy Bypass -File .\scripts\publish-installer.ps1

# 1b. Optional: self-contained EXE (no shared runtime) — not used by Inno in the default flow
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1

# 2. Inno — AppVersion must match Directory.Build.props (same as build-installer.ps1)
$v = powershell -NoProfile -File .\scripts\get-version.ps1
Push-Location .\installer
try {
  & "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=$v .\WhatAmIDoing.iss
}
finally {
  Pop-Location
}
```

The Inno script installs **per-user** (no admin prompt), to **`%LocalAppData%\Programs\WhatAmIDoing`** (the **program** folder), and offers: **Create a desktop shortcut** and **Start when I sign in to Windows**. Your **database and logs** live in a **different** folder: **`%LocalAppData%\WhatAmIDoing\`** (see below — not removed by a normal uninstall of the program folder).

## Where data lives

**Two different “WhatAmIDoing” folders under your profile — do not mix them up:**

| Folder | Purpose | Typical contents right after install |
|--------|---------|--------------------------------------|
| **`%LocalAppData%\Programs\WhatAmIDoing\`** | **Program files** from the installer (`{app}` in Inno) | **`WhatAmIDoing.exe`** (single-file app) + Inno’s **uninstaller** (`unins000.exe` and sometimes a small companion file). **No** `activity.sqlite3` here — that is intentional. |
| **`%LocalAppData%\WhatAmIDoing\`** | **Your data** (samples, rules, settings, logs) | Created when the app **runs successfully** the first time: **`activity.sqlite3`**, **`logs\`**, optional **`screens\`**. If startup fails before the database opens, this folder may be empty or missing files you expect. |

```
%LocalAppData%\WhatAmIDoing\
    activity.sqlite3        ← samples, rules, settings, screen-event index
    screens\                ← DPAPI-encrypted JPEGs (only if screenshots are enabled)
    logs\                   ← rolling crash log per UTC day
```

Crashes and unobserved task exceptions are appended to `logs\app-YYYY-MM-DD.log` so you can see what went wrong even though the app runs in the tray with no console.

## Roadmap

1. **Cloud-shareable reports** — opt-in upload of an HTML report to a parent dashboard.
2. **Screenshot-based understanding** — deeper page/scene awareness; current limits documented in `docs/screenshot-module.md`.
3. **Per-rule weekly limits** — soft alerts when, e.g., “games” passes a budget for the week.

## Repository layout

- `Directory.Build.props` — **`<Version>`** (and assembly/file versions) for the app, publish output, and Inno; single source of truth.
- `src/WhatAmIDoing/` — WPF UI, SQLite storage, sampling/audio/screen services, HTML export.
- `installer/` — Inno Setup script (`WhatAmIDoing.iss`); compiled setup lands in `installer/Output/` (gitignored).
- `scripts/publish.ps1` — produces the single-file release binary.
- `scripts/get-version.ps1` — prints MSBuild `Version` (for CI and local Inno `/DAppVersion`).
- `scripts/install-build-prerequisites.ps1` — tries winget/Chocolatey for .NET 8 SDK + Inno Setup (developer machines).
- `scripts/build-installer.ps1` — publish + Inno `ISCC.exe` in one step (`-InstallPrerequisites` optional).
- `.github/workflows/release-windows.yml` — CI: build installer on tag `v*` and attach to Releases; manual runs upload an artifact.
- `docs/` — context summary, parent-facing doc, screenshot-module design, **[how to publish a release](docs/releasing.md)**.
