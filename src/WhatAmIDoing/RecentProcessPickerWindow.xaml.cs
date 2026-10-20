using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class RecentProcessPickerWindow : Window
{
    public ForegroundWindowInfo? Selected { get; private set; }

    public RecentProcessPickerWindow()
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AccessibilityUi.Apply(this, App.Db);
            UpdateCustomRangeVisibility();
            ReloadProcessList();
        };
    }

    private void RangeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ProcessesList is null)
            return;
        UpdateCustomRangeVisibility();
        ReloadProcessList();
    }

    private void CustomUnitCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ReloadProcessList();

    private void CustomRangeFields_OnChanged(object sender, RoutedEventArgs e) =>
        ReloadProcessList();

    private void UpdateCustomRangeVisibility()
    {
        if (CustomRangePanel is null || RangeCombo is null)
            return;
        CustomRangePanel.Visibility = IsCustomRangeSelected() ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool IsCustomRangeSelected() =>
        RangeCombo.SelectedItem is ComboBoxItem c && c.Tag is string t && t == "custom";

    private void ReloadProcessList()
    {
        if (ProcessesList is null)
            return;

        var (start, end) = ProcessHistoryRangeHelper.GetRangeUtc(
            RangeCombo,
            IsCustomRangeSelected(),
            CustomDurationBox,
            CustomUnitCombo);

        var rows = App.Db.GetRecentProcessSummariesBetween(start, end)
            .Select(ToRow)
            .ToList();
        ProcessesList.ItemsSource = rows;
        ProcessesList.SelectedIndex = rows.Count > 0 ? 0 : -1;
    }

    private static ProcessRow ToRow(RecentProcessSummary s)
    {
        var local = s.LastSeenUtc.ToLocalTime();
        var when = local.Date == DateTime.Today
            ? local.ToString("t", CultureInfo.CurrentCulture)
            : local.ToString("g", CultureInfo.CurrentCulture);
        var title = string.IsNullOrWhiteSpace(s.LatestWindowTitle) ? "" : $" — {Ellipsize(s.LatestWindowTitle, 48)}";
        return new ProcessRow(
            s.ProcessName,
            s.LatestWindowTitle ?? "",
            $"{s.ProcessName}{title}  ·  last {when}");
    }

    private static string Ellipsize(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        s = s.Trim();
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e) => CommitSelection();

    private void ProcessesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => CommitSelection();

    private void CommitSelection()
    {
        if (ProcessesList.SelectedItem is not ProcessRow row)
        {
            System.Windows.MessageBox.Show(this, "Select an app from the list.", "Pick from recent apps",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Selected = new ForegroundWindowInfo(row.ProcessName, row.WindowTitle);
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record ProcessRow(string ProcessName, string WindowTitle, string DisplayLabel);
}
