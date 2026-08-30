using System.Windows;
using System.Windows.Media;
using WhatAmIDoing.Data;
using Application = System.Windows.Application;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace WhatAmIDoing.Services;

/// <summary>
/// App-level dashboard theme (light / dark), modeled on Local Music Hub <c>HubTheme</c>.
/// One base dictionary plus optional overlays on <see cref="Application.Current"/> — not per-window merges.
/// </summary>
public static class DashTheme
{
    public static event EventHandler? ThemeChanged;

    public static readonly Uri BaseThemeUri = new("Themes/DashboardTheme.xaml", UriKind.Relative);

    private static readonly Uri DarkOverlayUri =
        new("pack://application:,,,/Themes/DashboardDarkMode.xaml", UriKind.Absolute);

    private static readonly Uri LargeTextUri =
        new("pack://application:,,,/Themes/AccessibilityLargeText.xaml", UriKind.Absolute);

    private static readonly Uri HighContrastUri =
        new("pack://application:,,,/Themes/AccessibilityHighContrast.xaml", UriKind.Absolute);

    public static void ApplyFromSettings(AppDatabase db) => Apply(db);

    public static void Apply(AppDatabase db)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var merged = app.Resources.MergedDictionaries;

        // Keep base theme mounted — removing all dicts races DynamicResource during layout (Hub lesson).
        if (!ContainsSource(merged, "DashboardTheme.xaml"))
            merged.Insert(0, new ResourceDictionary { Source = BaseThemeUri });

        RemoveOverlayDictionaries(merged);

        var dark = db.GetSetting(AccessibilityUi.SettingDarkMode) == "1";

        if (db.GetSetting(AccessibilityUi.SettingLargeText) == "1")
            merged.Add(new ResourceDictionary { Source = LargeTextUri });

        if (!dark && db.GetSetting(AccessibilityUi.SettingHighContrast) == "1")
            merged.Add(new ResourceDictionary { Source = HighContrastUri });

        if (dark)
            merged.Add(new ResourceDictionary { Source = DarkOverlayUri });

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Call from window constructors after <see cref="ApplyFromSettings"/> has run at startup.</summary>
    public static void Ensure(Window window)
    {
        if (Application.Current?.TryFindResource("DashBgBrush") is null)
            ApplyFromSettings(App.Db);

        window.SetResourceReference(Window.BackgroundProperty, "DashBgBrush");
    }

    /// <summary>User controls (e.g. HelpTipButton) that load before a parent window exists.</summary>
    public static void EnsureApplicationTheme()
    {
        if (Application.Current?.TryFindResource("DashBgBrush") is null)
            ApplyFromSettings(App.Db);
    }

    public static void RefreshOpenWindows()
    {
        var app = Application.Current;
        if (app is null)
            return;

        foreach (Window window in app.Windows)
        {
            window.SetResourceReference(Window.BackgroundProperty, "DashBgBrush");
            RefreshDefaultStylesRecursive(window);
            AccessibilityUi.RefreshThemedVisualTree(window);
            if (window is MainWindow mw)
                mw.RefreshReport();
        }
    }

    private static void RefreshDefaultStylesRecursive(DependencyObject node)
    {
        if (node is FrameworkElement fe)
            fe.UpdateDefaultStyle();

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
            RefreshDefaultStylesRecursive(VisualTreeHelper.GetChild(node, i));
    }

    private static void RemoveOverlayDictionaries(
        System.Collections.ObjectModel.Collection<ResourceDictionary> merged)
    {
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var source = merged[i].Source?.OriginalString ?? merged[i].Source?.ToString();
            if (string.IsNullOrEmpty(source))
                continue;
            if (source.Contains("DashboardDarkMode", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("AccessibilityLargeText", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("AccessibilityHighContrast", StringComparison.OrdinalIgnoreCase))
                merged.RemoveAt(i);
        }
    }

    private static bool ContainsSource(
        System.Collections.ObjectModel.Collection<ResourceDictionary> merged,
        string fileName)
    {
        foreach (var dict in merged)
        {
            var source = dict.Source?.OriginalString ?? dict.Source?.ToString();
            if (!string.IsNullOrEmpty(source) &&
                source.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
