using System.IO;

namespace WhatAmIDoing.Services;

/// <summary>
/// Creates or removes a per-user Desktop shortcut (.lnk) using Windows Script Host — available on all consumer Windows installs.
/// </summary>
public static class DesktopShortcutService
{
    private const string ShortcutFileName = "What Am I Doing.lnk";

    public static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutFileName);

    public static bool ShortcutExists() =>
        File.Exists(ShortcutPath);

    /// <summary>Writes or overwrites the Desktop shortcut pointing at the running executable.</summary>
    public static bool TryCreate()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            return false;

        try
        {
            var dir = Path.GetDirectoryName(exe);
            var linkPath = ShortcutPath;
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return false;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = string.IsNullOrEmpty(dir) ? "" : dir;
            shortcut.Description = "What Am I Doing — local activity tracker";
            shortcut.Save();
            return File.Exists(linkPath);
        }
        catch
        {
            return false;
        }
    }

    public static void TryRemove()
    {
        try
        {
            if (File.Exists(ShortcutPath))
                File.Delete(ShortcutPath);
        }
        catch
        {
            /* best-effort */
        }
    }
}
