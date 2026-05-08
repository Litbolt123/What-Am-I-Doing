using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WhatAmIDoing.Models;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class RulesWindow
{
    private DispatcherTimer? _captureTimer;
    private long? _editingRuleId;

    /// <summary>
    /// Snapshot of the last successful capture. Held so the three quick-pick buttons
    /// (app name / window title / page or project) can fill different match types without
    /// re-running the capture countdown. Cleared when the window opens fresh.
    /// </summary>
    private sealed record CaptureSnapshot(
        string ProcessName,
        string WindowTitle,
        ContextKind ContextKind,
        string ContextValue);

    private CaptureSnapshot? _lastCapture;

    private sealed record RuleEditorBaseline(
        string Pattern,
        string Category,
        MatchKind Kind,
        string PriorityText,
        bool Ignore,
        string Idle,
        string Think,
        string Notes);

    private RuleEditorBaseline? _editorBaseline;

    public RulesWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AccessibilityUi.Apply(this, App.Db);
            Reload();
            _editorBaseline = CaptureEditorBaseline();
        };
        Closing += RulesWindow_OnClosing;
        // If the user closes the window mid-countdown (or while any other timer is
        // pending), tearing down the timer avoids a tick firing on disposed controls
        // and bubbling an InvalidOperationException up through the message pump, which
        // previously left the main window unable to reopen Rules.
        Closed += (_, _) =>
        {
            try
            {
                _captureTimer?.Stop();
            }
            catch
            {
            }
            _captureTimer = null;
        };
    }

    private void Reload()
    {
        try
        {
            var rules = App.Db.GetRules();
            var rows = rules.Select(r => new RuleRow(r)).ToList();

            // Reassigning ItemsSource on a DataGrid that currently has a selected row can
            // leave it in a half-rendered state where only the old selected row draws and
            // the rest look empty. Clearing the selection first avoids it.
            RulesGrid.SelectedItem = null;
            RulesGrid.ItemsSource = rows;

            StatusText.Text = rows.Count switch
            {
                0 => "No rules yet — click \"Restore suggested defaults…\" to seed the built-in list.",
                _ => $"{rows.Count} rules  ·  {rows.Count(r => r.SourceLabel == "Yours")} yours, {rows.Count(r => r.SourceLabel == "Suggested")} suggested",
            };

            // Offer existing category labels as quick-pick suggestions so users can reuse
            // a category across multiple apps or page patterns (e.g. one "SAT prep"
            // category covering Khan Academy context + a specific website).
            var current = CategoryBox.Text;
            var existing = rules
                .Where(r => !r.IgnoreInTotals)
                .Select(r => r.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
            CategoryBox.ItemsSource = existing;
            CategoryBox.Text = current;
        }
        catch (Exception ex)
        {
            // Don't let a rendering hiccup nuke the window — log it and show a status so
            // the user has *something* useful.
            CrashLogger.Log("RulesWindow.Reload", ex);
            StatusText.Text = $"Couldn't refresh: {ex.Message}";
        }
    }

    private void Add_OnClick(object sender, RoutedEventArgs e) =>
        TryCommitRuleEditor();

    /// <summary>Persists the editor to the database. Returns false if validation failed.</summary>
    private bool TryCommitRuleEditor()
    {
        var pattern = PatternBox.Text.Trim();
        if (pattern.Length == 0)
        {
            System.Windows.MessageBox.Show("Enter a pattern.", "Rules", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(PriorityBox.Text.Trim(), out var priority))
            priority = 0;

        var ignore = IgnoreBox.IsChecked == true;
        var category = ignore ? "Ignored" : EditableComboHelper.GetText(CategoryBox);
        if (!ignore && category.Length == 0)
        {
            System.Windows.MessageBox.Show("Enter a category label, or use “Exclude from totals”.", "Rules",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var kind = ReadMatchKind();

        if (!ignore && RulePriorityGuide.WarnIfSavingBelowBrowserBaseline(kind)
            && priority <= RulePriorityGuide.BuiltInBrowserProcessRulePriority)
        {
            var fix = System.Windows.MessageBox.Show(
                "Built-in rules tag browsers such as Chrome and Comet as \"Web browser\" at priority 190. " +
                "Your rule’s priority is not higher than that, so it may never apply.\n\n" +
                "Set priority to 200 and save?",
                "Rule priority",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (fix == MessageBoxResult.Cancel)
                return false;
            if (fix == MessageBoxResult.Yes)
                priority = RulePriorityGuide.RecommendedUserSiteRulePriority;
        }

        int? idleOverrideMs = null;
        var idleText = IdleOverrideBox.Text.Trim();
        if (idleText.Length > 0)
        {
            if (!double.TryParse(idleText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture, out var mins) || mins <= 0)
            {
                System.Windows.MessageBox.Show(
                    "Idle override must be a positive number of minutes (or leave blank to use the global default).",
                    "Rules", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            idleOverrideMs = (int)Math.Clamp(mins * 60_000, 15_000, 120 * 60_000);
        }

        int? thinkingOverrideMs = null;
        var thinkText = ThinkingOverrideBox.Text.Trim();
        if (thinkText.Length > 0)
        {
            if (!double.TryParse(thinkText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture, out var mins) || mins < 0)
            {
                System.Windows.MessageBox.Show(
                    "Thinking override must be a number of minutes (0 or more), or leave blank to use the global default.",
                    "Rules", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            thinkingOverrideMs = (int)Math.Clamp(mins * 60_000, 0, 60 * 60_000);
        }

        var notes = NotesBox.Text.Trim();

        if (_editingRuleId is long editId)
        {
            App.Db.UpdateUserRule(editId, kind, pattern, category, priority, ignore, idleOverrideMs, thinkingOverrideMs,
                notes.Length == 0 ? null : notes);
            ClearEditor();
        }
        else
        {
            App.Db.AddUserRule(kind, pattern, category, priority, ignore, idleOverrideMs, thinkingOverrideMs,
                notes.Length == 0 ? null : notes);
            PatternBox.Clear();
            IdleOverrideBox.Clear();
            ThinkingOverrideBox.Clear();
            NotesBox.Clear();
            _lastCapture = null;
            CapturePickRow.Visibility = Visibility.Collapsed;
            _editorBaseline = CaptureEditorBaseline();
        }

        Reload();
        return true;
    }

    private void RulesWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!IsEditorDirty())
            return;

        var r = System.Windows.MessageBox.Show(
            "You have unsaved changes in the rule editor. Save before closing?",
            "Classification rules",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (r == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (r == MessageBoxResult.No)
            return;

        if (!TryCommitRuleEditor())
            e.Cancel = true;
    }

    private RuleEditorBaseline CaptureEditorBaseline() =>
        new(
            PatternBox.Text.Trim(),
            IgnoreBox.IsChecked == true ? "" : EditableComboHelper.GetText(CategoryBox),
            ReadMatchKind(),
            PriorityBox.Text.Trim(),
            IgnoreBox.IsChecked == true,
            IdleOverrideBox.Text.Trim(),
            ThinkingOverrideBox.Text.Trim(),
            NotesBox.Text.Trim());

    private bool IsEditorDirty()
    {
        if (_editorBaseline is null)
            return false;
        var now = CaptureEditorBaseline();
        return !EditorBaselineEquals(now, _editorBaseline);
    }

    private static bool EditorBaselineEquals(RuleEditorBaseline a, RuleEditorBaseline b) =>
        a.Pattern == b.Pattern
        && string.Equals(a.Category, b.Category, StringComparison.Ordinal)
        && a.Kind == b.Kind
        && a.PriorityText == b.PriorityText
        && a.Ignore == b.Ignore
        && a.Idle == b.Idle
        && a.Think == b.Think
        && a.Notes == b.Notes;

    private void EditSelected_OnClick(object sender, RoutedEventArgs e) => BeginEditSelectedRow();

    private void RulesGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        BeginEditSelectedRow();

    private void BeginEditSelectedRow()
    {
        if (RulesGrid.SelectedItem is not RuleRow row)
        {
            System.Windows.MessageBox.Show("Select a rule to edit.", "Rules", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        LoadRowIntoEditor(row);
    }

    private void LoadRowIntoEditor(RuleRow row)
    {
        _editingRuleId = row.Id;
        EditorTitleText.Text = $"Edit rule #{row.Id}";
        AddRuleBtn.Content = "Save changes";
        CancelEditBtn.Visibility = Visibility.Visible;

        SelectMatchKind(row.Kind);
        PatternBox.Text = row.Pattern;
        IgnoreBox.IsChecked = row.IgnoreInTotals;
        CategoryRow.Visibility = row.IgnoreInTotals ? Visibility.Collapsed : Visibility.Visible;
        CategoryBox.Text = row.IgnoreInTotals ? "" : row.Category;
        PriorityBox.Text = row.Priority.ToString(CultureInfo.CurrentCulture);

        IdleOverrideBox.Text = row.IdleMs is int ims
            ? (ims / 60000.0).ToString("0.###", CultureInfo.CurrentCulture)
            : "";
        ThinkingOverrideBox.Text = row.ThinkingMs is int tms
            ? (tms / 60000.0).ToString("0.###", CultureInfo.CurrentCulture)
            : "";
        NotesBox.Text = row.Notes ?? "";

        RefreshPatternHint();

        _lastCapture = null;
        CapturePickRow.Visibility = Visibility.Collapsed;

        _editorBaseline = CaptureEditorBaseline();
    }

    private void CancelEdit_OnClick(object sender, RoutedEventArgs e) => ClearEditor();

    private void ClearEditor()
    {
        _editingRuleId = null;
        EditorTitleText.Text = "Add a rule";
        AddRuleBtn.Content = "Add rule";
        CancelEditBtn.Visibility = Visibility.Collapsed;
        PatternBox.Clear();
        IdleOverrideBox.Clear();
        ThinkingOverrideBox.Clear();
        NotesBox.Clear();
        IgnoreBox.IsChecked = false;
        CategoryRow.Visibility = Visibility.Visible;
        SelectMatchKind(MatchKind.ProcessNameContains);
        PriorityBox.Text = RulePriorityGuide.DefaultNewProcessRulePriority.ToString(CultureInfo.CurrentCulture);

        RefreshPatternHint();
        _editorBaseline = CaptureEditorBaseline();
    }

    private void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not RuleRow row)
        {
            System.Windows.MessageBox.Show("Select a rule to delete.", "Rules", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_editingRuleId == row.Id)
            ClearEditor();

        App.Db.DeleteRule(row.Id);
        Reload();
    }

    private void IgnoreBox_OnChecked(object sender, RoutedEventArgs e)
    {
        CategoryRow.Visibility = IgnoreBox.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }

    private void WebsiteRule_OnClick(object sender, RoutedEventArgs e)
    {
        var suggestions = App.Db.GetRules()
            .Where(r => !r.IgnoreInTotals)
            .Select(r => r.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dlg = new WebsiteRuleDialog(suggestions) { Owner = this };
        if (dlg.ShowDialog() == true)
            Reload();
    }

    private void RestoreDefaults_OnClick(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            "This removes every rule and replaces them with the built-in suggested list (browsers, development tools, YouTube in the window title, communication apps, etc.).\n\nContinue?",
            "Restore suggested defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
            return;

        ClearEditor();
        App.Db.RestoreBuiltInDefaultRules();
        Reload();
    }

    private void MatchKindBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshPatternHint();
        ApplyRecommendedPriorityForNewRule();
    }

    /// <summary>
    /// When adding (not editing) a rule, pick a priority that beats generic browser rules for site/title matches.
    /// </summary>
    private void ApplyRecommendedPriorityForNewRule()
    {
        // ComboBox fires SelectionChanged during InitializeComponent before controls declared later
        // in XAML (e.g. PriorityBox inside Advanced) are assigned — guard to avoid NRE on open.
        if (PriorityBox is null)
            return;
        if (_editingRuleId is not null)
            return;
        var kind = ReadMatchKind();
        if (RulePriorityGuide.NeedsPriorityAboveBrowsers(kind))
            PriorityBox.Text = RulePriorityGuide.RecommendedUserSiteRulePriority.ToString(CultureInfo.CurrentCulture);
        else
            PriorityBox.Text = RulePriorityGuide.DefaultNewProcessRulePriority.ToString(CultureInfo.CurrentCulture);
    }

    private void RefreshPatternHint()
    {
        // Keep the small grey hint under the Pattern field in sync with the match type so
        // the user knows what shape of input to type.
        if (PatternHint is null)
            return;
        PatternHint.Text = ReadMatchKind() switch
        {
            MatchKind.ProcessNameEquals    => "Pattern: exact .exe-style process name, e.g. 'explorer' or 'Cursor'",
            MatchKind.ProcessNameContains  => "Pattern: part of the process name, e.g. 'comet', 'Cursor', 'Discord'",
            MatchKind.WindowTitleContains  => "Pattern: text that appears in the window title, e.g. 'YouTube', 'Khan Academy'",
            MatchKind.ContextValueContains => "Pattern: text in any extracted page / YouTube / project (broadest)",
            MatchKind.ContextSiteContains => "Pattern: text in a normal browser *page* title (not YouTube- or project-specific rows below).",
            MatchKind.ContextYouTubeVideoContains => "Pattern: text in the *video title* (only when a tab is classified as YouTube, e.g. a documentary on YouTube)",
            MatchKind.ContextProjectContains => "Pattern: the IDE’s project or folder name we extracted (Cursor/VS…)",
            _ => "",
        };
    }

    private void Capture_OnClick(object sender, RoutedEventArgs e)
    {
        // 3-second countdown so the user has time to alt-tab to the target app.
        // We disable the button and the Add Rule flow while the countdown runs so there's
        // no ambiguity about what we're capturing.
        if (_captureTimer is not null)
            return;

        var remaining = 3;
        CaptureBtn.IsEnabled = false;
        CancelCaptureBtn.Visibility = Visibility.Visible;
        CaptureHint.Foreground = System.Windows.Media.Brushes.DarkOrange;
        CaptureHint.Text = $"Switch to the app now… capturing in {remaining}s";

        _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _captureTimer.Tick += (_, _) =>
        {
            // If the window has already been closed (timer outlived the window), bail out
            // quietly. Without this, touching CaptureHint / PatternBox below throws and
            // poisons the main window's ability to reopen the dialog.
            if (!IsLoaded)
            {
                _captureTimer?.Stop();
                _captureTimer = null;
                return;
            }

            remaining--;
            if (remaining > 0)
            {
                CaptureHint.Text = $"Switch to the app now… capturing in {remaining}s";
                return;
            }

            _captureTimer!.Stop();
            _captureTimer = null;
            CaptureBtn.IsEnabled = true;
            CancelCaptureBtn.Visibility = Visibility.Collapsed;

            var fg = ForegroundWindowHelper.TryGetForeground();
            if (fg is null || string.IsNullOrWhiteSpace(fg.Value.ProcessName))
            {
                CaptureHint.Foreground = System.Windows.Media.Brushes.Firebrick;
                CaptureHint.Text = "Couldn't read the foreground window. Try again.";
                return;
            }

            // If the user never alt-tabbed, the countdown captures WhatAmIDoing itself —
            // which would write a self-rule for the tracker. Warn instead of pre-filling.
            if (fg.Value.ProcessName.Equals("WhatAmIDoing", StringComparison.OrdinalIgnoreCase))
            {
                CaptureHint.Foreground = System.Windows.Media.Brushes.Firebrick;
                CaptureHint.Text = "Captured this app instead of the target — click the button, then alt-tab to the app you want a rule for before the countdown ends.";
                return;
            }

            PrefillFromForeground(fg.Value);
        };
        _captureTimer.Start();
    }

    /// <summary>
    /// User hit "Cancel" during the countdown. Stops the timer and resets the UI without
    /// capturing anything. Safe to call when there's no timer active.
    /// </summary>
    private void CancelCapture_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _captureTimer?.Stop();
        }
        catch
        {
        }
        _captureTimer = null;
        CaptureBtn.IsEnabled = true;
        CancelCaptureBtn.Visibility = Visibility.Collapsed;
        CaptureHint.Foreground = System.Windows.Media.Brushes.DimGray;
        CaptureHint.Text = "Capture cancelled.";
    }

    /// <summary>
    /// After a successful capture we store the whole snapshot (process, title, extracted
    /// context) so the user can choose which field the rule should match. The three buttons
    /// below all read from <see cref="_lastCapture"/>.
    /// </summary>
    private void PrefillFromForeground(ForegroundWindowInfo fg)
    {
        var ctx = TitleContextExtractor.Extract(fg.ProcessName, fg.WindowTitle);
        _lastCapture = new CaptureSnapshot(fg.ProcessName, fg.WindowTitle ?? "", ctx.Kind, ctx.Value);

        // Default to "App name contains" because it tolerates Chromium-style companion
        // processes (chrome.exe, chrome_proxy.exe, msedgewebview2.exe, etc.). The pick-row
        // below lets the user switch to title / page matching with one click.
        ApplyMatchChoice(MatchKind.ProcessNameContains, fg.ProcessName);

        CaptureHint.Foreground = System.Windows.Media.Brushes.ForestGreen;
        var titleSnippet = string.IsNullOrWhiteSpace(fg.WindowTitle)
            ? "(no window title)"
            : (fg.WindowTitle!.Length > 60 ? fg.WindowTitle[..60] + "…" : fg.WindowTitle);
        CaptureHint.Text = $"Captured: {fg.ProcessName} — {titleSnippet}";

        // Populate the three quick-pick buttons with label text drawn from the capture.
        PickAppBtn.Content = $"Match app: “{fg.ProcessName}”";
        PickAppBtn.ToolTip = "Any window whose process name contains this text counts. Good for most apps.";

        var titleForButton = Ellipsize(fg.WindowTitle, 42);
        if (string.IsNullOrWhiteSpace(titleForButton))
        {
            PickTitleBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            PickTitleBtn.Visibility = Visibility.Visible;
            PickTitleBtn.Content = $"Match title contains: “{titleForButton}”";
            PickTitleBtn.ToolTip = "Matches whenever the window title contains this text. Good for websites that always keep the site name in the tab.";
        }

        if (ctx.Kind != ContextKind.None && !string.IsNullOrWhiteSpace(ctx.Value))
        {
            var ctxLabel = ctx.Kind switch
            {
                ContextKind.Site => "page",
                ContextKind.YouTube => "YouTube video",
                ContextKind.Project => "project",
                _ => "page",
            };
            PickContextBtn.Visibility = Visibility.Visible;
            PickContextBtn.Content = $"Match {ctxLabel}: “{Ellipsize(ctx.Value, 42)}”";
            PickContextBtn.ToolTip = "Matches only when you're on this specific page / video / project, regardless of which browser or IDE it opens in.";
        }
        else
        {
            PickContextBtn.Visibility = Visibility.Collapsed;
        }

        CapturePickRow.Visibility = Visibility.Visible;
    }

    private void PickApp_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapture is null)
            return;
        ApplyMatchChoice(MatchKind.ProcessNameContains, _lastCapture.ProcessName);
    }

    private void PickTitle_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapture is null || string.IsNullOrWhiteSpace(_lastCapture.WindowTitle))
            return;

        // Use a shortened, meaningful fragment instead of the full window title, which
        // often contains per-tab junk (timestamps, notification counts) that would make
        // the rule fail to re-match. For browsers the context extractor already gave us
        // the "clean" page title — prefer that if present, even though we're in title mode.
        var candidate = !string.IsNullOrWhiteSpace(_lastCapture.ContextValue)
            ? _lastCapture.ContextValue
            : _lastCapture.WindowTitle;
        // Trim to ~60 chars so the pattern is tight enough to match tomorrow too.
        if (candidate.Length > 60)
            candidate = candidate[..60].TrimEnd();
        ApplyMatchChoice(MatchKind.WindowTitleContains, candidate);
    }

    private void PickContext_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastCapture is null || string.IsNullOrWhiteSpace(_lastCapture.ContextValue))
            return;

        // Pick the *narrowest* correct match kind so a browser capture doesn’t get stuck
        // on the generic “any context” row.
        var kind = _lastCapture.ContextKind switch
        {
            ContextKind.Site => MatchKind.ContextSiteContains,
            ContextKind.YouTube => MatchKind.ContextYouTubeVideoContains,
            ContextKind.Project => MatchKind.ContextProjectContains,
            _ => MatchKind.ContextValueContains,
        };
        ApplyMatchChoice(kind, _lastCapture.ContextValue);
    }

    private void ApplyMatchChoice(MatchKind kind, string pattern)
    {
        SelectMatchKind(kind);
        PatternBox.Text = pattern;
    }

    private static string Ellipsize(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    private MatchKind ReadMatchKind()
    {
        if (MatchKindBox.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && Enum.TryParse<MatchKind>(tag, out var parsed))
            return parsed;
        // Reasonable fallback: "App name contains" behaves well for most users.
        return MatchKind.ProcessNameContains;
    }

    private void SelectMatchKind(MatchKind kind)
    {
        foreach (var obj in MatchKindBox.Items)
        {
            if (obj is ComboBoxItem item && item.Tag is string tag
                && Enum.TryParse<MatchKind>(tag, out var parsed) && parsed == kind)
            {
                MatchKindBox.SelectedItem = item;
                return;
            }
        }
    }
}

internal sealed class RuleRow
{
    public RuleRow(ClassificationRule r)
    {
        Id = r.Id;
        Pattern = r.Pattern;
        Category = r.Category;
        Priority = r.Priority;
        IgnoreInTotals = r.IgnoreInTotals;
        MatchLabel = r.MatchKind switch
        {
            MatchKind.ProcessNameEquals    => "App name is",
            MatchKind.ProcessNameContains  => "App contains",
            MatchKind.WindowTitleContains  => "Title contains",
            MatchKind.ContextValueContains => "Any page/video/project",
            MatchKind.ContextSiteContains => "Page (site)",
            MatchKind.ContextYouTubeVideoContains => "YouTube video",
            MatchKind.ContextProjectContains => "Project (IDE)",
            _ => "?",
        };
        SourceLabel = r.IsBuiltIn ? "Suggested" : "Yours";
        IdleOverrideLabel = r.IdleThresholdMsOverride is int ms
            ? (ms / 60000.0).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)
            : "";
        ThinkingOverrideLabel = r.ThinkingExtraMsOverride is int tms
            ? (tms / 60000.0).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)
            : "";
        Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes;
        // A small glyph in the grid column signals "hover me for the note". Empty string
        // when there's no note so the column stays visually quiet for the common case.
        NotesGlyph = Notes is null ? "" : "✎";
        Kind = r.MatchKind;
        IdleMs = r.IdleThresholdMsOverride;
        ThinkingMs = r.ThinkingExtraMsOverride;
    }

    public long Id { get; }
    public MatchKind Kind { get; }
    /// <summary>Per-rule idle override in ms, for the editor.</summary>
    public int? IdleMs { get; }
    /// <summary>Per-rule thinking override in ms, for the editor.</summary>
    public int? ThinkingMs { get; }
    public string MatchLabel { get; }
    public string Pattern { get; }
    public string Category { get; }
    public int Priority { get; }
    public bool IgnoreInTotals { get; }
    public string SourceLabel { get; }

    /// <summary>Shown in the grid column; blank means the rule uses the global idle threshold.</summary>
    public string IdleOverrideLabel { get; }

    /// <summary>Shown in the grid column; blank means the rule uses the global thinking grace.</summary>
    public string ThinkingOverrideLabel { get; }

    /// <summary>Full note text — surfaced as the tooltip for the Note column cell.</summary>
    public string? Notes { get; }

    /// <summary>Visual marker (empty or ✎) shown inside the grid cell.</summary>
    public string NotesGlyph { get; }
}
