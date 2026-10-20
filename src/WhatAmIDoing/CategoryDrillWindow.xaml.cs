using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhatAmIDoing.Models;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class CategoryDrillWindow
{
    private readonly string _category;

    public CategoryDrillWindow(string category, IReadOnlyList<CategoryDrillRow> rows, int totalSeconds)
    {
        _category = category;
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        HeaderText.Text = category;
        SubText.Text = rows.Count == 0
            ? "No active samples were attributed to this category in the selected range."
            : $"Top {rows.Count} window titles · total {FormatDuration(totalSeconds)} · right-click a row to add a rule";

        DrillGrid.ItemsSource = rows
            .Select(r => new Row
            {
                FirstSeen = r.LocalTimestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
                Process = r.ProcessName,
                Title = string.IsNullOrEmpty(r.WindowTitle) ? "(no title)" : r.WindowTitle,
                RawProcess = r.ProcessName,
                RawTitle = r.WindowTitle ?? "",
                Active = FormatDuration(r.Seconds),
                Thinking = r.ThinkingSeconds > 0 ? FormatDuration(r.ThinkingSeconds) : "",
            })
            .ToList();
    }

    private void DrillGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        AddRuleForApp_OnClick(sender, e);

    private void AddRuleForApp_OnClick(object sender, RoutedEventArgs e)
    {
        if (DrillGrid.SelectedItem is not Row row)
        {
            System.Windows.MessageBox.Show(this, "Select a row first.", "Category details", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenRulesWithPrefill(row.RawProcess, row.RawTitle);
    }

    private void AddRuleForTitle_OnClick(object sender, RoutedEventArgs e)
    {
        if (DrillGrid.SelectedItem is not Row row || string.IsNullOrWhiteSpace(row.RawTitle))
        {
            System.Windows.MessageBox.Show(this, "Select a row with a window title first.", "Category details", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenRulesWithPrefill(row.RawProcess, row.RawTitle, matchTitle: true);
    }

    private void OpenRulesWithPrefill(string processName, string? windowTitle, bool matchTitle = false)
    {
        if (Owner is not MainWindow main)
        {
            System.Windows.MessageBox.Show(this, "Open Rules from the main dashboard to add a rule.", "Category details",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!main.TryEnsureEditUnlocked())
            return;

        var w = new RulesWindow { Owner = main };
        if (matchTitle && !string.IsNullOrWhiteSpace(windowTitle))
            w.SetStartupPrefill(processName, windowTitle, _category, MatchKind.WindowTitleContains, windowTitle);
        else
            w.SetStartupPrefill(processName, windowTitle, _category);
        w.ShowDialog();
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 0)
            seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private sealed class Row
    {
        public string FirstSeen { get; init; } = "";
        public string Process { get; init; } = "";
        public string Title { get; init; } = "";
        public string RawProcess { get; init; } = "";
        public string RawTitle { get; init; } = "";
        public string Active { get; init; } = "";
        public string Thinking { get; init; } = "";
    }
}
