using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WhatAmIDoing.Data;
using WhatAmIDoing.Export;
using WhatAmIDoing.Models;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class MainWindow
{
    private AggregatedReport? _currentReport;
    private IReadOnlyList<DashboardTutorialStep>? _tourSteps;
    private int? _activeTourStep;
    private readonly Dictionary<Border, (System.Windows.Media.Brush? brush, Thickness thickness)> _savedHighlight = new();
    private DispatcherTimer? _continuousRefreshTimer;
    private bool _catchUpUpdateCheckStarted;
    private bool _syncingLegendDisplayCombo;

    public MainWindow()
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        DayPicker.SelectedDate = DateTime.Today;
        Loaded += (_, _) =>
        {
            ApplyAccessibilityFromSettings();
            ApplyLegendDisplayFromSettings();
            UpdateChartScrollMaxHeight();
            ApplyReportDetailsLayoutForWidth();
            RefreshReport();
            MaybeShowFirstRunChecklist();
            Dispatcher.BeginInvoke(
                () =>
                {
                    ApplyReportDetailsLayoutForWidth();
                    RefreshChartForViewportWidth();
                    RefreshCatchUpPanel();
                    ApplyContinuousRefreshFromSettings();
                },
                DispatcherPriority.Loaded);
        };

        IsVisibleChanged += (_, _) => SyncContinuousRefreshTimer();
    }

    /// <summary>Called when the dashboard is shown from the tray (window may already be loaded).</summary>
    public void ApplyContinuousRefreshFromSettings()
    {
        if (ContinuousRefreshMenuItem is not null)
            ContinuousRefreshMenuItem.IsChecked = App.Db.GetSetting(DashboardRefreshService.SettingContinuousRefresh) == "1";
        SyncContinuousRefreshTimer();
    }

    private void SyncContinuousRefreshTimer()
    {
        var want = IsVisible
                   && IsLoaded
                   && App.Db.GetSetting(DashboardRefreshService.SettingContinuousRefresh) == "1";
        if (want)
            StartContinuousRefreshTimer();
        else
            StopContinuousRefreshTimer();
    }

    private void StartContinuousRefreshTimer()
    {
        _continuousRefreshTimer ??= new DispatcherTimer
        {
            Interval = DashboardRefreshService.ContinuousRefreshInterval,
        };
        _continuousRefreshTimer.Tick -= ContinuousRefreshTimer_OnTick;
        _continuousRefreshTimer.Tick += ContinuousRefreshTimer_OnTick;
        if (!_continuousRefreshTimer.IsEnabled)
            _continuousRefreshTimer.Start();
    }

    private void StopContinuousRefreshTimer()
    {
        if (_continuousRefreshTimer is not null)
            _continuousRefreshTimer.Stop();
    }

    private void ContinuousRefreshTimer_OnTick(object? sender, EventArgs e)
    {
        if (!IsVisible || !IsLoaded)
            return;
        RefreshReport();
    }

    private void RefreshOptions_OnClick(object sender, RoutedEventArgs e)
    {
        if (RefreshOptionsButton?.ContextMenu is { } menu)
        {
            menu.PlacementTarget = RefreshOptionsButton;
            menu.IsOpen = true;
        }
    }

    private void ContinuousRefreshMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (ContinuousRefreshMenuItem is null)
            return;
        App.Db.SetSetting(
            DashboardRefreshService.SettingContinuousRefresh,
            ContinuousRefreshMenuItem.IsChecked ? "1" : "0");
        SyncContinuousRefreshTimer();
    }

    private bool? _reportDetailsNarrow;

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged)
            UpdateChartScrollMaxHeight();
        if (e.WidthChanged)
        {
            ApplyReportDetailsLayoutForWidth();
            RefreshChartForViewportWidth();
        }
    }

    private static void PlaceDetailCard(Border border, Grid panel, int row)
    {
        if (border.Parent is System.Windows.Controls.Panel parent && !ReferenceEquals(parent, panel))
            parent.Children.Remove(border);
        if (!panel.Children.Contains(border))
            panel.Children.Add(border);
        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);
    }

    private void ApplyReportDetailsLayoutForWidth()
    {
        if (ReportDetailsRoot is null || ReportDetailsLeftPanel is null || ReportDetailsRightPanel is null
            || ReportDetailsRightColumn is null || SummaryReportBorder is null || HighlightsReportBorder is null
            || CategoryReportBorder is null || ProcessReportBorder is null)
            return;

        var inner = ReportDetailsRoot.ActualWidth > 10
            ? ReportDetailsRoot.ActualWidth
            : Math.Max(0, ActualWidth - 48);
        if (inner < 10)
            return;

        const double breakpoint = 920;
        var narrow = inner < breakpoint;
        if (_reportDetailsNarrow == narrow)
            return;
        _reportDetailsNarrow = narrow;

        if (narrow)
        {
            ReportDetailsRightColumn.Width = new GridLength(0);
            PlaceDetailCard(SummaryReportBorder, ReportDetailsLeftPanel, 0);
            PlaceDetailCard(HighlightsReportBorder, ReportDetailsLeftPanel, 1);
            PlaceDetailCard(CategoryReportBorder, ReportDetailsLeftPanel, 2);
            PlaceDetailCard(ProcessReportBorder, ReportDetailsLeftPanel, 3);

            SummaryReportBorder.Margin = new Thickness(0, 0, 0, 8);
            HighlightsReportBorder.Margin = new Thickness(0, 0, 0, 8);
            CategoryReportBorder.Margin = new Thickness(0, 0, 0, 8);
            ProcessReportBorder.Margin = new Thickness(0, 0, 0, 4);
        }
        else
        {
            ReportDetailsRightColumn.Width = new GridLength(1, GridUnitType.Star);
            PlaceDetailCard(SummaryReportBorder, ReportDetailsLeftPanel, 0);
            PlaceDetailCard(HighlightsReportBorder, ReportDetailsLeftPanel, 1);
            PlaceDetailCard(CategoryReportBorder, ReportDetailsRightPanel, 0);
            PlaceDetailCard(ProcessReportBorder, ReportDetailsRightPanel, 1);

            SummaryReportBorder.Margin = new Thickness(0, 0, 10, 8);
            HighlightsReportBorder.Margin = new Thickness(0, 0, 10, 0);
            CategoryReportBorder.Margin = new Thickness(10, 0, 0, 8);
            ProcessReportBorder.Margin = new Thickness(10, 0, 0, 0);
        }
    }

    /// <summary>
    /// DataGrids inside tall grid cells otherwise leave a large empty area below few rows.
    /// </summary>
    private static void FitDataGridToRows(DataGrid grid, int rowCount)
    {
        if (rowCount <= 0)
        {
            grid.ClearValue(FrameworkElement.HeightProperty);
            return;
        }

        const double header = 30;
        var rowH = grid.RowHeight > 0 ? grid.RowHeight : 26;
        var max = double.IsNaN(grid.MaxHeight) || grid.MaxHeight <= 0 ? 420 : grid.MaxHeight;
        grid.Height = Math.Min(max, header + rowCount * rowH + 4);
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
        // Header + metrics + toolbar + window chrome + chart header/legend + outer margins (approximate).
        const double reserved = 500;
        var cap = h - reserved;
        ChartAreaScrollViewer.MaxHeight = Math.Clamp(cap, 160, 560);
    }

    private void ScrollMainReportByWheel(MouseWheelEventArgs e)
    {
        if (MainReportScrollViewer is null)
            return;
        var steps = Math.Max(1, Math.Abs(e.Delta) / 120);
        for (var i = 0; i < steps; i++)
        {
            if (e.Delta > 0)
                MainReportScrollViewer.LineUp();
            else
                MainReportScrollViewer.LineDown();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    /// <summary>
    /// Nested scroll viewers (chart, DataGrid) swallow the wheel even when they cannot scroll.
    /// Forward to the main report <see cref="ScrollViewer"/> so the page still moves.
    /// </summary>
    private void BubbleWheelToMainReportScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            if (ReferenceEquals(sv, MainReportScrollViewer))
                return;
            var sh = sv.ScrollableHeight;
            if (sh <= 0.5)
            {
                ScrollMainReportByWheel(e);
                e.Handled = true;
                return;
            }

            var atTop = sv.VerticalOffset <= 0.5 && e.Delta > 0;
            var atBottom = sv.VerticalOffset >= sh - 0.5 && e.Delta < 0;
            if (atTop || atBottom)
            {
                ScrollMainReportByWheel(e);
                e.Handled = true;
            }

            return;
        }

        if (sender is DataGrid dg)
        {
            var inner = FindVisualChild<ScrollViewer>(dg);
            if (inner is null || inner.ScrollableHeight <= 0.5)
            {
                ScrollMainReportByWheel(e);
                e.Handled = true;
                return;
            }

            var atTop = inner.VerticalOffset <= 0.5 && e.Delta > 0;
            var atBottom = inner.VerticalOffset >= inner.ScrollableHeight - 0.5 && e.Delta < 0;
            if (atTop || atBottom)
            {
                ScrollMainReportByWheel(e);
                e.Handled = true;
            }
        }
    }

    private void SummaryBlock_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollMainReportByWheel(e);
        e.Handled = true;
    }

    private void HighlightsBlock_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        if (YouTubeGrid is not null && IsDescendantOf(src, YouTubeGrid))
            return;
        if (SiteGrid is not null && IsDescendantOf(src, SiteGrid))
            return;
        if (ProjectGrid is not null && IsDescendantOf(src, ProjectGrid))
            return;
        ScrollMainReportByWheel(e);
        e.Handled = true;
    }

    private void CategoryBlock_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (CategoryGrid is not null && IsDescendantOf(e.OriginalSource as DependencyObject, CategoryGrid))
            return;
        ScrollMainReportByWheel(e);
        e.Handled = true;
    }

    private void ProcessBlock_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ProcessGrid is not null && IsDescendantOf(e.OriginalSource as DependencyObject, ProcessGrid))
            return;
        ScrollMainReportByWheel(e);
        e.Handled = true;
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

    private enum CatchUpDisplayMode
    {
        Tour,
        WhatsNew,
        Update,
        Idle,
    }

    /// <summary>Called when the background update check finds a newer release.</summary>
    public void RefreshCatchUpFromApp() => RefreshCatchUpPanel();

    public void StartDashboardTutorial(bool replay = false)
    {
        if (replay)
            DashboardTutorialService.ResetCompleted(App.Db);

        _tourSteps = DashboardTutorialService.BuildSteps(this);
        _activeTourStep = 0;
        ShowCatchUpMode(CatchUpDisplayMode.Tour);
        ShowCatchUpTourStep(0);
    }

    private void RefreshCatchUpPanel()
    {
        if (CatchUpCard is null || CatchUpTourPanel is null || CatchUpWhatsNewPanel is null || CatchUpIdlePanel is null
            || CatchUpUpdatePanel is null)
            return;

        MaybeStartCatchUpUpdateCheck();

        if (_activeTourStep is int stepIndex && _tourSteps is not null)
        {
            ShowCatchUpMode(CatchUpDisplayMode.Tour);
            ShowCatchUpTourStep(stepIndex);
            return;
        }

        if (!DashboardTutorialService.IsCompleted(App.Db))
        {
            StartDashboardTutorial();
            return;
        }

        if (DashboardTutorialService.HasUnseenWhatsNew(App.Db))
        {
            ShowCatchUpWhatsNew();
            return;
        }

        if (TryShowCatchUpUpdate())
            return;

        ShowCatchUpIdle();
    }

    private void MaybeStartCatchUpUpdateCheck()
    {
        if (_catchUpUpdateCheckStarted || UpdateAvailabilityCache.HasPending)
            return;
        if (App.Db.GetSetting(UpdateCheckService.SettingAutoCheckUpdates) == "0")
            return;

        _catchUpUpdateCheckStarted = true;
        _ = RunCatchUpUpdateCheckAsync();
    }

    private async Task RunCatchUpUpdateCheckAsync()
    {
        try
        {
            var r = await UpdateCheckService.CheckLatestReleaseAsync().ConfigureAwait(true);
            if (r.Success)
                App.Db.SetSetting(UpdateCheckService.SettingLastUpdateCheckUtc,
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            if (r.Success && r.IsNewerThanCurrent && !string.IsNullOrWhiteSpace(r.LatestVersion))
                UpdateAvailabilityCache.Set(r.LatestVersion, r.InstallerDownloadUrl);
            else
                UpdateAvailabilityCache.Clear();

            RefreshCatchUpPanel();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("CatchUpUpdateCheck", ex);
        }
    }

    private bool TryShowCatchUpUpdate()
    {
        var version = UpdateAvailabilityCache.PendingVersion;
        if (version is null || !UpdateCheckService.ShouldShowCatchUpUpdate(App.Db, version))
            return false;

        ShowCatchUpUpdate();
        return true;
    }

    private void ShowCatchUpMode(CatchUpDisplayMode mode)
    {
        CatchUpTourPanel.Visibility = mode == CatchUpDisplayMode.Tour ? Visibility.Visible : Visibility.Collapsed;
        CatchUpWhatsNewPanel.Visibility = mode == CatchUpDisplayMode.WhatsNew ? Visibility.Visible : Visibility.Collapsed;
        CatchUpUpdatePanel.Visibility = mode == CatchUpDisplayMode.Update ? Visibility.Visible : Visibility.Collapsed;
        CatchUpIdlePanel.Visibility = mode == CatchUpDisplayMode.Idle ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowCatchUpTourStep(int index)
    {
        if (_tourSteps is null || index < 0 || index >= _tourSteps.Count)
            return;

        var step = _tourSteps[index];
        CatchUpTourTitle.Text = step.Title;
        CatchUpTourBody.Text = step.Body;
        CatchUpTourStepCounter.Text = $"Step {index + 1} of {_tourSteps.Count}";
        CatchUpTourNextButton.Content = index >= _tourSteps.Count - 1 ? "Done" : "Next";
        ApplyTutorialHighlight(step.Target);
        step.Target?.BringIntoView();
    }

    private void ShowCatchUpWhatsNew()
    {
        ClearTutorialHighlight();
        ShowCatchUpMode(CatchUpDisplayMode.WhatsNew);

        var version = DashboardTutorialService.GetAppVersion();
        CatchUpWhatsNewTitle.Text = $"What’s new in v{version}";
        CatchUpWhatsNewBody.Text = string.Join(
            Environment.NewLine,
            DashboardTutorialService.GetWhatsNewBullets().Select(b => "• " + b));
    }

    private void ShowCatchUpUpdate()
    {
        ClearTutorialHighlight();
        ShowCatchUpMode(CatchUpDisplayMode.Update);

        var version = UpdateAvailabilityCache.PendingVersion ?? "?";
        var cur = DashboardTutorialService.GetAppVersion();
        CatchUpUpdateTitle.Text = $"Version {version} is on GitHub";
        CatchUpUpdateBody.Text =
            $"This PC is running {cur}. Quit the app from the tray before running the installer. " +
            "You can also use Settings → Updates to download the setup file.";
        CatchUpUpdateDownloadButton.Content = string.IsNullOrEmpty(UpdateAvailabilityCache.InstallerDownloadUrl)
            ? "Open Releases page"
            : "Open download";
    }

    private void ShowCatchUpIdle()
    {
        ClearTutorialHighlight();
        ShowCatchUpMode(CatchUpDisplayMode.Idle);

        var pending = UpdateAvailabilityCache.PendingVersion;
        if (pending is not null && !UpdateCheckService.ShouldShowCatchUpUpdate(App.Db, pending))
        {
            CatchUpIdleTitle.Text = "All caught up!";
            CatchUpIdleSubtitle.Text =
                $"An update (v{pending}) is available — use Update available when you are ready.";
            if (CatchUpUpdateLinkButton is not null)
                CatchUpUpdateLinkButton.Visibility = Visibility.Visible;
        }
        else
        {
            CatchUpIdleTitle.Text = "All caught up!";
            CatchUpIdleSubtitle.Text = "Nothing new here — you’re up to speed on this dashboard.";
            if (CatchUpUpdateLinkButton is not null)
                CatchUpUpdateLinkButton.Visibility = Visibility.Collapsed;
        }

        if (CatchUpWhatsNewLinkButton is not null)
            CatchUpWhatsNewLinkButton.Visibility = Visibility.Collapsed;
    }

    private void CompleteTour()
    {
        _activeTourStep = null;
        ClearTutorialHighlight();
        DashboardTutorialService.MarkCompleted(App.Db);
        RefreshCatchUpPanel();
    }

    private void ApplyTutorialHighlight(FrameworkElement? target)
    {
        ClearTutorialHighlight();
        if (target is not Border border)
            return;

        _savedHighlight[border] = (border.BorderBrush, border.BorderThickness);
        border.BorderBrush = (System.Windows.Media.Brush)FindResource("DashAccentBrush");
        border.BorderThickness = new Thickness(2);
    }

    private void ClearTutorialHighlight()
    {
        foreach (var (border, saved) in _savedHighlight)
        {
            border.BorderBrush = saved.brush;
            border.BorderThickness = saved.thickness;
        }

        _savedHighlight.Clear();
    }

    private void CatchUpTourNext_OnClick(object sender, RoutedEventArgs e)
    {
        if (_tourSteps is null || _activeTourStep is not int index)
            return;

        if (index + 1 >= _tourSteps.Count)
        {
            CompleteTour();
            return;
        }

        _activeTourStep = index + 1;
        ShowCatchUpTourStep(index + 1);
    }

    private void CatchUpTourSkip_OnClick(object sender, RoutedEventArgs e) => CompleteTour();

    private void CatchUpWhatsNewDone_OnClick(object sender, RoutedEventArgs e)
    {
        DashboardTutorialService.MarkWhatsNewSeen(App.Db);
        RefreshCatchUpPanel();
    }

    private void CatchUpShowWhatsNew_OnClick(object sender, RoutedEventArgs e) => ShowCatchUpWhatsNew();

    private void CatchUpShowUpdate_OnClick(object sender, RoutedEventArgs e) => ShowCatchUpUpdate();

    private void CatchUpUpdateLater_OnClick(object sender, RoutedEventArgs e)
    {
        var version = UpdateAvailabilityCache.PendingVersion;
        if (!string.IsNullOrWhiteSpace(version))
            UpdateCheckService.DismissCatchUpUpdate(App.Db, version);
        RefreshCatchUpPanel();
    }

    private void CatchUpUpdateDownload_OnClick(object sender, RoutedEventArgs e) =>
        UpdateCheckService.OpenUpdateDownload(UpdateAvailabilityCache.InstallerDownloadUrl);

    private void DashboardTour_OnClick(object sender, RoutedEventArgs e) => StartDashboardTutorial(replay: true);

    private void TrackingHelp_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var w = new TrackingHelpWindow { Owner = this };
            w.LoadContent();
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("TrackingHelp", ex);
        }
    }

    public void RefreshReport()
    {
        // ComboBox SelectionChanged can fire during InitializeComponent() before named fields below exist.
        if (SummaryText is null || SummaryTrackingStatusText is null || SummaryDetailsText is null
            || SummaryDetailsExpander is null
            || CategoryGrid is null || ProcessGrid is null || DayPicker is null || RangeMode is null
            || YouTubeGrid is null || SiteGrid is null || ProjectGrid is null
            || MetricOnComputer is null || MetricActive is null || MetricThinking is null || MetricIdle is null
            || MetricRangeLabel is null)
            return;

        var (startLocal, endLocal) = GetSelectedRange();
        var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

        var samples = App.Db.GetSamplesBetween(startUtc, endUtc);
        var intervalSec = Math.Max(1, App.Db.GetSampleIntervalMs() / 1000);
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

        var trackingStatus = TrackingStatusSummary.Build(startUtc, endUtc);
        SummaryTrackingStatusText.Text = string.Join(Environment.NewLine, trackingStatus.Lines);
        if (SummaryTrackingStatusPanel is not null)
        {
            SummaryTrackingStatusPanel.BorderBrush = trackingStatus.HasAlerts
                ? (System.Windows.Media.Brush)FindResource("DashAccentBrush")
                : (System.Windows.Media.Brush)FindResource("DashBorderBrush");
            SummaryTrackingStatusPanel.BorderThickness = trackingStatus.HasAlerts
                ? new Thickness(1.5)
                : new Thickness(1);
        }

        var webSummaryBlock = BuildWebContentSummaryText(report);
        var activityLog = TrackingStatusSummary.BuildFullActivityLog(startUtc, endUtc);

        var daySpan = Math.Max(1, report.DayCount);
        var (rangeOnComputer, _) = ComputeOnComputerSeconds(startLocal, endLocal, intervalSec, rules);

        MetricRangeLabel.Text =
            $"{startLocal:MMM d} – {endLocal.AddDays(-1):MMM d}\n{daySpan} day(s) in range";
        MetricOnComputer.Text = Fmt(rangeOnComputer);
        MetricActive.Text = Fmt(report.SecondsActiveFocused);
        MetricThinking.Text = report.SecondsThinking > 0 ? Fmt(report.SecondsThinking) : "—";
        MetricIdle.Text = Fmt(report.SecondsIdle);

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

        var summaryParts = new List<string>();
        if (!string.IsNullOrEmpty(webSummaryBlock))
            summaryParts.Add(webSummaryBlock.Trim());
        if (!string.IsNullOrEmpty(compareBlock))
            summaryParts.Add(compareBlock.Trim());

        SummaryText.Text = summaryParts.Count > 0
            ? string.Join("\n\n", summaryParts)
            : "No website or page titles to list for this range. Check Highlights below, or try a day with browser activity.";

        var extraParts = new List<string>();
        if (!string.IsNullOrEmpty(voiceLine))
            extraParts.Add(voiceLine.TrimEnd());
        if (!string.IsNullOrEmpty(ignoredPctLine))
            extraParts.Add(ignoredPctLine.TrimEnd());
        if (!string.IsNullOrEmpty(quietLine))
            extraParts.Add(quietLine.TrimEnd());
        if (!string.IsNullOrEmpty(activityLog))
            extraParts.Add(activityLog);

        if (extraParts.Count > 0)
        {
            SummaryDetailsText.Text = string.Join("\n\n", extraParts);
            SummaryDetailsExpander.Visibility = Visibility.Visible;
        }
        else
        {
            SummaryDetailsText.Text = "";
            SummaryDetailsExpander.Visibility = Visibility.Collapsed;
        }

        var catRows = report.SecondsByCategory
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
            {
                var thinking = report.ThinkingSecondsByCategory.TryGetValue(kv.Key, out var th) ? th : 0;
                var active = Math.Max(0, kv.Value - thinking);
                var isIdle = string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase);
                var pct = ChartLegendHelper.GetPercentOfTotalTime(report, kv.Value);
                return new CategoryRow(
                    kv.Key,
                    isIdle ? "" : Fmt(active),
                    thinking > 0 ? Fmt(thinking) : "",
                    Fmt(kv.Value),
                    ChartLegendHelper.FormatPercentColumn(pct));
            })
            .ToList();
        CategoryGrid.ItemsSource = catRows;
        FitDataGridToRows(CategoryGrid, catRows.Count);

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
        FitDataGridToRows(ProcessGrid, procRows.Count);

        var ytRows = TopHighlights(report.ActiveSecondsByYouTube);
        var siteRows = TopHighlights(report.ActiveSecondsBySite);
        var projectRows = TopHighlights(report.ActiveSecondsByProject);
        YouTubeGrid.ItemsSource = ytRows;
        SiteGrid.ItemsSource = siteRows;
        ProjectGrid.ItemsSource = projectRows;
        FitDataGridToRows(YouTubeGrid, ytRows.Count);
        FitDataGridToRows(SiteGrid, siteRows.Count);
        FitDataGridToRows(ProjectGrid, projectRows.Count);

        UpdateChart();
        if (LegendPanel is not null)
            ChartRenderer.DrawCategoryLegend(LegendPanel, report, GetLegendDisplayMode());
    }

    private ChartLegendDisplay GetLegendDisplayMode() =>
        ChartLegendDisplayService.Get(App.Db);

    private void ApplyLegendDisplayFromSettings()
    {
        if (LegendDisplayCombo is null)
            return;

        _syncingLegendDisplayCombo = true;
        try
        {
            var mode = ChartLegendDisplayService.Get(App.Db);
            var tag = mode switch
            {
                ChartLegendDisplay.Percent => "percent",
                ChartLegendDisplay.Both => "both",
                _ => "time",
            };
            foreach (ComboBoxItem item in LegendDisplayCombo.Items)
            {
                if (item.Tag as string == tag)
                {
                    LegendDisplayCombo.SelectedItem = item;
                    return;
                }
            }
        }
        finally
        {
            _syncingLegendDisplayCombo = false;
        }
    }

    private void LegendDisplayCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _syncingLegendDisplayCombo || LegendDisplayCombo.SelectedItem is not ComboBoxItem item)
            return;

        var tag = item.Tag as string ?? "time";
        var mode = tag switch
        {
            "percent" => ChartLegendDisplay.Percent,
            "both" => ChartLegendDisplay.Both,
            _ => ChartLegendDisplay.Time,
        };
        ChartLegendDisplayService.Set(App.Db, mode);
        if (_currentReport is not null && LegendPanel is not null)
            ChartRenderer.DrawCategoryLegend(LegendPanel, _currentReport, mode);
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
            SyncChartCanvasWidthToHost();
            ChartRenderer.DrawDailyStackedBars(ChartCanvas, _currentReport);
        }
        else
        {
            ChartTitle.Text = $"Hourly activity — {_currentReport.RangeStartLocal:dddd, MMM d}";
            ChartCanvas.Height = 160;
            SyncChartCanvasWidthToHost();
            ChartRenderer.DrawHourlyTimeline(ChartCanvas, _currentReport);
        }
    }

    private void SyncChartCanvasWidthToHost()
    {
        if (ChartCanvas is null || ChartAreaScrollViewer is null)
            return;
        if (ChartAreaScrollViewer.ActualWidth > 16)
            ChartCanvas.Width = Math.Max(80, ChartAreaScrollViewer.ActualWidth - 8);
        else
            ChartCanvas.ClearValue(FrameworkElement.WidthProperty);
    }

    private void ChartAreaScrollViewer_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            RefreshChartForViewportWidth();
    }

    private void RefreshChartForViewportWidth()
    {
        if (ChartCanvas is null || _currentReport is null)
            return;
        SyncChartCanvasWidthToHost();
        if (_currentReport.DayCount > 1)
            ChartRenderer.DrawDailyStackedBars(ChartCanvas, _currentReport);
        else
            ChartRenderer.DrawHourlyTimeline(ChartCanvas, _currentReport);
    }

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
            return "";

        var lines = new List<string> { "Where you spent time online:" };
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

        return string.Join(Environment.NewLine, lines);
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

        try
        {
            var w = new SettingsWindow { Owner = this };
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("SettingsWindow", ex);
            IsEnabled = true;
        }
        finally
        {
            IsEnabled = true;
            Activate();
        }

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
                App.Db.GetTrackerReportInfo(), rules, GetLegendDisplayMode());
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

        StopContinuousRefreshTimer();
        e.Cancel = true;
        Hide();
    }

    private sealed record CategoryRow(string Category, string Active, string Thinking, string Time, string Share);

    private sealed record ProcessRow(string Process, string Active, string Thinking, string Time);

    private sealed record HighlightRow(string Label, string Time);
}
