# Lessons

What worked and what did not. **Read this before repeating a fix.** After a fix or a failed approach, append a class (not a chat transcript): Symptom · Failed · Worked · Verify · Do not regress.

Vault index: `C:\Users\Augus\OneDrive\Documents\Obsidian Vault\Atlas\Lessons.md`


## 2026-08-25 — WPF dashboard dark mode overlay

**Symptom:** User wants dark mode without rewriting every window.

**Failed:** Hardcoding dark colors per XAML file. Merging dark dict only on `Loaded`. `{StaticResource DashCardBrush}` inside `DashboardTheme` styles (freezes white brush). High contrast + dark mode together (HC paints ScrollViewer/Border white).

**Worked:** Overlay `DashboardDarkMode.xaml`; `MergeAppearanceResources` before `InitializeComponent`; `{DynamicResource}` for brush setters in shared styles; skip HC merge when dark on; full style overrides in dark dict. **Code guide:** `docs/dark-mode-guide.md`.

**Full design note for agents:** Obsidian `Projects/What Am I Doing/Dark mode.md` (vault). Repo copy of lesson only here; extend the vault note when the pattern changes.

**Do not regress:** New themed controls with hardcoded `#FFFFFF` / `#ECEFF4` hovers need overrides in both theme files or shared brush keys.


## 2026-08-26 — WPF ComboBox popup + split-button theme refresh

**Symptom:** Dark mode partly applied — Range dropdown white popup with light text (unreadable); Refresh split button shows light grey inner box on blue accent after theme toggle.

**Failed:** Dark overlay `Style TargetType="ComboBox"` with property setters only (Win11 default template still paints white popup). `RefreshThemedVisualTree` reassigning `Style` by heuristics (`GuessSplitOrPrimaryButton` swapped `DashSplitPrimaryLeft` → `DashPrimaryButton` / `DashToolbarButton` white hover).

**Worked:** Full `DashComboBox` `ControlTemplate` + implicit `ComboBoxItem` template in `DashboardTheme.xaml` (popup uses `{DynamicResource DashCardBrush}` / list hover brushes). Remove weak dark `ComboBox` override so base template survives. `RefreshThemedVisualTree` only calls `UpdateDefaultStyle()` — never replace explicit/named styles. Port pattern from Local Music Hub `HubComboBox`.

**Verify:** Settings → Appearance → Dark mode → Save. Main dashboard: open **Range** dropdown (dark card, readable text); hover **Refresh** left segment (dark blue hover, no grey box); open **▾** menu (dark context menu).

**Do not regress:** Do not add setter-only implicit `ComboBox` styles in overlay dicts — they replace the full template. Do not reassign `Button.Style` on theme refresh.


## 2026-08-26 — WPF DatePicker / Calendar popup (open — fix next time)

**Symptom:** Dark mode Day picker (`MainWindow` `DayPicker`): calendar icon stays Win11 light chrome; opened popup has invisible weekday row (S M T W T F S); adjacent-month days same brightness as current month; selected day light-blue box; date text in field can be unreadable.

**Failed:** (not attempted this session) Dark overlay `Style TargetType="DatePicker"` with Foreground/Background/BorderBrush setters only — does not theme `Calendar` popup or `PART_Button` glyph.

**Worked:** (TBD) Full `DatePicker` `ControlTemplate` + `Calendar` / `CalendarDayButton` / `CalendarItem` styles with `{DynamicResource Dash*Brush}` — same approach as `DashComboBox`. Hub has no reference; search WPF DatePicker dark templates or port from YouTube Downloader if present.

**Verify:** Dark mode → open Day calendar → weekday headers readable, muted other-month days, dark popup card, themed calendar button, date visible in field.

**Do not regress:** Do not ship setter-only `DatePicker` implicit style in overlay without a base template in `DashboardTheme.xaml`.


## 2026-08-30 — WPF editable ComboBox invisible text (dark mode)

**Symptom:** Rules → Add a rule → **Category** selector shows empty/black text in dark mode; dropdown list items readable. Same class affects any `IsEditable="True"` ComboBox.

**Failed:** Single `DashComboBox` template for all ComboBoxes — non-editable template has no `PART_EditableTextBox`, so typed/selected text uses default black foreground on dark metric background.

**Worked:** `DashEditableComboBoxTemplate` with `PART_EditableTextBox` (`Foreground`/`CaretBrush` from `TemplateBinding`, `Style="{x:Null}"` so implicit `TextBox` style does not fight the template). Implicit `ComboBox` style triggers `IsEditable=True` → editable template. Explicit `TextElement.Foreground` on non-editable `ContentPresenter`.

**Verify:** Dark mode → Rules → capture window → Category shows selected/typed text; also Settings chart category combo, Detection tune process combo, Website rule dialog category.

