using System.Linq;
using System.Windows;

namespace WhatAmIDoing.Services;

/// <summary>Merges <c>Themes/DashboardTheme.xaml</c> so secondary windows match the main dashboard.</summary>
public static class DashboardUi
{
    public static readonly Uri ThemeUri = new("Themes/DashboardTheme.xaml", UriKind.Relative);

    public static void EnsureTheme(Window window) => EnsureTheme((FrameworkElement)window);

    /// <summary>Merges dashboard theme resources onto <paramref name="host"/> (window or user control).</summary>
    public static void EnsureTheme(FrameworkElement host)
    {
        if (host.Resources.MergedDictionaries.Any(d => d.Source == ThemeUri))
            return;
        host.Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = ThemeUri });
    }
}
