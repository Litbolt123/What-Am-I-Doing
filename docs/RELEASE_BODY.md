# What Am I Doing 1.1.1 — Windows installer

## What’s new

- **Charts:** **Uncategorized** time now appears as its own segment in the **multi-day “Activity by day”** stacked bars (same grey as elsewhere). Idle and Ignored stay excluded from those segments by design.
- **Settings:** Layout tuned so controls fit at the **default window size**: wider default/min width, backup buttons **wrap**, **Quiet hours** uses a clearer grid + wrapped helper text, long checkbox labels **wrap**, and horizontal clipping is avoided without sideways scrolling.
- **Inspect rule:** The dashboard **Inspect rule…** tool is **removed** for now (it blocked using the rest of the UI while open). The old window code is kept under **`archive/rule-inspector/`** in the repo for a possible future non-modal version.

## Notes

- **Reclassify with current rules:** Day/week totals and HTML export use your **current** rule set when you build the report (changing a rule updates how past samples are bucketed).
- **GitHub release text** for this repo is taken from this file at tag time so every shipped version has a **written** feature list.