**Do not regress:** Any new `IsEditable="True"` ComboBox must rely on implicit trigger — do not assign only `DashComboBox` style without editable template.


## 2026-10-08 — Dashboard category edit + Rules recent-app picker

**Symptom:** User wants to rename/assign categories from dashboard tables and add rules from past processes without live capture.

**Worked:** Right-click **By category** → Rename (`CategoryMergeWindow` prefilled); **By app** → Add rule (Rules prefilled; uses selected category row if any). Category drill → add rule for app/title. Rules → **Pick from recent apps…** (`RecentProcessPickerWindow`, `GetRecentProcessSummariesBetween`). PIN: `TryEnsureEditUnlocked()` on dashboard edit paths.

**Verify:** No PIN → rename category + add rule from app row; Rules recent picker fills pattern from history.

**Do not regress:** Dashboard edits must gate on family PIN like Settings/Rules.


## 2026-10-08 — WPF CheckBox / TextBox / TabItem dark chrome (Rules window)

**Symptom:** Rules in dark mode — white Skip checkboxes, bright tab underline, Pattern field border invisible.

**Failed:** Setter-only `CheckBox`/`TextBox`/`TabItem` in `DashboardDarkMode.xaml` (Win11 default templates stay light).

**Worked:** Full `DashCheckBox`, `DashTextBox`, `DashTabItem`, `DashTabControl` / `DashFilterTabControl` templates in `DashboardTheme.xaml` with `{DynamicResource}`; `DataGridCheckBoxColumn.ElementStyle` for Skip column; removed dark overlay `TabItem` setter override that wiped template.

**Verify:** Dark mode → Rules → tabs muted with blue underline; Skip + Don't count checkboxes dark; Pattern field visible border.

**Do not regress:** Do not add setter-only implicit `TabItem`/`CheckBox` in overlay without template.


## 2026-10-08 — Rules add feedback + history capture pickers

**Symptom:** Adding a rule gave no confirmation; capture only supported live window, open windows, and recent apps.

**Worked:** Green `EditorFeedbackBanner` after add/save (`Added "Category" rule — Match: pattern (#id)`); `AddUserRule` returns `last_insert_rowid` and grid scrolls to new row. `ActivityHistoryPickerWindow` with modes: recent apps, window titles (`GetRecentWindowTitlesBetween`), pages/videos (`GetRecentContextValuesBetween`). Post-capture **exact app** quick-pick; **Use selected rule** copies template from grid.

**Verify:** Rules → pick from recent titles/pages → pattern prefilled; Add rule → green banner + row selected in table.

**Do not regress:** History pickers share `ProcessHistoryRangeHelper`; success banner uses `DashSuccess*` theme brushes (light + dark).


## 2026-08-24 — tray-first / not administrator

**Symptom:** First run looks like nothing happened.

**Failed:** Run as administrator; looking only at the taskbar.

**Worked:** Windows 11 tray **^** chevron. Data is `%LocalAppData%\WhatAmIDoing` (`activity.sqlite3`), not the Programs install folder.

## 2026-08-24 — CSV / JSON BOM

**Failed:** Reading Desktop WAID CSV/JSON as utf-8 and choking on BOM.

**Worked:** `utf-8-sig`. Do not copy the live DB or all-time JSON into the Obsidian vault; use `Projects/What Am I Doing/Data map.md`.


## 2026-10-19 — New built-ins on an existing DB, and today stats on a huge samples table

**Symptom:** Adding rows to `BuiltInDefaultRules` looks like it will never reach a database that already has rules. A "today by category" export that loads every sample stalls on `activity.sqlite3` (~450MB).

**Failed:** A second migration that updates or deletes existing rule rows (user-edited and older built-ins must stay). `SELECT` without a `ts_utc` range. `dotnet build` into `bin\Release` while `WhatAmIDoing.exe` is running (it locks that output).

**Worked:** `TopUpMissingBuiltInRules` already runs from `AppDatabase.Initialize` and inserts only when kind+pattern is missing. New suggestions go in `BuiltInDefaultRules` only. Rainmeter today totals use `FROM samples INDEXED BY idx_samples_ts` with local-today UTC bounds, `ignored = 0` and `user_idle = 0`. Write `rainmeter-status.json` via a temp file then `File.Replace`. Build with `-o artifacts\agent-build` and process priority BelowNormal.

**Verify:** `EXPLAIN QUERY PLAN` shows `SEARCH samples USING INDEX idx_samples_ts`. A local-today group-by finished in ~0.03s. `dotnet build -o artifacts\agent-build` exit 0. No test project in this repo.

**Do not regress:** Do not change category or priority of a row that already has the same kind+pattern (`obs64` stays Video Recording on this PC). Do not aggregate samples without the timestamp index. Do not restart the running tracker just to pick up rules.
