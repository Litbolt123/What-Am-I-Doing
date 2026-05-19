using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Brush = System.Windows.Media.Brush;
using BrushConverter = System.Windows.Media.BrushConverter;
using Brushes = System.Windows.Media.Brushes;
using Orientation = System.Windows.Controls.Orientation;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace WhatAmIDoing.Services;

/// <summary>
/// Renders the hourly timeline (24 stacked bars, one per hour-of-day) and the day x hour heatmap
/// to either a WPF <see cref="Canvas"/> or an SVG string. The two outputs share the same data
/// pipeline so the dashboard and the exported HTML report are visually identical.
/// </summary>
public static class ChartRenderer
{
    private const double HourBarSpacing = 3;
    private const double HourLabelHeight = 18;

    // ---- WPF rendering --------------------------------------------------------------

    public static void DrawHourlyTimeline(Canvas canvas, AggregatedReport report)
    {
        canvas.Children.Clear();

        var width = canvas.ActualWidth > 0 ? canvas.ActualWidth : canvas.Width;
        var height = canvas.ActualHeight > 0 ? canvas.ActualHeight : canvas.Height;
        if (double.IsNaN(width) || double.IsNaN(height) || width < 10 || height < 10)
            return;

        var availHeight = Math.Max(20, height - HourLabelHeight);
        var barWidth = Math.Max(2, (width - 23 * HourBarSpacing) / 24);
        var maxSecondsInAnyHour = MaxHourSeconds(report);
        if (maxSecondsInAnyHour <= 0)
            maxSecondsInAnyHour = 1;

        for (var h = 0; h < 24; h++)
        {
            var x = h * (barWidth + HourBarSpacing);
            var hourTotal = report.HourlySecondsByCategory[h].Values.Sum();
            var barHeight = availHeight * hourTotal / (double)maxSecondsInAnyHour;
            var y = HourLabelHeight + (availHeight - barHeight);

            // Stack categories in deterministic order (largest first so colors are stable).
            var ordered = report.HourlySecondsByCategory[h]
                .OrderByDescending(kv => kv.Value)
                .ToList();
            var stackY = y;
            foreach (var (cat, sec) in ordered)
            {
                var segHeight = barHeight * sec / (double)Math.Max(1, hourTotal);
                if (segHeight < 0.5)
                    continue;
                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = segHeight,
                    Fill = (Brush)new BrushConverter().ConvertFromString(CategoryColors.Pick(cat))!,
                    ToolTip = $"{cat}: {FormatDuration(sec)} at {h:00}:00",
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, stackY);
                canvas.Children.Add(rect);
                stackY += segHeight;
            }

            // Hour label (every 3 hours to avoid clutter).
            if (h % 3 == 0)
            {
                var label = new TextBlock
                {
                    Text = $"{h:00}",
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                };
                Canvas.SetLeft(label, x);
                Canvas.SetTop(label, 2);
                canvas.Children.Add(label);
            }
        }
    }

    /// <summary>
    /// Draws one horizontal stacked bar per day. Each bar's width is proportional to that
    /// day's engaged time (Active + Thinking) vs. the busiest day in the range, and the bar
    /// is segmented by category using the same colors as the hourly timeline — including
    /// Uncategorized (grey) — so a parent can glance at the week and see "mostly school on
    /// Mon/Tue, games on the weekend."
    /// </summary>
    public static void DrawDailyStackedBars(Canvas canvas, AggregatedReport report)
    {
        canvas.Children.Clear();

        var width = canvas.ActualWidth > 0 ? canvas.ActualWidth : canvas.Width;
        var height = canvas.ActualHeight > 0 ? canvas.ActualHeight : canvas.Height;
        if (double.IsNaN(width) || double.IsNaN(height) || width < 10 || height < 10)
            return;

        var dayCount = Math.Max(1, report.DayCount);
        var leftLabelWidth = 78.0;
        var rightLabelWidth = 82.0;
        var rowSpacing = 6.0;
        var rowHeight = Math.Max(18.0, (height - rowSpacing * (dayCount - 1)) / dayCount);
        var barWidth = Math.Max(40.0, width - leftLabelWidth - rightLabelWidth);

        var dayTotals = new int[dayCount];
        for (var d = 0; d < dayCount; d++)
            dayTotals[d] = BarCategories(report, d).Sum(kv => kv.Value);
        var maxDayTotal = Math.Max(1, dayTotals.Max());

        for (var d = 0; d < dayCount; d++)
        {
            var date = report.RangeStartLocal.Date.AddDays(d);
            var y = d * (rowHeight + rowSpacing);
            var total = dayTotals[d];

            var dayLabel = new TextBlock
            {
                Text = date.ToString("ddd MMM d", CultureInfo.CurrentCulture),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#3D4450")!,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            };
            Canvas.SetLeft(dayLabel, 0);
            Canvas.SetTop(dayLabel, y + (rowHeight - 16) / 2);
            canvas.Children.Add(dayLabel);

            // Empty-track behind the bar so even zero-activity days read as "a day".
            var track = new Rectangle
            {
                Width = barWidth,
                Height = rowHeight,
                Fill = (Brush)new BrushConverter().ConvertFromString("#EEF1F6")!,
                RadiusX = 4,
                RadiusY = 4,
            };
            Canvas.SetLeft(track, leftLabelWidth);
            Canvas.SetTop(track, y);
            canvas.Children.Add(track);

            var filledWidth = barWidth * total / (double)maxDayTotal;
            var segX = leftLabelWidth;
            foreach (var (cat, sec) in BarCategories(report, d).OrderByDescending(kv => kv.Value))
            {
                var segW = filledWidth * sec / (double)Math.Max(1, total);
                if (segW < 0.5)
                    continue;
                var seg = new Rectangle
                {
                    Width = segW,
                    Height = rowHeight,
                    Fill = (Brush)new BrushConverter().ConvertFromString(CategoryColors.Pick(cat))!,
                    ToolTip = $"{date:ddd MMM d} — {cat}: {FormatDuration(sec)}",
                };
                Canvas.SetLeft(seg, segX);
                Canvas.SetTop(seg, y);
                canvas.Children.Add(seg);
                segX += segW;
            }

            var totalLabel = new TextBlock
            {
                Text = total > 0 ? FormatDuration(total) : "—",
                Foreground = (Brush)new BrushConverter().ConvertFromString("#5C6370")!,
                FontSize = 12,
                TextAlignment = TextAlignment.Right,
                Width = rightLabelWidth - 8,
            };
            Canvas.SetLeft(totalLabel, leftLabelWidth + barWidth + 8);
            Canvas.SetTop(totalLabel, y + (rowHeight - 16) / 2);
            canvas.Children.Add(totalLabel);
        }
    }

    /// <summary>
    /// Filter for the stacked-bar view: skip Idle / Ignored only (same engaged definition as
    /// <see cref="ReportAggregator"/>). Uncategorized appears as its own segment using
    /// <see cref="CategoryColors"/> grey.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, int>> BarCategories(AggregatedReport report, int dayIndex)
    {
        if (dayIndex < 0 || dayIndex >= report.DailyCategorySeconds.Length)
            return Array.Empty<KeyValuePair<string, int>>();
        return report.DailyCategorySeconds[dayIndex]
            .Where(kv => kv.Value > 0)
            .Where(kv => !string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase))
            .Where(kv => !string.Equals(kv.Key, "Ignored", StringComparison.OrdinalIgnoreCase));
    }

    public static void DrawCategoryLegend(WrapPanel panel, AggregatedReport report,
        ChartLegendDisplay display = ChartLegendDisplay.Time,
        int max = ChartLegendHelper.DefaultMaxEntries)
    {
        panel.Children.Clear();

        foreach (var entry in ChartLegendHelper.GetTopEntries(report, max))
        {
            var swatch = new Rectangle
            {
                Width = ChartLegendHelper.DashboardSwatchSize,
                Height = ChartLegendHelper.DashboardSwatchSize,
                RadiusX = 4,
                RadiusY = 4,
                Fill = (Brush)new BrushConverter().ConvertFromString(CategoryColors.Pick(entry.Category))!,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = ChartLegendHelper.FormatLabel(entry, display),
                FontSize = 12,
                Foreground = (Brush)new BrushConverter().ConvertFromString("#3D4450")!,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 12, 3),
            };
            item.Children.Add(swatch);
            item.Children.Add(label);
            panel.Children.Add(item);
        }
    }

    // ---- SVG rendering (mirror of the WPF charts for HTML export) -------------------

    public static string BuildHourlyTimelineSvg(AggregatedReport report, double width = 820, double height = 140)
    {
        var sb = new StringBuilder();
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {0} {1}\" width=\"100%\" height=\"{1}\" role=\"img\" aria-label=\"Hourly activity\">",
            width, height);

        var availHeight = height - HourLabelHeight;
        var barWidth = Math.Max(2.0, (width - 23 * HourBarSpacing) / 24);
        var maxSecondsInAnyHour = MaxHourSeconds(report);
        if (maxSecondsInAnyHour <= 0)
            maxSecondsInAnyHour = 1;

        for (var h = 0; h < 24; h++)
        {
            var x = h * (barWidth + HourBarSpacing);
            var hourTotal = report.HourlySecondsByCategory[h].Values.Sum();
            var barHeight = availHeight * hourTotal / (double)maxSecondsInAnyHour;
            var y = HourLabelHeight + (availHeight - barHeight);
            var stackY = y;

            foreach (var (cat, sec) in report.HourlySecondsByCategory[h].OrderByDescending(kv => kv.Value))
            {
                var segHeight = barHeight * sec / (double)Math.Max(1, hourTotal);
                if (segHeight < 0.5)
                    continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<rect x=\"{0:0.##}\" y=\"{1:0.##}\" width=\"{2:0.##}\" height=\"{3:0.##}\" fill=\"{4}\"><title>{5}: {6} at {7:00}:00</title></rect>",
                    x, stackY, barWidth, segHeight, CategoryColors.Pick(cat), EscapeXml(cat), FormatDuration(sec), h);
                stackY += segHeight;
            }

            if (h % 3 == 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<text x=\"{0:0.##}\" y=\"12\" fill=\"#74788D\" font-size=\"10\" font-family=\"Segoe UI, system-ui, sans-serif\">{1:00}</text>",
                    x, h);
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string BuildDailyStackedBarsSvg(AggregatedReport report, double width = 820)
    {
        var dayCount = Math.Max(1, report.DayCount);
        var leftLabelWidth = 82.0;
        var rightLabelWidth = 88.0;
        var rowHeight = 26.0;
        var rowSpacing = 8.0;
        var height = dayCount * rowHeight + (dayCount - 1) * rowSpacing;
        var barWidth = Math.Max(40.0, width - leftLabelWidth - rightLabelWidth);

        var dayTotals = new int[dayCount];
        for (var d = 0; d < dayCount; d++)
            dayTotals[d] = BarCategories(report, d).Sum(kv => kv.Value);
        var maxDayTotal = Math.Max(1, dayTotals.Max());

        var sb = new StringBuilder();
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {0} {1}\" width=\"100%\" height=\"{1}\" role=\"img\" aria-label=\"Daily activity by category\">",
            width, height);

        for (var d = 0; d < dayCount; d++)
        {
            var date = report.RangeStartLocal.Date.AddDays(d);
            var y = d * (rowHeight + rowSpacing);
            var total = dayTotals[d];

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<text x=\"0\" y=\"{0:0.##}\" fill=\"#3D4450\" font-size=\"12\" font-weight=\"600\" font-family=\"Segoe UI, system-ui, sans-serif\">{1}</text>",
                y + rowHeight / 2 + 4, EscapeXml(date.ToString("ddd MMM d", CultureInfo.CurrentCulture)));

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<rect x=\"{0:0.##}\" y=\"{1:0.##}\" width=\"{2:0.##}\" height=\"{3:0.##}\" rx=\"4\" ry=\"4\" fill=\"#EEF1F6\"/>",
                leftLabelWidth, y, barWidth, rowHeight);

            var filledWidth = barWidth * total / (double)maxDayTotal;
            var segX = leftLabelWidth;
            foreach (var (cat, sec) in BarCategories(report, d).OrderByDescending(kv => kv.Value))
            {
                var segW = filledWidth * sec / (double)Math.Max(1, total);
                if (segW < 0.5)
                    continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<rect x=\"{0:0.##}\" y=\"{1:0.##}\" width=\"{2:0.##}\" height=\"{3:0.##}\" fill=\"{4}\"><title>{5} — {6}: {7}</title></rect>",
                    segX, y, segW, rowHeight, CategoryColors.Pick(cat),
                    EscapeXml(date.ToString("ddd MMM d", CultureInfo.CurrentCulture)),
                    EscapeXml(cat), FormatDuration(sec));
                segX += segW;
            }

            var totalText = total > 0 ? FormatDuration(total) : "—";
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<text x=\"{0:0.##}\" y=\"{1:0.##}\" text-anchor=\"end\" fill=\"#5C6370\" font-size=\"12\" font-family=\"Segoe UI, system-ui, sans-serif\">{2}</text>",
                width - 2, y + rowHeight / 2 + 4, EscapeXml(totalText));
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    // ---- Helpers --------------------------------------------------------------------

    private static int MaxHourSeconds(AggregatedReport report)
    {
        var max = 0;
        for (var h = 0; h < 24; h++)
        {
            var t = report.HourlySecondsByCategory[h].Values.Sum();
            if (t > max)
                max = t;
        }

        return max;
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

    private static string EscapeXml(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
