# What Am I Doing 1.1.2 — Windows installer

## What's new

- **Updates & installs:** The app can **check GitHub each time it starts** (Settings → Updates, on by default), show a **tray notification** when a newer release exists, and open the **installer download** or Releases page. In **About**, **Download and run installer…** saves `WhatAmIDoing-Setup-….exe` to your Temp folder, **fully exits** the app (not minimized to tray), starts the setup wizard, and writes a lifecycle line **Stopped (installing app update)** with version details when logging is enabled.
- **Tune detection:** The dialog opens reliably (startup race fix), supports a **custom time window** (your estimate in minutes or hours, ending now) for short sessions, reads the **process** field correctly from the editable combo before Analyze/Apply, and shows Pin/Tune in the taskbar when helpful.
- **YouTube highlights:** More reliable parsing for common browser title shapes (including `… - YouTube - …` tails), middle-dot tab titles, **WebView2** (`msedgewebview2`), **Chrome PWA** (`chrome_proxy`), and the **YouTube** Windows app process name.

## Notes

- **Reclassify with current rules:** Day/week totals and HTML export use your **current** rule set when you build the report (changing a rule updates how past samples are bucketed).
- **GitHub release text** for this repo is taken from this file at tag time so every shipped version has a **written** feature list.
