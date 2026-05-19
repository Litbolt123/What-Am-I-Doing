# What Am I Doing 1.2.2 — Windows installer

## What's new

### Updates
- **Catch up** card can show **Update available** when GitHub has a newer release (with **Open download** / **Later**).
- Tray update reminder runs **once per app session** until you install (not suppressed forever after the first check).
- **Settings → Family & app → Start in system tray** — launch without opening the dashboard (Start with Windows still uses tray-only startup).

### Installer
- **Updates** skip the liability notice and license pages if you already accepted the current **terms version** (shown again only when terms change).
- Removed the post-install **restart Windows** recommendation (not required for this self-contained app).

## Notes

- Quit the app from the tray before running the installer.
- First install on a PC that never ran a build with stored terms acceptance will show notice + license **one more time**; later updates skip until terms change.
