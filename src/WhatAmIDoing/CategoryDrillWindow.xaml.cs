using System.Globalization;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class CategoryDrillWindow
{
    public CategoryDrillWindow(string category, IReadOnlyList<CategoryDrillRow> rows, int totalSeconds)
    {
        InitializeComponent();
        HeaderText.Text = category;
        SubText.Text = rows.Count == 0
            ? "No active samples were attributed to this category in the selected range."
            : $"Top {rows.Count} window titles · total {FormatDuration(totalSeconds)}";

        DrillGrid.ItemsSource = rows
            .Select(r => new Row
            {
                FirstSeen = r.LocalTimestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
                Process = r.ProcessName,
                Title = string.IsNullOrEmpty(r.WindowTitle) ? "(no title)" : r.WindowTitle,
                Active = FormatDuration(r.Seconds),
                Thinking = r.ThinkingSeconds > 0 ? FormatDuration(r.ThinkingSeconds) : "",
            })
            .ToList();
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
        public string Active { get; init; } = "";
        public string Thinking { get; init; } = "";
    }
}
