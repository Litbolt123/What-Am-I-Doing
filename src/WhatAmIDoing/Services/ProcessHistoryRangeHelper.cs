using System.Globalization;

namespace WhatAmIDoing.Services;

/// <summary>Shared UTC windows for picking apps from sample history (Rules, tune detection).</summary>
public static class ProcessHistoryRangeHelper
{
    public static (DateTime StartUtc, DateTime EndUtc) GetRangeUtc(
        System.Windows.Controls.ComboBox rangeCombo,
        bool isCustomSelected,
        System.Windows.Controls.TextBox? customDurationBox,
        System.Windows.Controls.ComboBox? customUnitCombo)
    {
        var now = DateTime.UtcNow;
        if (!isCustomSelected)
        {
            if (rangeCombo.SelectedItem is not System.Windows.Controls.ComboBoxItem item || item.Tag is not string tag)
                return (now.AddHours(-2), now);
            return GetRangeUtc(tag, null, false);
        }

        if (customDurationBox is null || customUnitCombo is null
            || !TryParseCustomSpan(customDurationBox.Text, customUnitCombo.SelectedIndex == 1, out var span))
            return (now.AddHours(-2), now);
        return (now - span, now);
    }

    public static (DateTime StartUtc, DateTime EndUtc) GetRangeUtc(string tag, TimeSpan? customSpan, bool _)
    {
        var now = DateTime.UtcNow;
        var localNow = DateTime.Now;
        return tag switch
        {
            "2h" => (now.AddHours(-2), now),
            "6h" => (now.AddHours(-6), now),
            "today" => (DateTime.SpecifyKind(localNow.Date, DateTimeKind.Local).ToUniversalTime(), now),
            "yesterday" =>
            (
                DateTime.SpecifyKind(localNow.Date.AddDays(-1), DateTimeKind.Local).ToUniversalTime(),
                DateTime.SpecifyKind(localNow.Date, DateTimeKind.Local).ToUniversalTime()
            ),
            "7d" => (now.AddDays(-7), now),
            "30d" => (now.AddDays(-30), now),
            "custom" when customSpan is { } span => (now - span, now),
            _ => (now.AddHours(-2), now),
        };
    }

    public static bool TryParseCustomSpan(string text, bool useHours, out TimeSpan span)
    {
        span = TimeSpan.Zero;
        text = text.Trim();
        if (text.Length == 0)
            return false;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var n)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out n))
            return false;
        if (n <= 0 || double.IsNaN(n) || double.IsInfinity(n))
            return false;
        span = useHours ? TimeSpan.FromHours(n) : TimeSpan.FromMinutes(n);
        if (span < TimeSpan.FromMinutes(1))
            span = TimeSpan.FromMinutes(1);
        if (span > TimeSpan.FromDays(90))
            span = TimeSpan.FromDays(90);
        return true;
    }
}
