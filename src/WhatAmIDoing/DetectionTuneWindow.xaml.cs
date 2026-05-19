using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WhatAmIDoing.Data;
using WhatAmIDoing.Models;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class DetectionTuneWindow
{
    private TuneAnalysisResult? _lastResult;

    public DetectionTuneWindow()
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AccessibilityUi.Apply(this, App.Db);
            UpdateCustomRangeVisibility();
            ReloadProcessList();
            UpdatePriorHint();
        };
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void RangeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // RangeCombo gets a default selection during InitializeComponent before later controls
        // (e.g. ProcessCombo) exist — do not touch ProcessCombo until the window has loaded.
        if (!IsLoaded || ProcessCombo is null)
            return;
        UpdateCustomRangeVisibility();
        ReloadProcessList();
    }

    private void UpdateCustomRangeVisibility()
    {
        if (CustomRangePanel is null || RangeCombo is null)
            return;
        CustomRangePanel.Visibility = IsCustomRangeSelected() ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool IsCustomRangeSelected() =>
        RangeCombo.SelectedItem is ComboBoxItem c && c.Tag is string t && t == "custom";

    private void CustomUnitCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ProcessCombo is null || !IsCustomRangeSelected())
            return;
        ReloadProcessList();
    }

    private void CustomRangeFields_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || ProcessCombo is null || !IsCustomRangeSelected())
            return;
        ReloadProcessList();
    }

    /// <summary>Parses the custom duration row. Clamps to 1 minute … 48 hours.</summary>
    private bool TryGetCustomSpan(out TimeSpan span)
    {
        span = TimeSpan.Zero;
        if (CustomDurationBox is null || CustomUnitCombo is null)
            return false;
        var text = CustomDurationBox.Text.Trim();
        if (text.Length == 0)
            return false;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var n)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out n))
            return false;
        if (n <= 0 || double.IsNaN(n) || double.IsInfinity(n))
            return false;
        var useHours = CustomUnitCombo.SelectedIndex == 1;
        span = useHours ? TimeSpan.FromHours(n) : TimeSpan.FromMinutes(n);
        if (span < TimeSpan.FromMinutes(1))
            span = TimeSpan.FromMinutes(1);
        if (span > TimeSpan.FromHours(48))
            span = TimeSpan.FromHours(48);
        return true;
    }

    /// <summary>
    /// Editable ComboBox often leaves <see cref="P:System.Windows.Controls.ComboBox.Text"/> stale until LostFocus; read the template
    /// text box like <see cref="EditableComboHelper"/> (same fix as Rules website capture).
    /// </summary>
    private string GetProcessNameInput()
    {
        if (ProcessCombo is null)
            return "";
        var t = EditableComboHelper.GetText(ProcessCombo);
        if (t.Length > 0)
            return t;
        return (ProcessCombo.SelectedItem as string)?.Trim() ?? "";
    }

    private void ReloadProcessList()
    {
        if (ProcessCombo is null)
            return;
        var (start, end) = GetRangeUtc();
        var names = App.Db.GetDistinctProcessNamesBetween(start, end);
        var sel = GetProcessNameInput();
        ProcessCombo.Items.Clear();
        foreach (var n in names)
            ProcessCombo.Items.Add(n);
        if (!string.IsNullOrEmpty(sel))
        {
            var idx = names.FindIndex(x => x.Equals(sel, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                ProcessCombo.SelectedIndex = idx;
            else
                ProcessCombo.Text = sel;
        }
    }

    private (DateTime StartUtc, DateTime EndUtc) GetRangeUtc()
    {
        var now = DateTime.UtcNow;
        if (RangeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            return (now.AddHours(-2), now);

        var localNow = DateTime.Now;
        return tag switch
        {
            "2h" => (now.AddHours(-2), now),
            "6h" => (now.AddHours(-6), now),
            "today" =>
            (
                DateTime.SpecifyKind(localNow.Date, DateTimeKind.Local).ToUniversalTime(),
                now
            ),
            "yesterday" =>
            (
                DateTime.SpecifyKind(localNow.Date.AddDays(-1), DateTimeKind.Local).ToUniversalTime(),
                DateTime.SpecifyKind(localNow.Date, DateTimeKind.Local).ToUniversalTime()
            ),
            "custom" => TryGetCustomSpan(out var span)
                ? (now - span, now)
                : (now.AddMinutes(-30), now),
            _ => (now.AddHours(-2), now),
        };
    }

    private TuneIntent GetIntent()
    {
        if (IntentCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            return TuneIntent.Gaming;
        return tag switch
        {
            "Video" => TuneIntent.Video,
            "Reading" => TuneIntent.Reading,
            "Other" => TuneIntent.Other,
            _ => TuneIntent.Gaming,
        };
    }

    private void ProcessCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        UpdatePriorHint();
    }

    private void UpdatePriorHint()
    {
        if (PriorHintText is null || ProcessCombo is null)
            return;
        var proc = GetProcessNameInput();
        if (string.IsNullOrWhiteSpace(proc))
        {
            PriorHintText.Text = "";
            return;
        }

        var hint = TuningHintsStore.LastForProcess(proc);
        PriorHintText.Text = hint is null
            ? ""
            : $"Last tune-up for “{hint.ProcessName}”: intent {hint.Intent}, suggested idle {hint.SuggestedIdleMs / 60000.0:0.#} min — {hint.UpdatedUtc.ToLocalTime():g}";
    }

    private void Analyze_OnClick(object sender, RoutedEventArgs e)
    {
        var proc = GetProcessNameInput();
        if (string.IsNullOrWhiteSpace(proc))
        {
            System.Windows.MessageBox.Show("Choose or type a process name (e.g. Dolphin, chrome).", "Tune detection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsCustomRangeSelected() && !TryGetCustomSpan(out _))
        {
            System.Windows.MessageBox.Show(
                "For a custom time range, enter a positive number (e.g. 19 for a 19-minute video), then choose minutes or hours.",
                "Tune detection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var (start, end) = GetRangeUtc();
        var samples = App.Db.GetSamplesBetween(start, end);
        var intent = GetIntent();
        var globalIdle = App.Db.GetIdleThresholdMs();
        _lastResult = DetectionTuneAnalyzer.Analyze(samples, proc, intent, globalIdle);

        var text = _lastResult.SummaryLines;
        if (_lastResult.SuggestedIdleOverrideMs is int sid)
        {
            text += $"\r\nSuggested idle override: {sid / 60000.0:0.#} min (you can edit below).\r\n";
            SuggestedIdleBox.Text = (sid / 60000.0).ToString("0.###", CultureInfo.CurrentCulture);
        }
        else
        {
            SuggestedIdleBox.Text = "";
        }

        AnalysisBox.Text = text;
        ExtraRecoText.Text = _lastResult.ExtraRecommendation ?? "";

        UpdatePriorHint();
    }

    private void ApplyRule_OnClick(object sender, RoutedEventArgs e)
    {
        var proc = GetProcessNameInput();
        if (string.IsNullOrWhiteSpace(proc))
        {
            System.Windows.MessageBox.Show("Choose or type a process name.", "Tune detection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var cat = CategoryBox.Text.Trim();
        if (cat.Length == 0)
        {
            System.Windows.MessageBox.Show("Enter a category label for this rule.", "Tune detection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int? idleOv = null;
        var idleText = SuggestedIdleBox.Text.Trim();
        if (idleText.Length > 0)
        {
            if (!double.TryParse(idleText, NumberStyles.Float, CultureInfo.CurrentCulture, out var mins)
                && !double.TryParse(idleText, NumberStyles.Float, CultureInfo.InvariantCulture, out mins))
            {
                System.Windows.MessageBox.Show("Enter minutes for idle override, or leave blank.", "Tune detection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            idleOv = (int)Math.Clamp(mins * 60_000, 15_000, 120 * 60_000);
        }

        if (idleOv is null && _lastResult?.SuggestedIdleOverrideMs is int fallback)
            idleOv = fallback;

        if (idleOv is null)
        {
            System.Windows.MessageBox.Show(
                "Run Analyze first, or enter an idle override in minutes. Nothing to apply.",
                "Tune detection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var rules = App.Db.GetRules();
        var existing = rules.FirstOrDefault(r =>
            r.MatchKind == MatchKind.ProcessNameEquals
            && r.Pattern.Equals(proc, StringComparison.OrdinalIgnoreCase));

        var intentStr = (IntentCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Gaming";
        var note = $"Tune detection ({intentStr}, {DateTime.Now:g})";

        if (existing is not null)
        {
            App.Db.UpdateUserRule(existing.Id, MatchKind.ProcessNameEquals, proc, cat, existing.Priority,
                existing.IgnoreInTotals, idleOv, existing.ThinkingExtraMsOverride,
                string.IsNullOrEmpty(existing.Notes) ? note : existing.Notes + " · " + note);
        }
        else
        {
            App.Db.AddUserRule(MatchKind.ProcessNameEquals, proc, cat, RulePriorityGuide.DefaultNewProcessRulePriority,
                false, idleOv, null, note);
        }

        TuningHintsStore.Upsert(new TuningHintRecord
        {
            ProcessName = proc,
            Intent = intentStr,
            SuggestedIdleMs = idleOv.Value,
            UpdatedUtc = DateTime.UtcNow,
        });

        System.Windows.MessageBox.Show(
            $"Saved rule for process “{proc}” with idle override {idleOv.Value / 60000.0:0.#} min.",
            "Tune detection",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        DialogResult = true;
    }
}
