# Context summary — What Am I Doing (project)

This file is the **long-lived project snapshot** plus a **session log** of agent-driven changes. When you summarize chat context, **append a new dated entry** under **Session log (agent context)** (below); do not delete or rewrite older session lines (edit only if a fact was wrong).

## Session log (agent context)

Newest first. One line (or short bullet group) per agent session / handoff is enough.

- **2026-05-06** — **Ship v1.1.1:** `Directory.Build.props` / `app.manifest` / Inno fallback → **1.1.1** (assembly **1.1.1.0**); [`docs/RELEASE_BODY.md`](docs/RELEASE_BODY.md) for GitHub release (Uncategorized in stacked bars, Settings layout, Inspect rule archived). Tag **`v1.1.1`** pushed → Windows installer workflow.
- **2026-05-06** — **Inspect rule archived:** Removed dashboard **Inspect rule…**; [`RuleInspectorWindow`](archive/rule-inspector/) copied under [`archive/rule-inspector/`](archive/rule-inspector/README.md) (restore steps in README). Esc helper no longer references that type. User asked **no commit/push/release until after manual test approval**; **v1.1.1** naming discussed for next bump when releasing.
- **2026-05-06** — **Charts — Uncategorized in stacked day bars:** [`BarCategories`](src/WhatAmIDoing/Services/ChartRenderer.cs) no longer strips **Uncategorized** (still strips Idle/Ignored); weekly/HTML stacked bars show the grey segment; hourly timeline already included it from [`ReportAggregator`](src/WhatAmIDoing/Services/ReportAggregator.cs).
- **2026-05-06** — **Settings layout:** Wider default/min width (640/560), backup row `WrapPanel`, quiet hours 2-row grid + wrapped footnote, long checkboxes use wrapped `TextBlock` content; `ScrollViewer` horizontal disabled to avoid clipping scroll — fixes quiet hours and buttons cut off at default size.
- **2026-05-06** — **Inspect rule tooltip:** Clarifies that the inspector polls the **current** foreground window every 2s (user can open it, then switch to another app) — not “stuck” to focus at click time.
- **2026-05-06** — **Accessibility + ranges + roadmap batch:** Settings toggles (`ui_large_text`, `ui_high_contrast`, `ui_keyboard_helpers`, default off) via [`AccessibilityUi`](src/WhatAmIDoing/Services/AccessibilityUi.cs) + theme XAML; [`MainWindow`](src/WhatAmIDoing/MainWindow.xaml) range **14d / 28d / calendar month**, scrollable chart, summary (**ignored %**, optional quiet-hours engaged estimate, **period vs previous period** via [`ReportComparisonText`](src/WhatAmIDoing/Services/ReportComparisonText.cs)), **Inspect rule…**, **Merge categories…**. [`ActivityClassificationSnapshot`](src/WhatAmIDoing/Services/ActivityClassificationSnapshot.cs) shared with sampler. **First-run checklist**, **backup reminder** tray balloon, **support bundle** + **PC handoff** zips, [`MergeCategoryLabels`](src/WhatAmIDoing/Data/AppDatabase.cs), [`QuietHoursHelper`](src/WhatAmIDoing/Services/QuietHoursHelper.cs).
- **2026-05-06** — **Rules / Settings unsaved close + editable ComboBox:** [`EditableComboHelper`](src/WhatAmIDoing/Services/EditableComboHelper.cs) reads `PART_EditableTextBox` so OK/Save sees typed category before LostFocus (fixes **Add website rule** appearing to do nothing). Fully qualifies WPF types (WinForms also referenced). [`RulesWindow`](src/WhatAmIDoing/RulesWindow.xaml.cs): `TryCommitRuleEditor`, editor baseline dirty check, **Closing** Yes/No/Cancel; baseline reset on **ClearEditor** / **LoadRowIntoEditor**. [`SettingsWindow`](src/WhatAmIDoing/SettingsWindow.xaml.cs): `ComputeSettingsFingerprint` (PIN length only, not secret), `TryPersistSettings`, same close prompt; **Cancel** closes without a duplicate prompt.
- **2026-05-06** — **Rules window open failure:** `MatchKindBox.SelectionChanged` ran during `InitializeComponent` before `PriorityBox` was created → `NullReferenceException` in `ApplyRecommendedPriorityForNewRule`; `MainWindow` caught it and the dialog never showed. Guard: return when `PriorityBox` is null.
- **2026-05-06** — **Rules UX + highlights:** **Parent-friendly priorities:** [`RulePriorityGuide`](src/WhatAmIDoing/Services/RulePriorityGuide.cs), browser built-ins use its **190** constant; new rules default priority **200** for context/site/title kinds and **10** for process; save warns if site-like rule ≤190; **Add website rule…** [`WebsiteRuleDialog`](src/WhatAmIDoing/WebsiteRuleDialog.xaml). Same-day context: built‑in **Web browser** for `comet`/Chrome at **190** (parents raise site rules **200+**); **`TitleContextExtractor`** / **`ReportAggregator`** YouTube Highlights vs drill‑down.
- **2026-04-23** — **Privacy / git history (solo maintainer):** Repo grep: **no** occurrences in `src/` or normal `docs/` on the current tree; session log already says “second PC / second household test machine.” Temporary history-rewrite scripts that embedded a specific search string were **deleted** from `scripts/` so the name is not reintroduced by tooling. **Tradeoff:** scrubbing **old** commits (messages + blob diffs) needs `git filter-repo` or a careful `filter-branch` pass and is slow and fiddly (UTF-8 punctuation, Windows paths). With no dependents on `main`, **redact going forward** is often enough; full history rewrite is optional hardening for a public mirror.
- **2026-04-23** — **v1.1.0 prep:** Added root **`LICENSE`** (MIT). `docs/RELEASE_BODY.md` notes license + private-repo / `releases/latest` behavior.
- **2026-04-23** — **About / update check:** GitHub `releases/latest` **404** (no published release yet) handled as `NoPublishedReleases` instead of exception text. **About** version uses `AssemblyInformationalVersion` or `Version.ToString(4)` so **1.0.2.5** is not shown as **1.0.2**. Added **Open Releases page** button; copy tweak for empty Releases.
- **2026-04-23** — **Ship v1.1.0:** `Directory.Build.props` / `app.manifest` / Inno fallback → **1.1.0** (assembly **1.1.0.0**). `docs/RELEASE_BODY.md` set for GitHub release description.
- **2026-04-23** — **Dashboard + releases:** `MainWindow` Summary in `ScrollViewer`; left column `RowDefinition` both `*` so Summary and Highlights split height on small screens. GitHub workflow uses `docs/RELEASE_BODY.md` as `body_path`, `generate_release_notes: false`, verify step (file exists, min length). `docs/releasing.md` + README maintainer paths updated.
- **2026-04-23** — **Built-ins from parent screenshot:** `BuiltInDefaultRules` — Sticky Notes → Documents 150; explorer → Documents 80; Minecraft.Windows Gaming pri 10; GitHubDesktop, `bridge`, obs64 Video Recording, lock screen Ignored+ignore, title Roblox Gaming 10. `MigrateBuiltInFamilyPresetPackFromPhoto` updates matching **built_in** rows only (Notes→Documents, Windows File Explorer→Documents, Minecraft pri 162→10). Chart seed colors: Documents, Video Recording, Ignored (fresh empty `category_colors` only).
- **2026-04-23** — **Future PIN / uninstall / recovery (design only):** Added `docs/family-pin-roadmap.md` — PIN should eventually gate destructive “remove app / wipe data” paths where controllable; pair with **parent-verified email** (or similar) for reset so forgetting PIN does not force DB delete. Notes Windows **Settings → Apps** uninstall limits without Inno/app cooperation. README + `parents.md` link; `PinManager` remarks point to roadmap.
- **2026-04-23** — **Family / parents:** `app_lifecycle_log` table + settings `lifecycle_logging_enabled` (default on), `watchdog_restart_enabled`. `App`: `--spawn-if-stopped` early exit (`Mutex.TryOpenExisting`), `TryAppendLifecycleEvent` on session start/quit; `EnsureTrackerIdentity` logs `upgrade` when version changes. `WatchdogTaskHelper` registers per-user `schtasks` every 5 min. **Settings:** lifecycle + watchdog toggles. **MainWindow:** summary shows lifecycle lines then install block at **bottom**. **RulesWindow:** clearer priority copy. **BuiltInDefaultRules:** Roblox, Roblox Studio, Prism Launcher, Minecraft.Windows, Lunar Client. **About:** “Check for updates…” via `UpdateCheckService` (GitHub API `Litbolt123/What-Am-I-Doing`, no auto-install). **Docs:** `docs/updating-app.md`, README + `docs/parents.md` updates.
- **2026-04-23** — **Default Settings + YouTube scale:** Fresh installs and unmigrated keys match maintainer baseline: **2 s** sampling (`sample_interval_ms` **2000**), **YouTube** idle scale **10×** (was 4×; cap **30** via `AppDatabase.YouTubeContextIdleScaleMax`). **`MigrateSampleAndYoutubeInstallerBaseline`**: `5000→2000` ms sample, `4→10` scale only when values still match old defaults. **`SeedDefaultChartColorsIfEmpty`**: Activity tracker `#00FF00`, Notes `#EAD30D`, Windows (File Explorer) `#FFFF80`, YouTube `#FF8080`. README / parents / Settings UI copy updated.
- **2026-04-23** — **1.0.2.5 validated on second PC:** A second household test machine (**previously** Event 1000 / **0xc000041d** / no `%LocalAppData%\WhatAmIDoing` on **1.0.2.4**) runs the app after **1.0.2.5** (uncompressed single-file publish).
- **2026-04-23** — **WER corroborates 1.0.2.4 crash:** Event **1001** (*Windows Error Reporting*, `APPCRASH`) can share the same **Report Id** as Event **1000** — same single fault, not a separate failure. Signature **P7 `c000041d`**, **KERNELBASE**; minidump under `ProgramData\Microsoft\Windows\WER\…`. README troubleshooting notes Event **1001** vs **1000**.
- **2026-04-23** — **Release 1.0.2.5 (single-file reliability):** Some PCs crashed on launch (**Event 1000**, **KERNELBASE.dll**, **`0xc000041d`**) with **no** `%LocalAppData%\WhatAmIDoing` — before managed startup logs. **`EnableCompressionInSingleFile=false`** in **`publish-installer.ps1`** and **`publish.ps1`** (larger EXE, avoids compressed-bundle issues). **`ScreenOcrService`**: lazy-create **`OcrEngine`** on first OCR call (no static WinRT init at type load). Version **1.0.2.5**; README troubleshooting paragraph.
- **2026-04-23** — **CI fix (1.0.2.4):** **`AssemblyVersion` / `FileVersion`** must be **four** numeric parts only (`1.0.2.4`). Values like **`1.0.2.4.0`** (five parts) trigger WPF **MC1005** during `dotnet publish` on the WindowsDesktop SDK. **`app.manifest`** `assemblyIdentity` version aligned to **`1.0.2.4`**.
- **2026-04-23** — **Ship 1.0.2.4:** Committed on **`main`** (`fb92d5e`), pushed **`origin/main`**, pushed tag **`v1.0.2.4`** (triggers Windows installer workflow / GitHub Release per CI).
- **2026-04-23** — **Release build verification:** Background `dotnet build` … `-c Release` failed with **MSB3027/MSB3021** because **`WhatAmIDoing.exe` held a lock** on `bin\Release\…\WhatAmIDoing.exe` (running dev instance). After the lock cleared, the same **Release** build **succeeded** (0 warnings).
- **2026-04-23** — **Release 1.0.2.4 (self-contained installer):** `publish-installer.ps1` **`--self-contained true`** — .NET 8 + Windows Desktop / WPF embedded in `WhatAmIDoing.exe`; Inno no longer bundles Microsoft’s separate Desktop Runtime installer. CI dropped fetch + verify steps; workflow publish timeout **45 min**. `build-installer.ps1` no longer requires `installer\prereq\`; **`-SkipFetch`** deprecated no-op. README and `docs/releasing.md` updated. Tag **`v1.0.2.4`**.
- **2026-04-23** — **Release 1.0.2.3:** Installer **`InfoAfterFile`** recommends **restart** after .NET install / auto-start. **`App.OnStartup`** defers DB+tray+sampler to **`ApplicationIdle`**, plus **2.5s** extra delay when **`--minimized`** (HKCU Run at logon) so Shell/NotifyIcon is ready — addresses tray missing and process vanishing from Task Manager. Startup error text mentions restart.
- **2026-04-23** — **Release 1.0.2.2 validated:** Maintainer reports GitHub Actions **Windows installer** workflow **green** (no errors); testing **1.0.2.2** installer on a second PC (follow-up to earlier empty-AppData / Desktop-runtime issues).
- **2026-04-23** — **Release 1.0.2.2 (Inno fixes):** (1) `faDirectory` → **`$10`** for directory detection. (2) **Local `const` inside `DotNetWindowsDesktopApp8Present`** is invalid Inno Pascal — ISCC expected `begin`; moved registry subkey to script-level **`RegSubWinDesktop8`**. Tag **`v1.0.2.2`** re-pushed after **`main`** fix when prior tag run failed compile.
- **2026-04-23** — **Release 1.0.2.1:** Version bump to **1.0.2.1** (CI/Inno hardening + `WizardIsComponentSelected` on `main`); tag **`v1.0.2.1`** pushed for GitHub Release / installer artifact.
- **2026-04-23** — **CI hardening (workflow + Inno):** `run-name` wrapped in double quotes and **ASCII hyphens** (avoid YAML/Unicode edge cases). **Install Inno Setup** skips `choco` when `ISCC.exe` already exists (Chocolatey “0 packages / already installed” exit quirks on `windows-latest`). **Publish GitHub Release** `name` quoted + ASCII. Inno `[Code]` `IsComponentSelected` → **`WizardIsComponentSelected`** (Inno 6.7+).
- **2026-04-23** — **GitHub Actions run / Release naming:** `release-windows.yml` adds `run-name` so the workflow run list shows **`WhatAmIDoing v1.0.x — Windows installer`** (from tag/branch, not the unrelated HEAD commit message). **Publish GitHub Release** sets **`name:`** to **`What Am I Doing <MSBuild Version> — Windows installer`** so the Releases page title matches the product version.
- **2026-04-23** — **Release tag `v1.0.2` pushed:** `main` already carried **1.0.2** in `Directory.Build.props` / `app.manifest` / Inno fallback (commit `81bed50`: installer Desktop detection + docs), but only **`v1.0.0`** and **`v1.0.1`** existed on the remote — no installer Release until **`git push origin v1.0.2`** on `main` at `9c8e690` (includes cumulative session-log doc).
- **2026-04-23** — **Context summary format:** Maintainer asked to keep **all** agent context entries, not a single rolling “Last updated” line. Added this cumulative session log and the append-only rule above.
- **2026-04-23** — **Installer / runtime (1.0.2):** Inno `DotNetWindowsDesktopApp8Present` now checks on-disk `dotnet\shared\Microsoft.WindowsDesktop.App\8.*` under `{commonpf64}` and `%LocalAppData%\Microsoft`, plus **HKLM64** and **HKCU64** registry (not HKLM-only). Ready-page copy stresses **Windows Desktop** runtime vs plain “.NET Runtime” / ASP.NET. README + `docs/releasing.md` explain empty `%LocalAppData%\WhatAmIDoing` = CLR never started (missing Desktop). Version bump **1.0.2** (`Directory.Build.props`, `app.manifest`, Inno `#define AppVersion` fallback).
- **2026-04-23** — **GitHub Pre-releases for beta tags:** `release-windows.yml` sets `prerelease` / `make_latest` from MSBuild `Version` when it matches SemVer prerelease (`^\d+(\.\d+)*-.+$`). `docs/releasing.md` section 6 documents `v1.0.2-beta.1`-style tags.
- **2026-05-05** — **Hotfix 1.0.1 + release:** Bumped to **1.0.1**; early `CrashLogger.InstallProcessWideHooks()`, `App.OnStartup` try/catch + MessageBox on failure, tray balloon first-run hint; Inno `[Icons]` `WorkingDir: "{app}"`; README first-run + Programs vs data folder table; added `docs/releasing.md`; pushed tag **`v1.0.1`**.
- **2026-05-05** (approx.) — **CI / Inno reliability:** Dropped flaky ISPP `#ifexist` / `FileExists` guard on bundled runtime; GHA verify step after fetch; `Compile Inno Setup` with `working-directory: installer`; `scripts/build-installer.ps1` `Push-Location installer` before ISCC.

