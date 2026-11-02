using System.IO;
using System.Threading;

namespace WhatAmIDoing.Services;

internal static class UserTempHygiene
{
    private const int Attempts = 6;
    private static readonly object Sync = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Task? _sweep;

    internal const string StagingFolderName = "WhatAmIDoing";

    internal static string InstallerPath =>
        Path.Combine(Path.GetTempPath(), StagingFolderName, "setup", "WhatAmIDoing-Setup.exe");

    internal static void StartSweep()
    {
        lock (Sync)
        {
            _sweep ??= Task.Run(() =>
            {
                try
                {
                    Sweep(Path.GetTempPath());
                }
                catch
                {
                }
            });
        }
    }

    internal static Task WhenSweptAsync()
    {
        StartSweep();
        lock (Sync)
            return _sweep ?? Task.CompletedTask;
    }

    internal static void WhenSwept() => WhenSweptAsync().GetAwaiter().GetResult();

    internal static Task AcquireAsync(CancellationToken cancellationToken) =>
        Gate.WaitAsync(cancellationToken);

    internal static void Release() => Gate.Release();

    internal static void WithStagingDirectory(string leaf, Action<string> work)
    {
        WhenSwept();
        Gate.Wait();
        var dir = Path.Combine(Path.GetTempPath(), StagingFolderName, leaf);
        try
        {
            TryDeleteDirectory(dir);
            Directory.CreateDirectory(dir);
            work(dir);
        }
        finally
        {
            try
            {
                TryDeleteDirectory(dir);
            }
            finally
            {
                Gate.Release();
            }
        }
    }

    internal static void Sweep(string tempRoot)
    {
        if (string.IsNullOrWhiteSpace(tempRoot) || !Directory.Exists(tempRoot))
            return;

        foreach (var pattern in new[] { "WhatAmIDoing-support-*", "WhatAmIDoing-handoff-*" })
        {
            List<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(tempRoot, pattern).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var dir in dirs)
                TryDeleteDirectory(dir);
        }

        List<string> exes;
        try
        {
            exes = Directory.EnumerateFiles(tempRoot, "WhatAmIDoing-Setup*.exe").ToList();
        }
        catch
        {
            exes = new List<string>();
        }

        foreach (var file in exes)
            TryDeleteFile(file);

        TryDeleteDirectory(Path.Combine(tempRoot, StagingFolderName));
    }

    internal static bool TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                    return true;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            catch (Exception ex) when (IsSharingOrAccess(ex) && attempt < Attempts - 1)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50 * (1 << attempt)));
            }
            catch
            {
                return !File.Exists(path);
            }
        }

        return !File.Exists(path);
    }

    internal static bool TryDeleteDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return true;

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return true;
                ClearAttributes(dir);
                Directory.Delete(dir, recursive: true);
                return true;
            }
            catch (Exception ex) when (IsSharingOrAccess(ex) && attempt < Attempts - 1)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50 * (1 << attempt)));
            }
            catch
            {
                return !Directory.Exists(dir);
            }
        }

        return !Directory.Exists(dir);
    }

    private static bool IsSharingOrAccess(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

    private static void ClearAttributes(string dir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }
}
