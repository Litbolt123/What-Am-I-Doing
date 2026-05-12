# Updating the app

What Am I Doing keeps your history, rules, and settings in a **single SQLite file** under `%LocalAppData%\WhatAmIDoing\` (usually `activity.sqlite3`). Updating the installed app **does not** delete that file when you run a newer installer over the old one.

## Recommended steps

1. **Optional backup:** In the app, open **Settings** and use **Export database backup…** so you have a copy of your data (rules, PIN hash, samples, optional screenshots index).
2. **Download the new release:** From [GitHub Releases](https://github.com/Litbolt123/What-Am-I-Doing/releases), get the latest **`WhatAmIDoing-Setup-….exe`** (or your maintainer’s published installer).
3. **Run the installer:** Close the running app from the tray (or let the installer prompt you). Install over the existing copy in `%LocalAppData%\Programs\WhatAmIDoing`.
4. **Start the app again:** Your database should be picked up automatically from the same data folder.

## After an update

- **Version line:** The dashboard’s “This install” block shows the app version and last session start time.
- **Lifecycle log (optional):** If **Settings → Family controls → Log when the app starts…** is on, an **upgrade** row is written when the app detects a new version string compared to the last run.

## In-app “Check for updates”

**About** (from **Settings**) includes **Check for updates…**. It compares your installed version to the **latest GitHub release tag** and can open the **installer download** or the Releases page in your browser. There is **no silent in-app upgrade**; you still run the setup yourself (typical for small desktop apps and avoids elevation surprises).

**Settings → Appearance & accessibility** includes **Updates (GitHub)**: you can turn on an **automatic check** each time the app starts (one public `GET` to GitHub’s API per session) and an optional **tray notification** when a newer release exists. Clicking the balloon opens the download in your browser — **quit the app** before running the installer.

**About → Download and run installer…** (shown after **Check for updates** finds a newer release **with** a `WhatAmIDoing-Setup-….exe` attached on GitHub) saves the setup under your user **Temp** folder, **closes this app**, and starts the Inno wizard. You still confirm in the installer and may see **UAC** / **SmartScreen** — it is not a fully silent upgrade.

If the repo URL or owner name changes, maintainers should update `UpdateCheckService.GitHubRepo` in code to match the new GitHub location.

- Restore from **Import database backup…** (replaces the live database and restarts the app), or copy your backup `.sqlite3` over `activity.sqlite3` while the app is **not** running.
- If the repo URL or owner name changes, maintainers should update `UpdateCheckService.GitHubRepo` in code to match the new GitHub location.
