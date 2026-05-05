using System.IO;

namespace WhatAmIDoing.Services;

/// <summary>
/// Append-only crash / error logger. Logs go to %LocalAppData%\WhatAmIDoing\logs\app-YYYY-MM-DD.log.
/// Best-effort: failures while logging are intentionally swallowed.
/// </summary>
public static class CrashLogger
{
    private static readonly object Sync = new();
    private static bool _processWideHooksInstalled;

    /// <summary>
    /// Registers AppDomain / task handlers as early as possible (before <see cref="System.Windows.Application.OnStartup"/>),
    /// so native or CLR failures while starting WPF still get logged.
    /// </summary>
    public static void InstallProcessWideHooks()
    {
        if (_processWideHooksInstalled)
            return;
        _processWideHooksInstalled = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("UnhandledException", e.ExceptionObject as Exception);

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    public static void Install()
    {
        InstallProcessWideHooks();

        if (System.Windows.Application.Current is { } app)
            app.DispatcherUnhandledException += (_, e) =>
            {
                Log("DispatcherUnhandledException", e.Exception);
                e.Handled = true;
            };
    }

    public static void Log(string source, Exception? ex)
    {
        try
        {
            var line = $"{DateTime.UtcNow:o}\t{source}\t{ex?.GetType().FullName}\t{ex?.Message}\n{ex?.StackTrace}\n\n";
            var path = Path.Combine(AppPaths.LogsDirectory, $"app-{DateTime.UtcNow:yyyy-MM-dd}.log");
            lock (Sync)
                File.AppendAllText(path, line);
        }
        catch
        {
            /* never crash from the crash logger */
        }
    }

    public static void Info(string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:o}\tINFO\t{message}\n";
            var path = Path.Combine(AppPaths.LogsDirectory, $"app-{DateTime.UtcNow:yyyy-MM-dd}.log");
            lock (Sync)
                File.AppendAllText(path, line);
        }
        catch
        {
            /* best effort */
        }
    }
}
