using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WhatAmIDoing.Services;

public readonly record struct ForegroundWindowInfo(string ProcessName, string WindowTitle)
{
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(WindowTitle)
            ? ProcessName
            : $"{WindowTitle}  ({ProcessName})";
}

public static class ForegroundWindowHelper
{
    private static readonly HashSet<string> SkipProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "WhatAmIDoing",
        "ApplicationFrameHost",
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "SystemSettings",
        "LockApp",
    };

    /// <summary>Visible top-level windows with a non-empty title (for the rules picker).</summary>
    public static IReadOnlyList<ForegroundWindowInfo> EnumerateVisibleWindows()
    {
        var rows = new List<ForegroundWindowInfo>();
        EnumWindows((hwnd, lParam) =>
        {
            _ = lParam;
            if (!IsWindowVisible(hwnd))
                return true;
            if (GetWindowStyle(hwnd) is long style && (style & WS_CHILD) != 0)
                return true;

            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
                return true;

            try
            {
                using var p = Process.GetProcessById((int)pid);
                var name = p.ProcessName;
                if (SkipProcessNames.Contains(name))
                    return true;
                rows.Add(new ForegroundWindowInfo(name, title));
            }
            catch
            {
                // Process exited between enum and open — skip.
            }

            return true;
        }, 0);

        return rows
            .GroupBy(r => $"{r.ProcessName}\0{r.WindowTitle}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.WindowTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ForegroundWindowInfo? TryGetForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
            return null;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return null;

        try
        {
            using var p = Process.GetProcessById((int)pid);
            var name = p.ProcessName;
            var title = GetWindowTitle(hwnd);
            return new ForegroundWindowInfo(name, title);
        }
        catch
        {
            return null;
        }
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0)
            return "";

        var sb = new StringBuilder(len + 1);
        _ = GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static long GetWindowStyle(nint hwnd) => GetWindowLong(hwnd, GWL_STYLE);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern long GetWindowLong(nint hWnd, int nIndex);

    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000;
}
