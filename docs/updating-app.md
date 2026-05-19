# Updating the app

What Am I Doing keeps your history, rules, and settings in a **single SQLite file** under `%LocalAppData%\WhatAmIDoing\` (usually `activity.sqlite3`). Updating the installed app **does not** delete that file when you run a newer installer over the old one.

## Recommended steps

1. **Optional backup:** In the app, open **Settings** and use **Export database backup…** so you have a copy of your data (rules, PIN hash, samples, optional screenshots index).
2. **Download the new release:** From [GitHub Releases](https://github.com/Litbolt123/What-Am-I-Doing/releases), get the latest **`WhatAmIDoing-Setup-….exe`** (or your maintainer’s published installer).
3. **Run the installer:** Close the running app from the tray (or let the installer prompt you). Install over the existing copy in `%LocalAppData%\Programs\WhatAmIDoing`. If you already accepted the current **terms version**, the wizard skips the notice and license pages on update (they appear again only when terms change).
4. **Start the app again:** Your database should be picked up automatically from the same data folder.

## After an update

- **Version line:** The dashboard’s “This install” block shows the app version and last session start time.
- **Lifecycle log (optional):** If **Settings → Family controls → Log when the app starts…** is on, an **upgrade** row is written when the app detects a new version string compared to the last run.

## In-app “Check for updates”

**Settings → Updates (GitHub)** includes **Check for updates…** and **Download and run installer…** (when GitHub has a newer tag with `WhatAmIDoing-Setup-….exe` attached). The check compares your installed version to the **highest published release tag** returned by GitHub (drafts ignored; pre-releases count). It does **not** rely only on GitHub’s single **“Latest”** release pointer — so you still see a newer build if two installers were published close together and the wrong one was marked latest, or if the newer tag is a pre-release.

**About** (from **Settings**) only shows the version and data path; use the **Updates** section above for GitHub checks.

There is **no silent in-app upgrade**; you still run the setup yourself (typical for small desktop apps and avoids elevation surprises).

**Settings → Updates (GitHub)** also lets you turn on an **automatic check** each time the app starts and an optional **tray notification** when a newer release exists. You get **one tray balloon per app session** while you are still on an older build (a new session after reboot will remind you again until you install). The generic “app is running” tray hint is shown at most once. Clicking the update balloon opens the download in your browser — **quit the app** before running the installer.

**Settings → Family & app → Start in system tray** keeps the dashboard closed on launch (tray only). **Start with Windows** already launches tray-only via `--minimized`.

When a newer release exists, the dashboard **Catch up** card can show **Update available** (after the walkthrough / what’s new, if any). **Later** hides that card for that release version; **Update available** on the idle Catch up row brings it back.

**Download and run installer…** (after a check finds a newer release **with** a `WhatAmIDoing-Setup-….exe` attached on GitHub) saves the setup under your user **Temp** folder, **closes this app**, and starts the Inno wizard. You still confirm in the installer and may see **UAC** / **SmartScreen** — it is not a fully silent upgrade.

If the repo URL or owner name changes, maintainers should update `UpdateCheckService.GitHubRepo` in code to match the new GitHub location.

- Restore from **Import database backup…** (replaces the live database and restarts the app), or copy your backup `.sqlite3` over `activity.sqlite3` while the app is **not** running.
