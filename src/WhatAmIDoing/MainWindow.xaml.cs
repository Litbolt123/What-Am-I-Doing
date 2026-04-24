using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WhatAmIDoing.Data;
using WhatAmIDoing.Export;
using WhatAmIDoing.Models;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class MainWindow
{
    private AggregatedReport? _currentReport;

    public MainWindow()
    {
        InitializeComponent();
        DayPicker.SelectedDate = DateTime.Today;
        Loaded += (_, _) => RefreshReport();
    }

    public void RefreshReport()
    {
        // ComboBox SelectionChanged can fire during InitializeComponent() before named fields below exist.
        if (SummaryText is null || CategoryGrid is null || ProcessGrid is null || DayPicker is null || RangeMode is null
            || YouTubeGrid is null || SiteGrid is null || ProjectGrid is null)
            return;

        var (startLocal, endLocal) = GetSelectedRange();
        var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

        var samples = App.Db.GetSamplesBetween(startUtc, endUtc);
        var intervalSec = Math.Max(1, App.Db.GetSampleIntervalMs() / 1000);
        var idleMin = App.Db.GetIdleThresholdMs() / 60000.0;
        // Pull the current rule set once and feed it to every aggregation so category/ignore
        // decisions always reflect "what my rules say right now", not "what they said last week".
        var rules = App.Db.GetRules();
        var report = ReportAggregator.Build(samples, intervalSec, startLocal, endLocal, rules);
        _currentReport = report;

        var voiceLine = "";
        if (report.CompanionAudioSeconds.Count > 0 || report.VoiceWhileGamingSeconds > 0)
        {
            var top = report.CompanionAudioSeconds
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => $"{kv.Key} ({Fmt(kv.Value)})");
            voiceLine = $"Voice / mic activity: {string.Join(", ", top)}\n";
            if (report.VoiceWhileGamingSeconds > 0)
                voiceLine += $"  …while gaming: {Fmt(report.VoiceWhileGamingSeconds)}\n";
        }

        var thinkMin = App.Db.GetThinkingExtraMs() / 60000.0;

        // Inline list of where you were on the web (same data as the Highlights “Sites / pages”
        // and “YouTube” tabs — surfaced here so the summary is self-contained).
        var webSummaryBlock = BuildWebContentSummaryText(report);

        // Time-on-computer cards: active + thinking + idle for today and for the trailing
        // 7 days ending on the selected day. "Ignored" rules (e.g. HWiNFO running in the
        // background) are reported separately so totals aren't inflated by monitoring utils.
        var today = DayPicker.SelectedDate ?? DateTime.Today;
        var (todayOn, _) = ComputeOnComputerSeconds(today, today.AddDays(1), intervalSec, rules);
        var (weekOn, _) = ComputeOnComputerSeconds(today.AddDays(-6), today.AddDays(1), intervalSec, rules);

        SummaryText.Text =
            $"On computer today ({today:MMM d}): {Fmt(todayOn)}\n" +
            $"On computer last 7 days: {Fmt(weekOn)}\n\n" +
            $"Active (typing / clicking): {Fmt(report.SecondsActiveFocused)}\n" +
            (report.SecondsThinking > 0 ? $"Thinking (reading / paused): {Fmt(report.SecondsThinking)}\n" : "") +
            $"Idle / AFK: {Fmt(report.SecondsIdle)}\n" +
            (report.SecondsIgnored > 0 ? $"Ignored (rules): {Fmt(report.SecondsIgnored)}\n" : "") +
            voiceLine +
            webSummaryBlock +
            $"\nSamples: {report.TotalSamples} · every {intervalSec}s" +
            $"\n\nTracking: Active \u2192 Thinking at {FormatMinutes(idleMin)} without input, Thinking \u2192 Idle after a further {FormatMinutes(thinkMin)}. Both are per-rule overridable in Rules (Cursor ships at 30s + 30s)." +
            "\nCategories reflect your current rules — adding or editing a rule updates past totals too.";

        var catRows = report.SecondsByCategory
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
            {
                var thinking = report.ThinkingSecondsByCategory.TryGetValue(kv.Key, out var th) ? th : 0;
                var active = Math.Max(0, kv.Value - thinking);
                var isIdle = string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase);
                return new CategoryRow(
                    kv.Key,
                    isIdle ? "" : Fmt(active),
                    thinking > 0 ? Fmt(thinking) : "",
                    Fmt(kv.Value));
            })
            .ToList();
        CategoryGrid.ItemsSource = catRows;

        var procKeys = new HashSet<string>(report.ActiveSecondsByProcess.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in report.ThinkingSecondsByProcess.Keys)
            procKeys.Add(k);

        var procRows = procKeys
            .Select(k =>
            {
                var active = report.ActiveSecondsByProcess.TryGetValue(k, out var a) ? a : 0;
                var thinking = report.ThinkingSecondsByProcess.TryGetValue(k, out var t) ? t : 0;
                return (Proc: k, Active: active, Thinking: thinking, Total: active + thinking);
            })
            .OrderByDescending(r => r.Total)
            .Take(40)
            .Select(r => new ProcessRow(
                r.Proc,
                Fmt(r.Active),
                r.Thinking > 0 ? Fmt(r.Thinking) : "",
                Fmt(r.Total)))
            .ToList();
        ProcessGrid.ItemsSource = procRows;

        YouTubeGrid.ItemsSource = TopHighlights(report.ActiveSecondsByYouTube);
        SiteGrid.ItemsSource = TopHighlights(report.ActiveSecondsBySite);
        ProjectGrid.ItemsSource = TopHighlights(report.ActiveSecondsByProject);

        UpdateChart();
        if (LegendPanel is not null)
            ChartRenderer.DrawCategoryLegend(LegendPanel, report);
    }

    private void UpdateChart()
    {
        if (ChartCanvas is null || _currentReport is null)
            return;

        if (_currentReport.DayCount > 1)
        {
            ChartTitle.Text =
                $"Activity by day — 7 days ending {_currentReport.RangeEndLocal.AddDays(-1):MMM d}";
            // 26 px per row + 6 px spacing + a little breathing room.
            ChartCanvas.Height = Math.Max(180, _currentReport.DayCount * 32 + 12);
            ChartRenderer.DrawDailyStackedBars(ChartCanvas, _currentReport);
        }
        else
        {
            ChartTitle.Text = $"Hourly activity — {_currentReport.RangeStartLocal:dddd, MMM d}";
            ChartCanvas.Height = 160;
            ChartRenderer.DrawHourlyTimeline(ChartCanvas, _currentReport);
        }
    }

    private void ChartCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateChart();

    private void CategoryGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_currentReport is null || CategoryGrid.SelectedItem is not CategoryRow row)
            return;
        var rows = _currentReport.DrillDownByCategory.TryGetValue(row.Category, out var list)
            ? list
            : Array.Empty<CategoryDrillRow>();
        var totalSec = _currentReport.SecondsByCategory.TryGetValue(row.Category, out var ts) ? ts : 0;
        var w = new CategoryDrillWindow(row.Category, rows, totalSec) { Owner = this };
        w.ShowDialog();
    }

    private static List<HighlightRow> TopHighlights(IReadOnlyDictionary<string, int> bucket) =>
        bucket
            .OrderByDescending(kv => kv.Value)
            .Take(15)
            .Select(kv => new HighlightRow(kv.Key, Fmt(kv.Value)))
            .ToList();

    /// <summary>
    /// Human-readable “where on the web” for the summary card. YouTube and non-YouTube
    /// browser page titles are tracked separately in the aggregator.
    /// </summary>
    private static string BuildWebContentSummaryText(AggregatedReport report)
    {
        var youTube = report.ActiveSecondsByYouTube
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .ToList();
        var sites = report.ActiveSecondsBySite
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .ToList();

        if (youTube.Count == 0 && sites.Count == 0)
        {
            return
                "\nWebsites & web content: nothing to list in this range. " +
                "We need a supported browser, a visible tab title, and engaged time (not fully idle) on that tab. " +
                "Use Highlights for the full tables when data exists.\n";
        }

        var lines = new List<string>
        {
            "",
            "Websites & web content (engaged time in this report — Active + Thinking):",
        };
        if (youTube.Count > 0)
        {
            lines.Add("  YouTube (video / channel in tab):");
            lines.AddRange(youTube.Select(kv => $"    • {TruncateForSummary(kv.Key)} — {Fmt(kv.Value)}"));
        }

        if (sites.Count > 0)
        {
            lines.Add("  Other pages & sites (from tab title):");
            lines.AddRange(sites.Select(kv => $"    • {TruncateForSummary(kv.Key)} — {Fmt(kv.Value)}"));
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string TruncateForSummary(string? s, int max = 64)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "(no title)";
        s = s.Trim();
        if (s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }

    private (DateTime StartLocal, DateTime EndLocal) GetSelectedRange()
    {
        var day = DayPicker.SelectedDate ?? DateTime.Today;
        var startLocal = day.Date;
        var endLocal = day.Date.AddDays(1);

        if (RangeMode.SelectedIndex == 1)
        {
            startLocal = day.Date.AddDays(-6);
            endLocal = day.Date.AddDays(1);
        }

        return (startLocal, endLocal);
    }

    private static string Fmt(int seconds)
    {
        if (seconds < 0)
            seconds = 0;
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1)
            return $"{t.Minutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }

    /// <summary>
    /// Returns (counted, ignored) seconds of "you were on the computer" for [startLocal, endLocal).
    /// counted = active + thinking + idle (non-ignored samples × interval).
    /// Idle is included because the user is still at the machine — just not inputting.
    /// Samples matched by an "Exclude from totals" rule (HWiNFO / ThrottleStop etc.) are returned
    /// separately so they don't inflate "time on computer".
    ///
    /// We re-classify each sample against the supplied rule set rather than trusting the
    /// per-row <c>ignored</c> column, so that adding a new "ignore HWiNFO" rule immediately
    /// drops those seconds from "on computer" totals — not only going forward.
    /// </summary>
    private static (int Counted, int Ignored) ComputeOnComputerSeconds(
        DateTime startLocal, DateTime endLocal, int intervalSec, IReadOnlyList<ClassificationRule> rules)
    {
        var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
        var samples = App.Db.GetSamplesBetween(startUtc, endUtc);

        var counted = 0;
        var ignored = 0;
        foreach (var s in samples)
        {
            var (_, ign) = CategoryClassifier.Classify(
                rules,
                s.ProcessName,
                s.WindowTitle ?? "",
                s.UserIdle,
                s.ContextValue,
                ContextKindExtensions.FromDbString(s.ContextKind));
            if (ign)
                ignored++;
            else
                counted++;
        }
        return (counted * intervalSec, ignored * intervalSec);
    }

    /// <summary>Renders minutes as "30s" when <1, else as "1.5 min" etc.</summary>
    private static string FormatMinutes(double minutes)
    {
        if (minutes <= 0)
            return "0 min";
        if (minutes < 1)
            return $"{Math.Round(minutes * 60)}s";
        return $"{minutes:0.##} min";
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshReport();

    private void DayPicker_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e) =>
        RefreshReport();

    private void RangeMode_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        RefreshReport();

    private void Settings_OnClick(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow { Owner = this };
        w.ShowDialog();
        RefreshReport();
    }

    private void Rules_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var w = new RulesWindow { Owner = this };
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            // If RulesWindow throws while opening or closing, the modal state can leave
            // the main window disabled. We log the failure so we can debug, but
            // deliberately don't show a blocking MessageBox — a modal error dialog on
            // the dashboard covers the charts and tables the user came here to read.
            // If it ever recurs, it will be visible in the crash log under "RulesWindow".
            CrashLogger.Log("RulesWindow", ex);
            IsEnabled = true;
        }
        finally
        {
            // Defense in depth: any modal-close path leaves this window enabled.
            IsEnabled = true;
            Activate();
        }

        RefreshReport();
    }

    private void Export_OnClick(object sender, RoutedEventArgs e)
    {
        var (startLocal, endLocal) = GetSelectedRange();
        var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
        var samples = App.Db.GetSamplesBetween(startUtc, endUtc);
        var intervalSec = Math.Max(1, App.Db.GetSampleIntervalMs() / 1000);
        var rules = App.Db.GetRules();
        var report = ReportAggregator.Build(samples, intervalSec, startLocal, endLocal, rules);
        var screenEvents = App.Db.GetScreenEventsBetween(startUtc, endUtc);

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "HTML report (*.html)|*.html",
            FileName = $"what-am-i-doing-{startLocal:yyyy-MM-dd}.html",
        };
        if (dlg.ShowDialog() != true)
            return;

        var title = RangeMode.SelectedIndex == 0
            ? $"What Am I Doing — {startLocal:yyyy-MM-dd}"
            : $"What Am I Doing — 7 days ending {endLocal.AddDays(-1):yyyy-MM-dd}";

        var includeEvidence = false;
        if (screenEvents.Count > 0)
        {
            var ans = System.Windows.MessageBox.Show(
                $"Include {Math.Min(12, screenEvents.Count)} thumbnail screenshots in the exported report?\n\n" +
                "They will be decrypted and saved to an 'evidence' folder next to the HTML file.",
                "Include screenshot evidence?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            includeEvidence = ans == MessageBoxResult.Yes;
        }

        try
        {
            HtmlReportExporter.WriteFile(dlg.FileName, report, title, screenEvents, includeEvidence);
            System.Windows.MessageBox.Show(
                $"Saved report:\n{dlg.FileName}",
                "What Am I Doing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Could not save the report:\n" + ex.Message,
                "What Am I Doing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private sealed record CategoryRow(string Category, string Active, string Thinking, string Time);

    private sealed record ProcessRow(string Process, string Active, string Thinking, string Time);

    private sealed record HighlightRow(string Label, string Time);
}
