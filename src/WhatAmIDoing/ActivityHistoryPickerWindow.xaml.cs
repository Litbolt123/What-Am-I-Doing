using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class ActivityHistoryPickerWindow : Window
{
    private readonly ActivityHistoryPickMode _mode;

    public ActivityHistoryPickResult? Selected { get; private set; }

    public ActivityHistoryPickerWindow(ActivityHistoryPickMode mode)
    {
        _mode = mode;
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        Title = mode switch
        {
            ActivityHistoryPickMode.Process => "Pick from recent apps",
            ActivityHistoryPickMode.WindowTitle => "Pick from recent window titles",
            ActivityHistoryPickMode.PageOrVideo => "Pick from recent pages & videos",
            _ => "Pick from activity history",
        };
        IntroText.Text = mode switch
        {
            ActivityHistoryPickMode.Process =>
                "Choose an app from your activity history. We'll fill in match and pattern like a capture — no need to open that app now.",
            ActivityHistoryPickMode.WindowTitle =>
                "Choose a window title from your history. We'll match when the title contains that text.",
            ActivityHistoryPickMode.PageOrVideo =>
                "Choose a page, YouTube video, or IDE project we extracted from your history.",
            _ => "",
        };
        Loaded += (_, _) =>
        {
            AccessibilityUi.Apply(this, App.Db);
            UpdateCustomRangeVisibility();
            ReloadList();
        };
    }

    private void RangeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ItemsList is null)
            return;
        UpdateCustomRangeVisibility();
        ReloadList();
    }

    private void CustomUnitCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ReloadList();

    private void CustomRangeFields_OnChanged(object sender, RoutedEventArgs e) =>
        ReloadList();

    private void UpdateCustomRangeVisibility()
    {
        if (CustomRangePanel is null || RangeCombo is null)
            return;
        CustomRangePanel.Visibility = IsCustomRangeSelected() ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool IsCustomRangeSelected() =>
        RangeCombo.SelectedItem is ComboBoxItem c && c.Tag is string t && t == "custom";

    private void ReloadList()
    {
        if (ItemsList is null)
            return;

        var (start, end) = ProcessHistoryRangeHelper.GetRangeUtc(
            RangeCombo,
            IsCustomRangeSelected(),
            CustomDurationBox,
            CustomUnitCombo);

        var rows = _mode switch
        {
            ActivityHistoryPickMode.Process => App.Db.GetRecentProcessSummariesBetween(start, end)
                .Select(ToProcessRow)
                .Cast<PickerRow>()
                .ToList(),
            ActivityHistoryPickMode.WindowTitle => App.Db.GetRecentWindowTitlesBetween(start, end)
                .Select(ToTitleRow)
                .Cast<PickerRow>()
                .ToList(),
            ActivityHistoryPickMode.PageOrVideo => App.Db.GetRecentContextValuesBetween(start, end)
                .Select(ToContextRow)
                .Cast<PickerRow>()
                .ToList(),
            _ => new List<PickerRow>(),
        };

        ItemsList.ItemsSource = rows;
        ItemsList.SelectedIndex = rows.Count > 0 ? 0 : -1;
    }

    private static PickerRow ToProcessRow(RecentProcessSummary s)
    {
        var when = FormatWhen(s.LastSeenUtc);
        var title = string.IsNullOrWhiteSpace(s.LatestWindowTitle) ? "" : $" — {Ellipsize(s.LatestWindowTitle, 48)}";
        return new PickerRow(
            ActivityHistoryPickMode.Process,
            s.ProcessName,
            s.LatestWindowTitle ?? "",
            null,
            $"{s.ProcessName}{title}  ·  last {when}");
    }

    private static PickerRow ToTitleRow(RecentWindowTitleSummary s)
    {
        var when = FormatWhen(s.LastSeenUtc);
        var proc = string.IsNullOrWhiteSpace(s.ProcessName) ? "" : $" ({s.ProcessName})";
        return new PickerRow(
            ActivityHistoryPickMode.WindowTitle,
            s.WindowTitle,
            s.ProcessName,
            null,
            $"{Ellipsize(s.WindowTitle, 56)}{proc}  ·  last {when}");
    }

    private static PickerRow ToContextRow(RecentContextSummary s)
    {
        var when = FormatWhen(s.LastSeenUtc);
        var kindLabel = s.ContextKind switch
        {
            "youtube" => "YouTube",
            "project" => "Project",
            "site" => "Page",
            _ => s.ContextKind,
        };
        var proc = string.IsNullOrWhiteSpace(s.ProcessName) ? "" : $" · {s.ProcessName}";
        return new PickerRow(
            ActivityHistoryPickMode.PageOrVideo,
            s.ContextValue,
            s.ProcessName,
            s.ContextKind,
            $"[{kindLabel}] {Ellipsize(s.ContextValue, 52)}{proc}  ·  last {when}");
    }

    private static string FormatWhen(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return local.Date == DateTime.Today
            ? local.ToString("t", CultureInfo.CurrentCulture)
            : local.ToString("g", CultureInfo.CurrentCulture);
    }

    private static string Ellipsize(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        s = s.Trim();
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e) => CommitSelection();

    private void ItemsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => CommitSelection();

    private void CommitSelection()
    {
        if (ItemsList.SelectedItem is not PickerRow row)
        {
            var noun = _mode switch
            {
                ActivityHistoryPickMode.WindowTitle => "window title",
                ActivityHistoryPickMode.PageOrVideo => "page or video",
                _ => "app",
            };
            System.Windows.MessageBox.Show(this, $"Select a {noun} from the list.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Selected = new ActivityHistoryPickResult(row.Mode, row.Primary, row.Secondary, row.ContextKind);
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record PickerRow(
        ActivityHistoryPickMode Mode,
        string Primary,
        string Secondary,
        string? ContextKind,
        string DisplayLabel);
}
