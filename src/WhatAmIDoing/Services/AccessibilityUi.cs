using System.Windows;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Optional accessibility resource dictionaries (large text, high contrast) and light keyboard affordances.
/// All toggles default off in the database.
/// </summary>
public static class AccessibilityUi
{
    private static readonly Uri LargeTextUri =
        new("pack://application:,,,/Themes/AccessibilityLargeText.xaml", UriKind.Absolute);

    private static readonly Uri HighContrastUri =
        new("pack://application:,,,/Themes/AccessibilityHighContrast.xaml", UriKind.Absolute);

    public const string SettingLargeText = "ui_large_text";
    public const string SettingHighContrast = "ui_high_contrast";
    public const string SettingKeyboardHelpers = "ui_keyboard_helpers";

    public static void Apply(Window window, AppDatabase db)
    {
        RemoveMerged(window);

        if (db.GetSetting(SettingLargeText) == "1")
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = LargeTextUri });

        if (db.GetSetting(SettingHighContrast) == "1")
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = HighContrastUri });

        if (db.GetSetting(SettingKeyboardHelpers) == "1")
            AccessibilityKeyboardHelpers.Attach(window);
        else
            AccessibilityKeyboardHelpers.Detach(window);
    }

    private static void RemoveMerged(Window window)
    {
        AccessibilityKeyboardHelpers.Detach(window);

        var remove = window.Resources.MergedDictionaries
            .Where(d =>
                d.Source is { IsAbsoluteUri: true, Scheme: "pack" } u &&
                (u.AbsoluteUri.Contains("AccessibilityLargeText", StringComparison.OrdinalIgnoreCase) ||
                 u.AbsoluteUri.Contains("AccessibilityHighContrast", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var d in remove)
            window.Resources.MergedDictionaries.Remove(d);
    }
}
