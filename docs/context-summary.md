# Context summary — What Am I Doing (project)

**Last updated:** 2026-04-21 (Step 16: websites in summary + HTML at-a-glance)

## Product goal

Windows desktop app for **meaningful** time tracking: foreground window + **idle detection**, **rules/categories**, **local SQLite** storage, **dashboard** + **HTML export** for parents. **.NET 8 WPF**. Positioning: families where parents guide screen time; data stays local in v1.

## Implemented (current state)

### v1 baseline
- Tray app, single instance, **sampling** with configurable interval (default 5s) and idle threshold (default 2 min).
- **SQLite** at `%LocalAppData%\WhatAmIDoing\activity.sqlite3`.
- **Rules**: process equals/contains, window title contains, **context value contains**; priority order; optional **exclude from totals**.
- **Built-in default rules** seeded when empty (`BuiltInDefaultRules.cs`). Adding a user rule with the same match type + pattern **replaces only that** preset.
- Dashboard with day or 7-day window, summary, by-category and by-process tables, HTML export.

### Step 1 — Context extraction
- `Models/ContextKind.cs`, `Services/TitleContextExtractor.cs`: parse window titles into `Site` / `YouTube` / `Project` + value (host, channel/video, project folder).
- DB migration: `samples.context_kind`, `samples.context_value`.
- `ReportAggregator` rolls up top sites / YouTube / projects; dashboard "Highlights" tabs and HTML top-N sections show them.
- `MatchKind.ContextValueContains` lets rules match the extracted value (e.g., "Khan Academy" channel → Study).

### Step 2 — Companion audio
- `Services/AudioSessionInspector.cs` (WASAPI P/Invoke `IAudioSessionManager2`/`IAudioSessionControl2`) snapshots processes producing audio.
- DB migration: `samples.companion_audio`.
- Sampler co-attributes audio to apps/categories so a Discord call alongside Minecraft counts on both. Dashboard summary shows voice/mic activity totals; HTML mirrors it. Toggle in Settings.

### Step 3 — Visual dashboard + HTML
- `Services/CategoryColors.cs` (deterministic palette) and `Services/ChartRenderer.cs` (WPF Canvas + inline SVG).
- Dashboard: hourly timeline (single day) or 7-day heatmap (week view), category legend, drill-down popup (`CategoryDrillWindow`) on double-click.
- HTML export embeds the same chart as inline SVG with legend.

### Step 4 — Opt-in screen capture
- `Services/ScreenCaptureService.cs`: periodic primary-screen JPEG (≤1280px, q70), encrypted with **DPAPI (CurrentUser)**, retention purge, exclusion list, skips when idle/locked/paused.
- `Services/ScreenOcrService.cs`: `Windows.Media.Ocr` text extraction.
- DB: `screen_events` table; tray menu has "Pause screen captures for 30 minutes" / "Resume".
- HTML export: optional "On-screen signals" section with keyword frequency and (opt-in) decrypted thumbnails.