## Product goal

Windows desktop app for **meaningful** time tracking: foreground window + **idle detection**, **rules/categories**, **local SQLite** storage, **dashboard** + **HTML export** for parents. **.NET 8 WPF**. Positioning: families where parents guide screen time; data stays local in v1.

## Implemented (current state)

### v1 baseline
- Tray app, single instance, **sampling** with configurable interval (default **2 s**) and idle threshold (default **1 min**), plus **Thinking** grace (default **1.5 min**).
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
- `Services/PinManager.cs` (PBKDF2-SHA256, 200k iterations, salted) + `PinPromptWindow.xaml`. `App.EnsurePinUnlocked` gates **Settings** and **Rules** when a PIN is set (session remembers until process exit). **Tray Exit** shows `PinPromptWindow` **every time** when a PIN is set — not tied to the session unlock. **Opening the dashboard** (tray or startup) does **not** require a PIN. PIN setup in Settings.
- `AboutWindow.xaml` (version + data-folder shortcut), available from Settings.
- `app.manifest` (long-path aware, Win10/11 supportedOS) + `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in csproj.
- `WhatAmIDoing.csproj` carries product metadata, conditional `<ApplicationIcon>app.ico</ApplicationIcon>` (drop the file in to enable). **`<Version>`** / assembly versions live only in repo-root **`Directory.Build.props`** (single source for EXE + Inno). The **family installer** path uses **`scripts/publish-installer.ps1`** (**self-contained** single-file for Inno from **1.0.2.4** onward; embeds .NET 8 + Windows Desktop / WPF). **`scripts/publish.ps1`** remains an alternate **self-contained** single-file build for portable/dev without Inno.
- `scripts/build-installer.ps1` runs fetch (unless `-SkipFetch` / file present), `publish-installer.ps1`, then **`ISCC /DAppVersion=<MSBuild Version>`** (optional `INNO_SETUP_ROOT`, `-SkipPublish`, `-InstallPrerequisites`).
- `scripts/get-version.ps1` prints `Version` via `dotnet msbuild -getProperty:Version`.
- `scripts/install-build-prerequisites.ps1` tries **winget** then **Chocolatey** to install **.NET 8 SDK** and **Inno Setup 6** on developer machines that build the setup.
- `.github/workflows/release-windows.yml` — reads `Version` from MSBuild; downloads the bundled Desktop Runtime, runs **`publish-installer.ps1`**, compiles Inno; on tag `v*` **requires tag to match** `Version`, publishes **GitHub Release**; `workflow_dispatch` uploads artifact only.
- `installer/WhatAmIDoing.iss` — `InfoBeforeFile` + `LicenseFile`, `[Types]` Express vs Advanced, optional components (stricter idle + screenshots) that write `%LocalAppData%\WhatAmIDoing\install-bootstrap.json` consumed once by `InstallerBootstrap.ApplyIfPresent` in `App.OnStartup`; bundled Desktop Runtime in `{tmp}` with **`[Run]`** after user confirms on **Ready** (or quiet when `WizardSilent`); interactive run uses **`/install /norestart`** without **`/quiet`** so Microsoft’s UI shows; per-user install, `MinVersion=10.0.17763`. No ISPP file guard (was flaky); CI **Verify** step + **`ISCC` from `installer/`** (`working-directory` / `Push-Location` in `build-installer.ps1`).
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

**Per-rule notes.** Tiny but long-requested; the idea is a one-liner parents can read when sharing a report, e.g. "Science fair project — counts as homework."

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
- `else if` the extractor set `ContextKind.YouTube` (in-browser YouTube *video* tab) we multiply the rule’s (or global) `effectiveIdleMs` and `effectiveThinkingMs` by `GetYouTubeContextIdleScale()` (default **10×**, **1–30**, stored as `youtube_context_idle_scale`) so a **muted** or very quiet YouTube long watch still has a long runway before Thinking/Idle. Caps at 2 h idle + 1 h thinking so typos can’t go crazy.

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

### Step 17 — Per-category chart colors; Settings clarity (samples, PIN)
User asked for RGB/custom colors per category (similar blues confusing parents). Implemented:

- SQLite `category_colors(category PRIMARY KEY COLLATE NOCASE, color_hex)` + CRUD on `AppDatabase`.
- `CategoryColors.Bind(Db)` at startup; `Pick(category)` checks overrides then falls back to the hashed palette. `TryNormalizeHex` accepts `#RGB` / `#RRGGBB`.
- **Settings → Chart & report colors:** editable ComboBox of rule categories, hex box, **Pick…** (`ColorDialog`), **Save color**, ListBox of overrides with **Remove**. Saving/removing refreshes `MainWindow` charts immediately.

