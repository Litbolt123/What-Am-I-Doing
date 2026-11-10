# What Am I Doing 1.2.6 — Windows installer

## What's new

- 7, 14, and 28-day reports load much faster after the first open (finished days are cached).
- Resizing the window is smoother.
- The day chart scrolls with the rest of the page — no getting stuck, no cut-off rows, and no extra thin bar under the last day.
- Built-in category rules cover many more apps, including WSL, Windhawk, and WhatsApp on the web.
- When adding a rule, you can pick from recent apps, titles, and pages — not only a live capture.
- Right-click a category to rename it or view details, or right-click an app to add a rule.
- The Temp folder no longer fills with leftover copies of the database or installer.

## Notes

- Quit the app from the tray before running the installer.
- Updating over 1.2.5 keeps your activity database and settings.
- A small sidecar file, day_cache.sqlite3, may appear next to the database under %LocalAppData%\WhatAmIDoing\ (~1 MB). It is only a speed cache and can be deleted; it will rebuild.
