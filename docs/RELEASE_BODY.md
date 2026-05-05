# What Am I Doing 1.1.0 — Windows installer

## What’s new

- **Dashboard:** The **Summary** area is **scrollable** and shares space with **Highlights**, so long summaries (week view, lifecycle log, install footers) fit on smaller screens such as 14″ laptops.
- **Family monitoring (Settings):** Optional **activity log** when the app **starts**, **closes**, or the app **version changes**; entries show in the dashboard summary for the selected day or week. Optional **“bring the app back”** Windows scheduled task (every 5 minutes; not tamper-proof — documented in Settings).
- **Rules:** Clearer copy on **priority** (higher number checked first; first match wins), including in **Advanced**.
- **Suggested rules:** Larger **built-in** set (gaming launchers, Roblox title match, GitHub Desktop, OBS, lock screen ignored from totals, family-oriented **explorer** / **Sticky Notes** presets, and more). New presets **merge in on launch** without replacing your custom rows when the same match already exists.
- **About:** **Check for updates…** compares this build to the latest GitHub release tag and can open the Releases page (download the new installer separately — no in-app upgrade install). If GitHub has **no published release** yet, the app explains that instead of showing a **404** error. Version line shows the full build (**1.0.2.5** not truncated to **1.0.2**). **Open Releases page** button opens the project Releases in the browser.
- **Docs for parents / maintainers:** [Updating the app](updating-app.md), [Family PIN roadmap](family-pin-roadmap.md) (future PIN + recovery ideas), and maintainer [releasing](releasing.md) notes.

## Notes

- **License:** This repository is published under the **MIT License** (`LICENSE` in the repo root) — permissive, widely understood, and fine for most consumer / family apps you distribute as binaries. If you need **copyleft** (derivative work must stay open) or **patent** language, consider **GPL-3.0** or **Apache-2.0** instead; ask a lawyer for commercial or liability-sensitive products.
- **Reclassify with current rules:** Day/week totals and HTML export use your **current** rule set when you build the report (changing a rule updates how past samples are bucketed).
- **GitHub release text** for this repo is taken from this file at tag time so every shipped version has a **written** feature list.
