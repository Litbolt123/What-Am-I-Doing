using System.IO;

namespace WhatAmIDoing;

internal static class AppPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhatAmIDoing");

    public static string DataDirectory
    {
        get
        {
            Directory.CreateDirectory(Root);
            return Root;
        }
    }

    public static string DatabasePath => Path.Combine(DataDirectory, "activity.sqlite3");

    public static string ScreensDirectory
    {
        get
        {
            var dir = Path.Combine(DataDirectory, "screens");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string LogsDirectory
    {
        get
        {
            var dir = Path.Combine(DataDirectory, "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