Settings copy updates:

- Under **Tracking:** activity samples are **not** auto-deleted by the app (only screenshots use retention).
- **PIN:** clarified session behavior for Settings/Rules vs tray Exit (Exit prompts every time).

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
   - In-place editing of an existing rule ~~(currently you delete + re-add)~~ — **Step 18:** Edit selected / Save changes in Rules window.
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
  - Filters Idle / Ignored only; Uncategorized appears as its own segment (grey) when present.
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

### Step 18 — PIN split, editable rules, install identity, backup/import
Parent-flow polish: lock down changes without blocking reading reports.

- **PIN:** `App.ShowDashboard` / tray left-click no longer call `EnsurePinUnlocked`. **Settings** and **Rules** use `EnsurePinUnlocked` (prompt once per session after success). **Exit** from the tray calls `PinPromptWindow` directly — **always** when a PIN is set, not cached with the session unlock. **Export HTML** stays unlocked so viewing/sharing a report matches opening the dashboard.
- **Rules editing:** `AppDatabase.UpdateUserRule` updates a row in place and clears `built_in`. **Rules** window: Edit selected / double-click loads the row into the form; primary button toggles **Add rule** vs **Save changes**; **Cancel edit** resets the form (`RulesWindow.xaml` / `.xaml.cs`). `RuleRow` exposes `Kind`, `IdleMs`, `ThinkingMs` for the editor.
- **Install / session signal:** On DB init, `EnsureTrackerIdentity` sets `install_instance_id` (GUID), `first_run_utc` once, and updates `last_app_start_utc` + `app_version_last_run` each launch. `AppDatabase.GetTrackerReportInfo()` feeds a banner on the dashboard summary and a card in `HtmlReportExporter` (`TrackerReportInfo`, `AppendTrackerCard`). Replacing `activity.sqlite3` (import) yields a **new** id — parents can compare ids across exports.
- **Backup / import:** `BackupDatabaseToFile` uses SQLite `VACUUM INTO`. Settings has **Export database backup…** and **Import database backup…**. Import writes `pending_import.json`, releases the single-instance mutex, and `App.RestartForImport` + `Environment.Exit(0)`; the new process runs `--complete-db-import` before opening the DB (`CompletePendingDatabaseImportIfNeeded` in `App.xaml.cs`).
- **Uninstall data:** Checkbox + copy clarifies `%LocalAppData%\WhatAmIDoing\` retention; `installer/WhatAmIDoing.iss` comments document that user data is outside `{app}`. Setting key `keep_data_after_uninstall` stores the parent’s acknowledgement preference.

**Deferred:** emailed reports (explicitly later).

### Step 18b — Settings Save includes chart color
Main **Save** runs the same chart color commit as **Save color** when a category is filled (`TryCommitChartColorRow`), before other DB writes if hex is invalid; hint text in Settings explains this.

### Step 19 — Family installer UX (Inno) + bundled .NET Desktop Runtime
End-user setup is meant to feel like a normal Windows wizard (Dolphin-style runtime handling), not a developer publish folder.

- **`installer/legal/`** — `notice-before-install.txt` (`InfoBeforeFile`) and `terms-license.txt` (`LicenseFile`).
- **`installer/WhatAmIDoing.iss`** — `[Types]` Express vs Advanced; Advanced-only optional components **stricter idle** (45 s idle + 15 s Thinking written into bootstrap) and **screenshots on**; `CurStepChanged(ssPostInstall)` writes `%LocalAppData%\WhatAmIDoing\install-bootstrap.json` when needed; **`Services/InstallerBootstrap.cs`** applies it immediately after `Db.Initialize()` in `App.xaml.cs`, then deletes the file.
- **Self-contained Inno payload (≥1.0.2.4)** — `scripts/publish-installer.ps1` uses **`--self-contained true`** so the shipped EXE does not depend on a shared Microsoft Desktop Runtime install. Inno no longer bundles or runs `DesktopRuntime-8-x64.exe`; **`InfoAfterFile`** explains restart for auto-start / tray. CI drops fetch/verify prerequisite steps; workflow publish timeout **45 min**. **≥1.0.2.5:** **`EnableCompressionInSingleFile=false`** (avoids rare **0xc000041d** / KERNELBASE faults during single-file startup).
- **Bundled runtime (legacy &lt;1.0.2.4)** — `scripts/fetch-installer-prerequisites.ps1` (winget) dropped `installer/prereq/DesktopRuntime-8-x64.exe` (gitignored). Older Inno copied it to `{tmp}`; on **Ready**, if `8.x` was missing, **`NextButtonClick`** asked the user; **Yes** ran the bundle with **`/install /norestart`** (no `/quiet`) so Microsoft’s UI and UAC appeared. **`/quiet`** only when **`WizardSilent()`**.
- **Publish split (legacy)** — older `publish-installer.ps1` = framework-dependent single-file for Inno; `scripts/publish.ps1` = self-contained for portable/dev.

## Decisions locked

- Local-only data in v1; cloud sharing is a future roadmap item.
- Per-user install, no admin elevation.
- PIN gates **Settings** and **Rules** when set (session remembers after first success); **tray Exit** prompts for the PIN **every time** when set — **not** opening the dashboard, viewing charts, or exporting HTML. Sampling continues regardless.
- DPAPI CurrentUser for screen captures (only the same Windows user can decrypt).

## Open / future
- Per-rule weekly limits with soft alerts.
- Browser-tab URL ingestion (currently relies on title bar).
- Scene-aware screenshot interpretation (beyond OCR keywords).

---

*Product sections above are updated when behavior changes. Agent chat handoffs go in **Session log** at the top — append only.*