### Step 5 — Polish & distribution (current)
- `Services/CrashLogger.cs` wired in `App.OnStartup` → `%LocalAppData%\WhatAmIDoing\logs\YYYY-MM-DD.log`.
- `Services/AutoStartService.cs` toggles `HKCU\…\Run` with `--minimized`. App honors `--minimized` / `--tray` to skip showing the dashboard at launch.
- `Services/PinManager.cs` (PBKDF2-SHA256, 200k iterations, salted) + `PinPromptWindow.xaml`. `App.EnsurePinUnlocked` gates dashboard reveal AND tray-Exit; once unlocked, the session stays unlocked until process exit. PIN setup in Settings.
- `AboutWindow.xaml` (version + data-folder shortcut), available from Settings.
- `app.manifest` (long-path aware, Win10/11 supportedOS) + `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in csproj.
- `WhatAmIDoing.csproj` carries product/version metadata, conditional `<ApplicationIcon>app.ico</ApplicationIcon>` (drop the file in to enable), and a `PublishSingleFile=true` profile (self-contained win-x64, ReadyToRun, compressed).
- `scripts/publish.ps1` builds the single-file EXE in `src/WhatAmIDoing/bin/Publish/win-x64/` (verified ~95 MB).
- `installer/WhatAmIDoing.iss` — Inno Setup script: per-user install to `%LocalAppData%\Programs\WhatAmIDoing`, no admin, optional desktop shortcut + auto-start checkbox writing the same `Run` key with `--minimized`.
- README rewritten; new `docs/parents.md` parent-facing one-pager.

### Step 6 — Thinking bucket + per-rule idle overrides
Idle detection went from binary (Active / Idle) to three-way (Active / Thinking / Idle) to better handle apps like Cursor where reading code with no input is still productive time.

- `Models/ActivityState.cs` — `Active | Thinking | Idle` with db-string helpers.
- `ClassificationRule.IdleThresholdMsOverride` and `ClassificationRule.ThinkingExtraMsOverride` (both nullable) + DB migrations `rules.idle_threshold_ms_override` and `rules.thinking_extra_ms_override`.
- `BuiltInDefaultRules`: IDE / creative rules (Code, devenv, rider, WebStorm, PyCharm, Sublime, Blender, Photoshop) seed a 5-min idle override. **Cursor specifically** seeds idle=3 min **and** thinking=2 min so it lands on Active 0–3 / Thinking 3–5 / Idle 5+. A one-time migration (`MigrateCursorBuiltInThresholds`) upgrades existing Cursor preset rows from the old 5-min idle default to the new 3+2 pair, only if the user hasn't customized them.
- New setting `thinking_extra_ms` (default 180000). Bucketing in the sampler:
  - `inputIdleMs < effectiveIdle` → **Active**
  - `< effectiveIdle + thinkingExtra` → **Thinking**
  - otherwise → **Idle**
  where `effectiveIdle = matchedRule?.IdleThresholdMsOverride ?? globalIdle` and `effectiveThinking = matchedRule?.ThinkingExtraMsOverride ?? globalThinkingExtra`.
- `CategoryClassifier.MatchRule(...)` new helper so the sampler can look up per-rule overrides without tripping the idle short-circuit.
- `ActivitySamplingService` resolves the matched rule first, then decides state, then picks category (Active/Thinking land in their rule category; only Idle collapses to the generic idle category).
- DB migration `samples.activity_state` stores `"active" | "thinking" | "idle"`; legacy rows fall back to `user_idle`.
- `ReportAggregator` adds `SecondsThinking`, `ThinkingSecondsByCategory`, `ThinkingSecondsByProcess`; drill-down rows carry both Active + Thinking seconds. Thinking counts toward context / audio / heatmap (engaged time) but is reported separately for category + process totals.
- Dashboard: `CategoryGrid` and `ProcessGrid` now show Active / Thinking / Total columns. Summary line now reads "Active (typing / clicking)" + conditional "Thinking (reading / paused)" + "Idle / AFK" with the current thresholds quoted. Category drill-down window gets Active + Thinking columns.
- `RulesWindow`: new "Idle min" + "Think min" grid columns and optional "Idle threshold override (minutes)" + "Thinking grace override (minutes)" inputs on the Add Rule panel.
- `SettingsWindow`: new "Thinking (reading / paused) for an extra N minutes" field; 0 disables the bucket.
- `HtmlReportExporter`: summary adds Thinking row (conditional); By-category and By-app tables gain Active / Thinking / Total columns; trailing note explains the three buckets and the per-rule override.

### Step 8 — Snappier idle defaults, computer-time card, friendlier Add Rule
Second polish pass after testing: shorter idle thresholds, a "how long was I on the computer today / this week" readout, and a much easier rule-adding flow.

- **Idle defaults halved.** Global idle 2 min → 1 min; global thinking grace 3 min → 1.5 min. IDE / creative built-in idle overrides 5 min → 2.5 min. `MigrateSnappierIdleDefaults` cuts over any DB that still had the old values (settings and rules), but skips anything the user has customized.
- **Cursor tuned to 30s + 30s** (Active 0–30s / Thinking 30–60s / Idle 60s+). `MigrateCursorBuiltInThresholds` now handles both the original 5-min default and the intermediate 3 + 2 default, only touching the preset row when the value still matches one of those historical defaults.
- **Computer-time card.** `AppDatabase.CountSamplesBetween` returns `(counted, ignored)` sample counts; the dashboard summary now leads with "On computer today: Xh Ym" and "On computer last 7 days: Yh Zm" (counted × interval). Idle and Thinking are included because you *were* at the machine; rules marked Exclude-from-totals (HWiNFO, ThrottleStop) are held out so background monitors don't inflate the number.
- **Chart legend polish.** Larger swatches (14×14, rounded), includes the per-category time next to the label, hides Idle / Ignored so real activity stands out.
- **RulesWindow UX rewrite for beginners:**
  - Match-type dropdown is now plain English ("App name is exactly" / "App name contains" / "Window title contains" / "Page / video / project contains"). Uses a Tag-based lookup instead of index, so re-ordering items can't silently break anything.
  - A **Capture focused app (3s)** button counts down, then grabs the current foreground process + title via `ForegroundWindowHelper` and pre-fills the form. Handles the "I'd need to alt-tab" problem gracefully.
  - Pattern helper text updates live per match type.
  - Priority + idle/thinking overrides moved into a collapsed `Advanced` `Expander` so the default form is just: match type, pattern, optional exclude, category.
  - Grid `MatchLabel` column uses the same plain-English wording ("App contains", "Page contains") to stay consistent.

### Step 14 — Richer capture flow + per-rule notes
Clearing two items from the rules backlog. The capture flow now gives a three-way choice (app / title / page) instead of guessing, and every rule can carry a free-form reason.

**Capture flow.** `Services/TitleContextExtractor.cs` already knew how to pull a Site / YouTube video / Project out of a foreground window title. The Rules window now uses that at capture time:

- `Capture_OnClick` kicks off the same 3-second countdown, but the hint row also shows a **Cancel** button (`CancelCaptureBtn`) so you can bail without firing. Cancelling stops the timer, re-enables the capture button, and drops the hint to a neutral "Capture cancelled."
- `PrefillFromForeground` now stores a `CaptureSnapshot(process, title, contextKind, contextValue)` on the window and reveals a new **"What should this rule match?"** panel (`CapturePickRow`) with up to three pick-buttons drawn from the capture:
  - *Match app: "chrome"* → `ProcessNameContains` with the process name (the default; tolerant to Chromium helper processes).
  - *Match title contains: "Some Video Title"* → `WindowTitleContains`. When the extractor found a clean page/video/project value, that's used as the pattern instead of the raw window title (which is noisy, e.g. "(3) Some Video Title - YouTube - Mozilla Firefox"); we also trim to 60 chars so the pattern keeps matching after the title shifts.
  - *Match YouTube video / page / project: "…"* → `ContextValueContains`. Hidden when the extractor couldn't identify a context (generic apps like Notepad just show App + Title).
- Clicking a pick-button calls `ApplyMatchChoice(kind, pattern)`, which swaps the match-kind ComboBox and the pattern TextBox together. The pick-row hides itself after a successful `Add rule`.

So for "I want a rule for Khan Academy on YouTube", the old flow was: capture → realize the process is `chrome` → manually switch to "Page / video / project contains" → manually retype "Khan Academy". The new flow is: capture → click the "Match YouTube video" button → done.

**Per-rule notes.** Tiny but long-requested; the idea is a one-liner parents can read when sharing a report, e.g. "Maple Bear Addon — my Minecraft addon project, counts as coding."

- `rules.notes TEXT` column + `EnsureRulesNotesColumn` migration (nullable, safe on old DBs).
- `ClassificationRule.Notes` (nullable init-only property).
- `AppDatabase.AddUserRule(...)` gained an optional `notes` parameter; null/blank writes `NULL`.
- `AppDatabase.GetRules()` reads the new column.
- `RulesWindow.xaml`: a `NotesBox` (multiline, max 96px tall) lives inside the existing `Advanced` expander so the default form stays minimal. The grid picks up a new **"Note"** column — a 42px cell showing `✎` when a note is attached, with the full text surfaced as the cell tooltip via a `DataGridCell` style.
- `RuleRow` exposes both `Notes` (full text → tooltip) and `NotesGlyph` (`""` or `"✎"` → cell content).

**Not done this round (deliberately parked):** Separate Apps/Websites tabs; AND-combined process+context rules; time-of-day scoping; soft limits; editing existing rule notes in-place (today you'd delete + re-add). Each of these is a bigger model change and deserves its own session.

### Step 15 — “Watching” isn’t AFK: speaker audio + YouTube margin; page / video / project rule kinds
User: Long documentaries and videos still show long idle time because there’s no input; and rules should split page vs YouTube video vs project.

**A — Passive engagement (input idle vs really away)**  
We can’t see “play” state in the browser, but on Windows the foreground’s **speaker (render) audio** is a good proxy for *watching or listening*.

- `AudioSessionInspector.ForegroundAppHasActiveRenderAudio(processName)` — enumerates the same Core Audio path as the companion list, but **render only** and checks whether the **foreground** process is in the active-session set. Not mic/capture, so a Discord call in the background does not re-use this.
- `ActivitySamplingService`: if `GetPassiveMediaAudioEngagementEnabled()` (new setting, default **on**), and that returns true, we set `inputIdleMs = 0` *before* the Active / Thinking / Idle check — you stay **Active** while the focused app is making sound. Covers Netflix/Disney in the title bar, VLC, a loud YouTube tab, etc. If you mute, we fall back to the next bullet for YouTube tabs only.
- `else if` the extractor set `ContextKind.YouTube` (in-browser YouTube *video* tab) we multiply the rule’s (or global) `effectiveIdleMs` and `effectiveThinkingMs` by `GetYouTubeContextIdleScale()` (default **4×**, 1–10, stored as `youtube_context_idle_scale`) so a **muted** or very quiet YouTube long watch still has a long runway before Thinking/Idle. Caps at 2 h idle + 1 h thinking so typos can’t go crazy.

**B — `MatchKind` 4/5/6: site, YouTube video, project (narrow) vs 3 = any**  
- `ContextSiteContains (4)`: `context_kind` must be `site` and the pattern matches `context_value`.
- `ContextYouTubeVideoContains (5)`: must be `youtube`.
- `ContextProjectContains (6)`: must be `project`.  
- `ContextValueContains (3)`: legacy “any of the three.”  
- `CategoryClassifier` / `ReportAggregator` / `MainWindow` re-classify pass take `ContextKind` from the sample, not from re-parsing.  
- `AddUserRule`: for kinds 3–6, `DeleteBuiltInContextRulesWithSamePattern` removes **all** built-in rows with the same **pattern** and `match_type` in (3,4,5,6) so switching “Khan on YouTube only” vs “any context” still replaces the preset. Other kinds use the old single-`(match,pattern)` delete.  
- Rules UI: four separate dropdown lines; capture **Match page/YouTube/project** now calls `ApplyMatchChoice` with 4/5/5/6/3 from `CaptureSnapshot.ContextKind`.

**C — Settings** new “Video & long-form watching” block: checkbox for speaker engagement + a small box for the YouTube scale factor.

**Limits / honesty:** A silent tab with a GIF or no output might still go idle. Full-screen *non-browser* video without a separate `TitleContext` still benefits from the **audio** path when sound is on. A future version could add optional “VLC/MPV = always long timeout” heuristics; not in this pass.

### Step 16 — Websites in the text summary (dashboard + HTML)
User: daily/weekly summary should list which websites (with times), not only in the Highlights tabs.

- `MainWindow.RefreshReport`: `BuildWebContentSummaryText(report)` appends to the **Summary** card after the voice line. Two bulleted subsections when data exists: **YouTube (video in tab)** (top 8) and **Other pages & sites** (top 10) from `ActiveSecondsByYouTube` / `ActiveSecondsBySite`, with `TruncateForSummary` on long titles. If both buckets are empty, a short explanation points to supported browsers + engaged time + Highlights.
- `HtmlReportExporter`: `AppendWebsitesAtAGlance` runs immediately under the first summary metrics `<table>`, with smaller top-N (6 YouTube, 8 sites) so parents see “where on the web” before the chart. The existing “Top sites / pages” and “Top YouTube” full tables still appear later in the document (top 15 each). Empty case: a `note` paragraph.

### Step 13 — Retroactive reclassification: rules changes reshape past reports
User: "If I played Minecraft for an hour and then added a rule saying Minecraft was Gaming instead of Uncategorized, it should update the current amount of time I have spent on that category." Completely fair: categories should be a view over the raw observation data, not a snapshot.

What the aggregator used to do: trust `samples.category` and `samples.ignored` — which were written once, at sample time, against whatever rules existed *then*. Adding a rule only changed future samples.

What it does now:

- `ReportAggregator.Build(...)` takes an optional `IReadOnlyList<ClassificationRule>? currentRules` parameter. When provided, every sample is re-run through `CategoryClassifier.Classify` using process name, window title, context value and idle state that we already persist, and the fresh `(category, ignored)` pair is used for *every* rollup — totals, drill-downs, hourly / daily-category slices, companion-audio "voice while gaming" check.
- `SampleRow.Category` / `SampleRow.Ignored` become cached hints only; they still come back from `GetSamplesBetween` for compatibility, but nothing in the report pipeline relies on them any more.
- `MainWindow.RefreshReport` pulls `App.Db.GetRules()` once per refresh and feeds it into both `Build` and a new rule-aware `ComputeOnComputerSeconds`. The latter used to call `CountSamplesBetween` (a fast SQL SUM over `ignored`); it now reads samples and re-classifies them, so adding an "Ignore HWiNFO" rule drops those seconds from "on computer" totals immediately, not only going forward. `CountSamplesBetween` is left in place but is currently unused.
- `Export_OnClick` passes the same `rules` list into the HTML report build.
- Summary card gets a new one-liner: "Categories reflect your current rules — adding or editing a rule updates past totals too." so the user isn't surprised when yesterday's numbers move.
- Sampling still writes the current classifier output into `samples.category` / `samples.ignored`; this keeps old DBs readable and gives us a crash-forensic trail of "what did the app think at the time".

The `ActivityState` column (active / thinking / idle) is *not* recomputed, because we don't store the raw `inputIdleMs` that would let us redo the per-rule override math. Changing a rule's idle threshold still only affects new samples. That's fine for now — the user-facing "Minecraft is Gaming" case is about categories, and the idle-threshold change is a minority use case.

### Step 12 — Root cause of the Rules-grid chaos: Ignore column's TwoWay binding
After disabling virtualization in step 12 (kept, because we want predictable rendering anyway), the user hit a *new* error dialog on the dashboard that turned out to be the smoking gun:

> A TwoWay or OneWayToSource binding cannot work on the read-only property 'IgnoreInTotals' of type 'WhatAmIDoing.RuleRow'.

`DataGridCheckBoxColumn` binds two-way by default. `RuleRow.IgnoreInTotals` is get-only (init-only on a C# record-shaped class). So WPF threw a binding exception **on every single row** during setup, which explains every weird symptom we saw: rows where the binding threw mid-setup had their remaining cells left half-initialized, producing the "blank row between data rows" pattern. Disabling virtualization didn't fix it because the problem was per-row binding, not virtualization-recycling.

**Fix applied:** `Binding="{Binding IgnoreInTotals, Mode=OneWay}"` on the Ignore column. One-line change, keeps the checkbox visibly accurate, nothing tries to write back to a read-only property.

**Also:** removed the blocking `MessageBox.Show` from `MainWindow.Rules_OnClick`'s catch block — a modal error dialog covered the dashboard's charts and tables exactly when the user needed them. It still logs via `CrashLogger` and still re-enables the main window; it just doesn't grab the user's screen any more.

Virtualization stays off (cheap, predictable), the `_captureTimer` lifetime fixes stay, the `Rules_OnClick` bulletproofing stays. The OneWay binding fix is the one that should make the symptoms vanish.

### Backlog for the next rules-focused session
Remaining user-requested follow-ups (moved here so we don't lose them):

1. **Confirm the OneWay fix actually cleared the rendering issues.** If blank rows still appear, we fall back to the step-11 theory list (ObservableCollection in place of reassignment, ListView, deferred first load, etc.).
2. **Richer rule details (beyond notes).**
   - Tags on a rule (e.g. `schoolwork`, `focus`, `weekend-ok`).
   - Time-of-day / day-of-week scoping ("this rule only counts weekdays 8–16").
   - Soft limits per rule with a gentle "heads up, you're past 1h of YouTube" banner.
   - In-place editing of an existing rule (currently you delete + re-add).
3. **Websites vs apps — better split in the rule model.** Users think of two distinct things: "apps I use" and "sites/pages I visit inside apps". Ideas:
   - Separate tabs in the Rules window: "Apps" vs "Websites & pages".
   - Allow combined rules ("chrome + contains youtube.com/shorts → Short-form video"), so context is AND-ed with process for more accurate categorization.
4. **HTML export: include rule notes.** Right now the notes live in the app; surfacing them next to the category totals would make the shared report far more parent-friendly.
5. **Capture flow extras.** The step-14 pick-row covers process / title / page. Still wishlist:
   - A "Pick from running apps" list as an alternative to the countdown.
   - Adjustable / longer countdown for people who need to navigate through multiple windows.
6. **More specifics we'll discuss when we get there.**

### Step 11 — Rules window stability: virtualization, capture timer lifetime, modal recovery
Step 10's fix wasn't enough. User reported three stacked issues on the Rules window:
1. After Capture → Add rule, the grid showed only one row (or none) with the rest drawn as empty gridlines, even though the `StatusText` I added confirmed the DB really did have 44 rules.
2. After Restore suggested defaults, still looked broken visually (DB was fine).
3. After closing + reopening the window twice, the "Rules..." button on the dashboard stopped responding at all.

Root causes and fixes:

- **Visual (1 and 2):** `ScrollViewer.CanContentScroll="False"` (pixel scrolling) forces DataGrid out of its virtualizing panel. On `ItemsSource` swap, the container layout gets stuck halfway and most rows draw as empty placeholders. Switched back to item-scrolling with explicit `EnableRowVirtualization="True"` + `VirtualizingPanel.VirtualizationMode="Recycling"`. `Reload()` still clears `SelectedItem` before reassigning `ItemsSource`, which was the other half of the fix.
- **Stuck "Rules..." button (3):** `_captureTimer` (the "Capture focused app (3s)" countdown) kept running after the window was closed mid-countdown. Its next Tick ran against disposed WPF controls, threw `InvalidOperationException`, and bubbled into the message pump, leaving the main window stuck in modal-disabled state so no further button click did anything. Fixes:
  - `RulesWindow.Closed` now stops and nulls `_captureTimer`.
  - The Tick handler bails out silently when `IsLoaded` is false (defense for a tick already queued when Closed fires).
  - `MainWindow.Rules_OnClick` wraps `ShowDialog` in try/catch/finally, logs to `CrashLogger`, and force-re-enables the main window + calls `Activate()` on every exit path so a future bug like this can never again leave the app unable to reopen Rules.
  - `Reload()` is wrapped in try/catch: rendering failures write to `CrashLogger` and surface as a status-line message instead of taking down the window.

Net effect: grid renders correctly after Add / Restore without needing a close/reopen, and the dashboard's Rules button can't be poisoned by a Rules-window exception any more.

### Step 10 — Rules list refresh after Add (visual-only bug)
After clicking "Capture focused app (3s)" → "Add rule", the DataGrid in `RulesWindow` collapsed to show just the previously-selected row, with the rest of the visible height rendered as empty gridlines. Closing + reopening the window showed the full list again, so the DB was fine — this was a WPF quirk: when you reassign `DataGrid.ItemsSource` with pixel scrolling on (`ScrollViewer.CanContentScroll="False"`) and the previously selected row no longer exists in the new list, the container recycling path gets stuck.

- `Reload()` now clears `SelectedItem`, nulls `ItemsSource`, reassigns, and scrolls the first row into view, so the rebuild is always from a clean state.
- Added a live `StatusText` above the grid: "N rules · X yours, Y suggested" (or a nudge to restore defaults when empty), so the user and I can confirm at a glance that the DB state matches what's drawn.
- `Capture_OnClick` now detects if the countdown caught `WhatAmIDoing` itself (user didn't alt-tab) and shows a warning instead of pre-filling a self-rule.

### Step 9 — 7-day view: stacked category bars instead of heatmap
Feedback after step 8: "it's the during the 7 days part chart that is confusing." The day × hour blue heatmap showed *when* you were on but not *what of*, and it didn't share a visual language with the daily stacked bars.

- `AggregatedReport.DailyCategorySeconds : IReadOnlyDictionary<string,int>[]` — per-day per-category engaged seconds (Active + Thinking, excluding Idle / Ignored). Populated alongside the existing `DailyHourlyActiveSeconds` in `ReportAggregator.Build`.
- `ChartRenderer.DrawDailyStackedBars(canvas, report)` + `BuildDailyStackedBarsSvg(report)` — one horizontal stacked bar per day:
  - Left: day label (`ddd MMM d`, bold).
  - Middle: a rounded track filled with stacked category segments, width proportional to that day's total engaged time vs. the busiest day in the range, same `CategoryColors.Pick` palette as the hourly view.
  - Right: total engaged time for the day (right-aligned), or `—` if zero.
  - Filters Idle / Ignored / Uncategorized so only meaningful activity shows, matching the legend above.
- `MainWindow.UpdateChart` now renders the stacked bars for `DayCount > 1` and retitles the card **"Activity by day — 7 days ending {date}"**. Canvas height scales to `DayCount * 32 + 12`.
- `HtmlReportExporter` uses the stacked-bar SVG for the 7-day export and places the category legend above it.
- Old heatmap methods (`DrawDailyHeatmap`, `BuildDailyHeatmapSvg`) and helpers (`HeatmapColor`, `InterpolateColor`, `ParseHex`, `HeatmapEmpty`, `HeatmapFull`) removed; still recoverable from git if we ever want an hour-of-day heatmap back as a second view.

### Step 7 — Polish: Rules layout, Comet, more built-ins, self-healing seed
Fix-it round after the first real usage session.

- **`RulesWindow.xaml` layout fix** — window is now `640×760` with `MinHeight=520`; `RulesGrid` has `MinHeight=220` and pixel scrolling (`ScrollViewer.CanContentScroll="False"`), so the list no longer collapses to three rows. The Add Rule panel is wrapped in a `ScrollViewer`, and idle + thinking overrides are stacked as a two-column grid with label-above-input.
- **Category field is now an editable `ComboBox`** populated from existing rule categories. Multiple rules with the same label are how you build a custom category that spans apps AND page patterns (e.g. same "SAT prep" category on a Khan Academy context rule and a college-prep site rule).
- **`TitleContextExtractor`** now recognizes `comet` as a browser, so Comet tab titles parse into Site / YouTube context just like Chrome/Edge.
- **`BuiltInDefaultRules` additions**:
  - Web browser: `comet`
  - Notes: `Sticky Notes` (window-title match because Sticky Notes renders via `ApplicationFrameHost` on Win11), `Notepad`
  - System tools: `PredatorSense` (counted — actively used), `ThrottleStop` & `HWiNFO` (seeded with `IgnoreInTotals=true` because they mostly idle in the background gathering metrics)
- **Self-healing seed** — new `TopUpMissingBuiltInRules(conn)` runs on every launch. For each suggested rule, if no existing rule (built-in *or* user-owned) has the same `match_type + pattern`, the preset is inserted. This makes old DBs pick up new built-ins without clobbering user customizations, and without bringing back rules the user replaced via `AddUserRule` (which deletes the matching preset by design).

## Decisions locked

- Local-only data in v1; cloud sharing is a future roadmap item.
- Per-user install, no admin elevation.
- PIN gates *changing* settings/rules and *exiting* — sampling continues regardless.
- DPAPI CurrentUser for screen captures (only the same Windows user can decrypt).

## Open / future
- Per-rule weekly limits with soft alerts.
- Browser-tab URL ingestion (currently relies on title bar).
- Scene-aware screenshot interpretation (beyond OCR keywords).

---

*Updated when chat context is summarized for continuity.*
