# Screenshot analysis module (planned, not in v1)

## Purpose

Optional future layer: periodic **screen captures** + **on-device or API vision/OCR** to infer what is on screen when window titles are not enough (e.g. same browser tab type, games, or mixed content). This complements foreground-window sampling; it does not replace it.

## Design constraints (when we implement)

- **Explicit opt-in** in settings; off by default until a parent/device owner enables it.
- **Retention**: cap how many images or derived labels are kept (e.g. days), with automatic purge.
- **Storage**: encrypt the image cache at rest; store under `%LocalAppData%\WhatAmIDoing\` (or a dedicated subfolder).
- **Performance**: adaptive interval (longer when idle), optional downscale before analysis.
- **Reporting**: attach **labels** to the same timeline as samples (e.g. “YouTube visible”, “code editor visible”) with confidence; show in HTML export as a separate section.

## Integration points in the current codebase

- `ActivitySamplingService` / DB schema: add optional columns or a side table `screen_events` keyed by `ts_utc` or sample `id`.
- **Classifier**: merge **text from OCR** + **window title** + **process name** for category rules (later).
- **Exports**: extend `HtmlReportExporter` with an “On-screen signals” section when data exists.

## Open decisions (for a future design session)

- Local model vs cloud API (cost, privacy, offline use).
- Whether to blur taskbar or known sensitive regions by default.
- How to align with parental expectations (clear disclosure in the exported report).

---

*This document is planning only; v1 ships without screenshot capture.*
