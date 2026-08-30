using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Accessibility affordances (large text, high contrast overlays, keyboard helpers).
/// Theme palette is applied app-wide by <see cref="DashTheme"/> (same pattern as Local Music Hub).
/// </summary>
public static class AccessibilityUi
{
    public const string SettingLargeText = "ui_large_text";
    public const string SettingHighContrast = "ui_high_contrast";
    public const string SettingDarkMode = "ui_dark_mode";
    public const string SettingKeyboardHelpers = "ui_keyboard_helpers";

    public static void Apply(Window window, AppDatabase db)
    {
        DashTheme.ApplyFromSettings(db);
        RefreshThemedVisualTree(window);

        if (db.GetSetting(SettingKeyboardHelpers) == "1")
            AccessibilityKeyboardHelpers.Attach(window);
        else
            AccessibilityKeyboardHelpers.Detach(window);
    }

    /// <summary>Re-resolve themed brushes/styles after toggling appearance at runtime.</summary>
    public static void RefreshThemedVisualTree(Window window)
    {
        window.SetResourceReference(Window.BackgroundProperty, "DashBgBrush");
        RefreshThemedVisualTreeRecursive(window, window);
    }

    private static void RefreshThemedVisualTreeRecursive(FrameworkElement host, DependencyObject node)
    {
        if (node is FrameworkElement fe && !ReferenceEquals(fe, host))
            ReapplyThemedStyle(host, fe);

        var childCount = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < childCount; i++)
            RefreshThemedVisualTreeRecursive(host, VisualTreeHelper.GetChild(node, i));
    }

    private static void ReapplyThemedStyle(FrameworkElement host, FrameworkElement element)
    {
        if (element is System.Windows.Controls.Control control)
            control.UpdateDefaultStyle();
    }
}
