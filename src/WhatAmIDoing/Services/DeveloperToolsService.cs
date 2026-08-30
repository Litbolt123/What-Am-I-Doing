using System.IO;

namespace WhatAmIDoing.Services;

/// <summary>
/// Personal / maintainer tools hidden from normal users. Enabled when
/// <c>%LocalAppData%\WhatAmIDoing\dev-tools.enabled</c> exists, or in Debug builds.
/// </summary>
public static class DeveloperToolsService
{
    public const string EnableFileName = "dev-tools.enabled";

    public static bool IsEnabled()
    {
#if DEBUG
        return true;
#else
        return File.Exists(GetEnableFilePath());
#endif
    }

    public static string GetEnableFilePath() =>
        Path.Combine(AppPaths.DataDirectory, EnableFileName);

    public static string EnableHint =>
        $"Create an empty file named {EnableFileName} in:\n{AppPaths.DataDirectory}\n\n" +
        "Then reopen Settings. (Debug builds show this section automatically.)";
}
