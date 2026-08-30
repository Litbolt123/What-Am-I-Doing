# Dark mode — code guide (WPF / C#)

**Audience:** Agents and developers extending What Am I Doing UI.  
**Stack:** .NET 8 WPF, XAML resource dictionaries, SQLite setting `ui_dark_mode`.

Vault overview: Obsidian `Projects/What Am I Doing/Dark mode.md`.

---

## Compared to Local Music Hub (reference implementation)

Local Music Hub themes work reliably using **`HubTheme.cs`** (`GitHub projects\Local Music Hub\src\LocalMusicHub\Services\HubTheme.cs`):

| Hub | What Am I Doing (now) |
|-----|------------------------|
| `HubThemeLight.xaml` / `HubThemeDark.xaml` | `DashboardTheme.xaml` + `DashboardDarkMode.xaml` overlay |
| Swapped on **`Application.Current.Resources`** | **`DashTheme.Apply(AppDatabase)`** — same pattern |
| Never remove all theme dicts during swap | Same guard in `DashTheme.cs` |
| `{DynamicResource Hub*Brush}` in styles | `{DynamicResource Dash*Brush}` in shared styles |
| `HubTheme.Ensure(window)` | `DashTheme.Ensure(window)` |
| Save → `HubTheme.ApplyFromSettings()` | Save → `DashTheme.ApplyFromSettings()` + `RefreshOpenWindows()` |

Obsidian: [[Projects/Local Music Hub]] · Hub theme race lesson in that project's `context-summary`.

---

## What we built

Dark mode is **not** a separate app theme file per window. It is:

1. A **base light theme** (`Themes/DashboardTheme.xaml`) merged on every dashboard window.
2. An optional **overlay dictionary** (`Themes/DashboardDarkMode.xaml`) merged when `ui_dark_mode == "1"`.
3. **C# glue** in `Services/DashTheme.cs` (app-wide, like Local Music Hub `HubTheme.cs`) plus `AccessibilityUi.cs` for keyboard helpers.

User toggle: **Settings → Appearance → Dark mode** → Save.

---

## File map

| File | Language | Role |
|------|----------|------|
| `Themes/DashboardTheme.xaml` | XAML | Light palette, `Dash*Brush` keys, shared styles (`DashCardBorder`, `DashToolbarButton`, …) |
| `Themes/DashboardDarkMode.xaml` | XAML | Dark palette + overrides for styles that hardcode light colors |
| `Services/DashboardUi.cs` | C# | `EnsureTheme()` — inserts base theme before `InitializeComponent` |
| `Services/DashTheme.cs` | C# | **App-level** theme swap (Hub pattern); call `ApplyFromSettings` at startup and on Save |
| `Services/AccessibilityUi.cs` | C# | Keyboard helpers + visual-tree refresh after theme change |
| `Data/AppSettingsDefaults.cs` | C# | `("ui_dark_mode", "0")` default |
| `SettingsWindow.xaml(.cs)` | XAML + C# | `UiDarkModeBox` checkbox; save persists setting |

---

## Critical WPF rule (why “partly dark” happened)

### 1. Apply at **app startup**, then `Ensure` per window

```csharp
// App.xaml.cs — after Db.Initialize()
DashTheme.ApplyFromSettings(Db);

// MainWindow constructor
DashTheme.Ensure(this);
InitializeComponent();
```

### 2. Use `{DynamicResource}` in **styles**, not `{StaticResource}`

In `DashboardTheme.xaml`, card styles must bind brushes dynamically:

```xml
<Style x:Key="DashCardBorder" TargetType="Border">
    <Setter Property="Background" Value="{DynamicResource DashCardBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource DashBorderBrush}"/>
    ...
</Style>
```

**Why:** `DashCardBrush` is defined in the **same** dictionary as the style. `{StaticResource}` resolves once at load time to the **white** brush baked into the style object. Merging `DashboardDarkMode.xaml` later overrides the **key** but not the frozen setter value. `{DynamicResource}` re-resolves when the overlay merges.

Window backgrounds should also use `{DynamicResource DashBgBrush}` where possible.

---

## Merge order (`DashTheme.Apply`)

```csharp
// Base DashboardTheme.xaml always mounted on Application.Current
// Overlays removed and re-added:
// 1. Large text (optional)
// 2. High contrast — SKIPPED when dark mode is on
// 3. DashboardDarkMode.xaml when ui_dark_mode == "1"
```

When the user turns on dark mode and saves, **high contrast is cleared** in Settings so the two modes do not fight.

---

## Dark palette (XAML)

`DashboardDarkMode.xaml` overrides color keys and brushes:

```xml
<Color x:Key="DashBgColor">#141820</Color>
<Color x:Key="DashCardColor">#1E2430</Color>
<Color x:Key="DashBorderColor">#3A4150</Color>
<Color x:Key="DashTextPrimaryColor">#E8EAEF</Color>
<Color x:Key="DashTextMutedColor">#9AA3B2</Color>
<Color x:Key="DashAccentColor">#4A8AF4</Color>
<Color x:Key="DashMetricBgColor">#252B38</Color>

<SolidColorBrush x:Key="DashCardBrush" Color="{StaticResource DashCardColor}"/>
<!-- … other Dash*Brush keys … -->
```

It also redefines named styles with dark button templates, DataGrid line colors, and implicit `ScrollViewer` / `Border` styles so scroll areas are not white.

---

## Runtime toggle (Save in Settings)

```csharp
DashTheme.ApplyFromSettings(App.Db);
DashTheme.RefreshOpenWindows();
```

---

## Adding new UI — checklist

1. Use `{DynamicResource Dash*Brush}` or styles from `DashboardTheme.xaml` — never hardcode `#FFFFFF` / `#F4F7FB` in feature XAML.
2. Button hovers with hardcoded hex need matching overrides in **both** theme files (or move hover colors to brush keys).
3. New window: `DashTheme.Ensure(this);` then `InitializeComponent();` — do **not** merge theme dictionaries on each window.

4. ComboBox dropdown popups may still need full `ControlTemplate` work on Win11 (see vault `Atlas/Lessons` — WPF ComboBox stays light).
5. Charts: `ChartRenderer.cs` still uses `#EEF1F6` for empty segments — cosmetic gap until that reads a theme brush.

---

## Database

| Key | Default | Meaning |
|-----|---------|---------|
| `ui_dark_mode` | `0` | `1` = merge dark overlay |

---

## Verify

1. Settings → Dark mode → Save.
2. Dashboard cards (toolbar, catch-up, chart, summary) use dark gray `#1E2430`, not white.
3. Settings scroll area is dark, not a white sheet.
4. Toggle off → light mode returns without restart.

---

## Related: “Restart helper” warning

Separate from dark mode. **Settings → Restart if not running** registers a Windows scheduled task via `schtasks`. If registration fails (permissions, policy), Save now **turns the option off** and shows an informational message instead of leaving a broken enabled state.
