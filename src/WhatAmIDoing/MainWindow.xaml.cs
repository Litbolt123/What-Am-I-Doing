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
        Loaded += (_, _) =>
        {
            ApplyAccessibilityFromSettings();
            UpdateChartScrollMaxHeight();
            RefreshReport();
            MaybeShowFirstRunChecklist();
        };
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged)
            UpdateChartScrollMaxHeight();
    }

    /// <summary>
    /// Keeps the stacked-day chart from consuming the whole window on short screens; inner scroll still shows full chart.
    /// Grows when the window is taller so wide monitors use space well.
    /// </summary>
    private void UpdateChartScrollMaxHeight()
    {
        if (ChartAreaScrollViewer is null)
            return;
        var h = ActualHeight;
        if (h < 120 || double.IsNaN(h))
            return;
        // Title + toolbar + window chrome + chart header/legend + outer margins (approximate).
        const double reserved = 340;
        var cap = h - reserved;
        ChartAreaScrollViewer.MaxHeight = Math.Clamp(cap, 160, 560);
    }

    public void ApplyAccessibilityFromSettings() =>
        AccessibilityUi.Apply(this, App.Db);

    private void MaybeShowFirstRunChecklist()
    {
        if (App.Db.GetSetting("first_run_checklist_dismissed") == "1")
            return;
        try
        {
            var w = new FirstRunChecklistWindow { Owner = this };
            w.ShowDialog();
        }
        catch
        {
            /* non-fatal */
        }
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

        var tracker = App.Db.GetTrackerReportInfo();
        var trackerBlock =
            $"This install: id {tracker.InstanceIdShort} (app {tracker.AppVersion}) — first run {tracker.FirstRunLocal}, this session {tracker.ThisSessionStartLocal}\n" +
            $"Data folder: {tracker.DataFolderHint}\n" +
            "If a report is missing this line, the app may not have run. A new id after reinstall means a new database on this PC.\n\n";

        var lifecycleBlock = BuildLifecycleSummaryText(startUtc, endUtc);
        // and “YouTube” tabs — surfaced here so the summary is self-contained).
        var webSummaryBlock = BuildWebContentSummaryText(report);

        var daySpan = Math.Max(1, report.DayCount);
        var (rangeOnComputer, _) = ComputeOnComputerSeconds(startLocal, endLocal, intervalSec, rules);

        var ignoredPctLine = "";
        if (report.SecondsIgnored > 0 && report.SecondsTotalTracked > 0)
        {
            var pct = 100.0 * report.SecondsIgnored / report.SecondsTotalTracked;
            ignoredPctLine = $"Ignored (rules): {Fmt(report.SecondsIgnored)} (~{pct:0.#}% of tracked clock time)\n";
        }
        else if (report.SecondsIgnored > 0)
            ignoredPctLine = $"Ignored (rules): {Fmt(report.SecondsIgnored)}\n";

        var quietLine = "";
        if (App.Db.GetSetting("quiet_hours_enabled") == "1")
        {
            _ = int.TryParse(App.Db.GetSetting("quiet_start_hour"), out var qhS);
            _ = int.TryParse(App.Db.GetSetting("quiet_end_hour"), out var qhE);
            qhS = Math.Clamp(qhS, 0, 23);
            qhE = Math.Clamp(qhE, 0, 23);
            var qSec = 0;
            foreach (var s in samples)
            {
                var local = s.TsUtc.ToLocalTime();
                if (!QuietHoursHelper.IsQuietHour(local.Hour, qhS, qhE))
                    continue;
                var st = ActivityStateExtensions.FromDbString(s.ActivityState);
                if (st is ActivityState.Active or ActivityState.Thinking)
                    qSec += intervalSec;
            }

            quietLine =
                $"Engaged time during quiet hours ({qhS}:00–{qhE}:00 local clock, approximate): {Fmt(qSec)}\n";
        }

        var compareBlock = "";
        if (daySpan >= 1)
        {
            var prevStartLocal = startLocal.AddDays(-daySpan);
            var prevEndLocal = startLocal;
            var prevStartUtc = DateTime.SpecifyKind(prevStartLocal, DateTimeKind.Local).ToUniversalTime();
            var prevEndUtc = DateTime.SpecifyKind(prevEndLocal, DateTimeKind.Local).ToUniversalTime();
            var prevSamples = App.Db.GetSamplesBetween(prevStartUtc, prevEndUtc);
            var prevReport = ReportAggregator.Build(prevSamples, intervalSec, prevStartLocal, prevEndLocal, rules);
            compareBlock = ReportComparisonText.Build(report, prevReport);
        }

        SummaryText.Text =
            $"Range: {startLocal:MMM d} – {endLocal.AddDays(-1):MMM d} ({daySpan} day(s)) · On computer in range: {Fmt(rangeOnComputer)}\n\n" +
            $"Active (typing / clicking): {Fmt(report.SecondsActiveFocused)}\n" +
            (report.SecondsThinking > 0 ? $"Thinking (reading / paused): {Fmt(report.SecondsThinking)}\n" : "") +
            $"Idle / AFK: {Fmt(report.SecondsIdle)}\n" +
            ignoredPctLine +
            quietLine +
            voiceLine +
            webSummaryBlock +
            $"\nSamples: {report.TotalSamples} · every {intervalSec}s" +
            compareBlock +
            $"\n\nTracking: Active \u2192 Thinking at {FormatMinutes(idleMin)} without keyboard/mouse (or XInput gamepad, if enabled in Settings), Thinking \u2192 Idle after a further {FormatMinutes(thinkMin)}. Speaker audio plus optional peak help with TV/HDMI. Use Tune detection for per-app overrides. Cursor ships at 30s + 30s in Rules." +
            "\nCategories reflect your current rules — adding or editing a rule updates past totals too." +
            lifecycleBlock +
            trackerBlock;

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
                $"Activity by day — {_currentReport.DayCount} days ending {_currentReport.RangeEndLocal.AddDays(-1):MMM d}";
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

    private static string BuildLifecycleSummaryText(DateTime startUtc, DateTime endUtc)
    {
        if (!App.Db.GetLifecycleLoggingEnabled())
            return "";

        var events = App.Db.GetLifecycleEventsBetween(startUtc, endUtc);
        if (events.Count == 0)
            return "";

        var culture = System.Globalization.CultureInfo.CurrentCulture;
        static string KindLabel(string k) => k.ToLowerInvariant() switch
        {
            "start" => "App started",
            "quit" => "App closed",
            "quit_update" => "Stopped (installing app update)",
            "upgrade" => "App updated",
            _ => k,
        };

        var lines = new List<string>
        {
            "",
            "App activity log (Settings → Family controls — when logging is on):",
        };
        foreach (var ev in events)
        {
            var local = ev.EventUtc.ToLocalTime().ToString("g", culture);
            var detail = string.IsNullOrWhiteSpace(ev.Detail) ? "" : $" — {ev.Detail}";
            lines.Add($"  • {local}: {KindLabel(ev.Kind)}{detail}");
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
        switch (RangeMode.SelectedIndex)
        {
            case 1:
                return (day.Date.AddDays(-6), day.Date.AddDays(1));
            case 2:
                return (day.Date.AddDays(-13), day.Date.AddDays(1));
            case 3:
                return (day.Date.AddDays(-27), day.Date.AddDays(1));
            case 4:
                var monthStart = new DateTime(day.Year, day.Month, 1);
                return (monthStart, monthStart.AddMonths(1));
            default:
                return (day.Date, day.Date.AddDays(1));
        }
    }

    private string BuildExportTitle(DateTime startLocal, DateTime endLocal)
    {
        return RangeMode.SelectedIndex switch
        {
            0 => $"What Am I Doing — {startLocal:yyyy-MM-dd}",
            4 => $"What Am I Doing — {startLocal:MMMM yyyy}",
            _ => $"What Am I Doing — {(int)(endLocal - startLocal).TotalDays} days ending {endLocal.AddDays(-1):yyyy-MM-dd}",
        };
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

    private void TuneDetection_OnClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app && PinManager.IsSet(App.Db) && !app.EnsurePinUnlocked(this))
            return;

        try
        {
            var w = new DetectionTuneWindow { Owner = this };
            if (w.ShowDialog() == true)
                RefreshReport();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("DetectionTuneWindow", ex);
            System.Windows.MessageBox.Show(
                "Could not open Tune detection.\n\n" + ex.Message,
                "What Am I Doing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
            Activate();
        }
    }

    private void MergeCategories_OnClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app && PinManager.IsSet(App.Db) && !app.EnsurePinUnlocked(this))
            return;

        var w = new CategoryMergeWindow { Owner = this };
        if (w.ShowDialog() == true)
            RefreshReport();
    }

    private void Settings_OnClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app && PinManager.IsSet(App.Db) && !app.EnsurePinUnlocked(this))
            return;

        var w = new SettingsWindow { Owner = this };
        w.ShowDialog();
        ApplyAccessibilityFromSettings();
        RefreshReport();
    }

    private void Rules_OnClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app && PinManager.IsSet(App.Db) && !app.EnsurePinUnlocked(this))
            return;

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

        ApplyAccessibilityFromSettings();
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

        var title = BuildExportTitle(startLocal, endLocal);

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
            HtmlReportExporter.WriteFile(dlg.FileName, report, title, screenEvents, includeEvidence,
                App.Db.GetTrackerReportInfo());
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
        if (System.Windows.Application.Current is App app && app.BypassMainWindowCloseCancel)
        {
            e.Cancel = false;
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private sealed record CategoryRow(string Category, string Active, string Thinking, string Time);

    private sealed record ProcessRow(string Process, string Active, string Thinking, string Time);

    private sealed record HighlightRow(string Label, string Time);
}
