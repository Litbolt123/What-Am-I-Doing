# Archived: Rule Inspector window (`RuleInspectorWindow`)

**Removed from v1.1.x builds** (dashboard button **Inspect rule…**).

The tool polled `ActivityClassificationSnapshot` every 2s and showed which rule matched the foreground window. It duplicated information parents already get from **Rules** + sampling, and as a **modal-ish window** it prevented interacting with the rest of the app while open — which made it awkward to inspect anything except this app.

## Restore (future update)

1. Copy `RuleInspectorWindow.xaml` and `RuleInspectorWindow.xaml.cs` into `src/WhatAmIDoing/`.
2. In `MainWindow.xaml`, add a toolbar button and `InspectRule_OnClick` handler (see git history before archive).
3. In `Services/AccessibilityKeyboardHelpers.cs`, include `RuleInspectorWindow` in the Esc-closes branch alongside `FirstRunChecklistWindow` / `CategoryMergeWindow` if keyboard helpers should close it.

Consider a **non-modal** or **tool window** host so users can switch apps without closing the inspector first.
