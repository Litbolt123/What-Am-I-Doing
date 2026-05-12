using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WhatAmIDoing;

namespace WhatAmIDoing.Services;

/// <summary>
/// When enabled: show keyboard cues where the theme supports it; Esc closes lightweight tool windows.
/// </summary>
internal static class AccessibilityKeyboardHelpers
{
    private static readonly ConditionalWeakTable<Window, System.Windows.Input.KeyEventHandler> EscHandlers = new();

    public static void Attach(Window window)
    {
        Detach(window);

        System.Windows.Input.KeyEventHandler esc = (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;

            // Modal Settings/Rules/PIN use explicit buttons and Closing prompts — do not steal Esc.
            if (window is SettingsWindow or RulesWindow or PinPromptWindow or AboutWindow)
                return;

            if (window is FirstRunChecklistWindow or CategoryMergeWindow or DetectionTuneWindow)
            {
                window.Close();
                e.Handled = true;
            }
        };

        window.PreviewKeyDown += esc;
        EscHandlers.Add(window, esc);
        window.Closed += WindowClosed;
    }

    public static void Detach(Window window)
    {
        window.Closed -= WindowClosed;

        if (EscHandlers.TryGetValue(window, out var esc))
        {
            window.PreviewKeyDown -= esc;
            EscHandlers.Remove(window);
        }
    }

    private static void WindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window w)
            Detach(w);
    }
}
