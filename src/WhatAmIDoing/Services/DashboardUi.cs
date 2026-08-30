using System.Windows;

namespace WhatAmIDoing.Services;

/// <summary>Backward-compatible entry point — themes are app-wide via <see cref="DashTheme"/>.</summary>
public static class DashboardUi
{
    public static readonly Uri ThemeUri = DashTheme.BaseThemeUri;

    public static void EnsureTheme(Window window) => DashTheme.Ensure(window);

    public static void EnsureTheme(FrameworkElement host)
    {
        DashTheme.EnsureApplicationTheme();
        if (host is Window w)
            w.SetResourceReference(Window.BackgroundProperty, "DashBgBrush");
    }
}
