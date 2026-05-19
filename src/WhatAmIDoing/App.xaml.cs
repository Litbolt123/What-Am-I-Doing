using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;
using Application = System.Windows.Application;

namespace WhatAmIDoing;

public partial class App : Application
{
    private const string MutexName = @"Global\WhatAmIDoing_8B4E2A1C_SingleInstance";
    private Mutex? _mutex;
    private NotifyIcon? _tray;
    private string? _pendingUpdateBalloonUrl;
    private bool _updateTrayNotifiedThisSession;
    private ActivitySamplingService? _sampler;
    private ScreenCaptureService? _screens;

    public static AppDatabase Db { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Watchdog scheduled task: if the real app is already running, exit silently; otherwise start it.
        if (e.Args.Any(a => string.Equals(a, "--spawn-if-stopped", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            if (Mutex.TryOpenExisting(MutexName, out var existing))
            {
                existing.Dispose();
                Shutdown(0);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                CrashLogger.Log("SpawnIfStopped", ex);
            }

            Shutdown(0);
            return;
        }

        _mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            System.Windows.MessageBox.Show(
                "What Am I Doing is already running (check the system tray).",
                "What Am I Doing",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        CrashLogger.InstallProcessWideHooks();

        try
        {
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            HandleStartupFailure(ex);
            return;
        }

        // Defer DB/tray/sampler until the dispatcher is idle so Shell/notification area exists (HKCU Run + --minimized at
        // logon otherwise often yields no tray icon and a process that exits). Extra delay only for that path.
        var startMinimized = StartupTrayService.ArgsRequestTray(e.Args);

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            if (startMinimized)
            {
                var delay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                delay.Tick += (_, _) =>
                {
                    delay.Stop();
                    try
                    {
                        RunApplicationStartup(e);
                    }
                    catch (Exception ex)
                    {
                        HandleStartupFailure(ex);
                    }
                };
                delay.Start();
            }
            else
            {
                try
                {
                    RunApplicationStartup(e);
                }
                catch (Exception ex)
                {
                    HandleStartupFailure(ex);
                }
            }
        });
    }

    private void RunApplicationStartup(StartupEventArgs e)
    {
        CrashLogger.Install();

        // Import may replace activity.sqlite3 before we open it — must run before AppDatabase().
        CompletePendingDatabaseImportIfNeeded(e.Args);

        Db = new AppDatabase();
        Db.Initialize();
        Db.TryAppendLifecycleEvent("start", "App session started");
        InstallerBootstrap.ApplyIfPresent(Db);
        CategoryColors.Bind(Db);

        _sampler = new ActivitySamplingService(Db);
        _sampler.Start();

        _screens = new ScreenCaptureService(Db);
        _screens.Start();

        _tray = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "What Am I Doing — tracking active time",
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open dashboard", null, (_, _) => ShowDashboard());
        menu.Items.Add("-");
        menu.Items.Add("Pause screen captures for 30 minutes", null, (_, _) => PauseScreens(TimeSpan.FromMinutes(30)));
        menu.Items.Add("Resume screen captures", null, (_, _) => PauseScreens(TimeSpan.Zero));
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (_, _) => ShutdownFromTray());
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += Tray_OnMouseClick;
        _tray.BalloonTipClicked += Tray_OnBalloonTipClicked;

        var main = new MainWindow();
        MainWindow = main;

        ClearStaleUpdateNotificationIfInstalled();

        var startMinimized = StartupTrayService.ShouldStartMinimized(Db, e.Args);

        if (!startMinimized)
            ShowDashboard();

        SchedulePostStartupTrayHints(startMinimized);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (_tray is not null)
                    BackupReminderService.MaybeShowTrayReminder(Db, _tray);
            }
            catch
            {
            }
        }), DispatcherPriority.Background, TimeSpan.FromSeconds(60));
    }

    private static void NotifyMainWindowCatchUpRefresh()
    {
        if (Current.MainWindow is not MainWindow mw)
            return;

        if (mw.Dispatcher.CheckAccess())
            mw.RefreshCatchUpFromApp();
        else
            _ = mw.Dispatcher.BeginInvoke(mw.RefreshCatchUpFromApp);
    }

    private void ClearStaleUpdateNotificationIfInstalled()
    {
        var notified = Db.GetSetting(UpdateCheckService.SettingLastNotifiedReleaseVersion);
        if (string.IsNullOrWhiteSpace(notified))
            return;
        var tag = notified.Trim();
        if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            tag = tag[1..];
        if (!Version.TryParse(tag, out var v))
            return;
        if (UpdateCheckService.CurrentAssemblyVersion >= v)
            Db.SetSetting(UpdateCheckService.SettingLastNotifiedReleaseVersion, "");
    }

    /// <summary>Update check first; optional one-time tray-icon hint only if no update balloon was shown.</summary>
    private void SchedulePostStartupTrayHints(bool startMinimized)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await CheckForUpdatesOnStartupAsync().ConfigureAwait(false);

                if (_updateTrayNotifiedThisSession || startMinimized)
                    return;
                if (Db.GetSetting("tray_icon_hint_shown") == "1")
                    return;

                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                void ShowHint()
                {
                    if (_tray is null || _updateTrayNotifiedThisSession)
                        return;
                    try
                    {
                        _tray.ShowBalloonTip(
                            8000,
                            "What Am I Doing is running",
                            "Look for this icon in the taskbar notification area (click ^ if icons are hidden). Click the icon to open the dashboard.",
                            ToolTipIcon.Info);
                        Db.SetSetting("tray_icon_hint_shown", "1");
                    }
                    catch
                    {
                        /* balloon is best-effort */
                    }
                }

                if (Dispatcher.CheckAccess())
                    ShowHint();
                else
                    _ = Dispatcher.BeginInvoke(ShowHint);
            }
            catch (Exception ex)
            {
                CrashLogger.Log("SchedulePostStartupTrayHints", ex);
            }
        });
    }

    private void Tray_OnBalloonTipClicked(object? sender, EventArgs e)
    {
        void Open()
        {
            var url = _pendingUpdateBalloonUrl;
            _pendingUpdateBalloonUrl = null;
            if (string.IsNullOrWhiteSpace(url))
                return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                CrashLogger.Log("UpdateBalloonOpen", ex);
            }
        }

        if (Dispatcher.CheckAccess())
            Open();
        else
            _ = Dispatcher.BeginInvoke(Open);
    }

    /// <summary>
    /// Background: calls GitHub once per app session if enabled, then optionally shows a tray balloon (same release is not re-nagged). No silent install.
    /// </summary>
    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (Db.GetSetting(UpdateCheckService.SettingAutoCheckUpdates) == "0")
            return;

        var r = await UpdateCheckService.CheckLatestReleaseAsync().ConfigureAwait(false);
        if (r.Success)
            Db.SetSetting(UpdateCheckService.SettingLastUpdateCheckUtc,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

        if (!r.Success || r.NoPublishedReleases || !r.IsNewerThanCurrent)
        {
            UpdateAvailabilityCache.Clear();
            NotifyMainWindowCatchUpRefresh();
            return;
        }

        UpdateAvailabilityCache.Set(r.LatestVersion, r.InstallerDownloadUrl);
        NotifyMainWindowCatchUpRefresh();

        if (Db.GetSetting(UpdateCheckService.SettingNotifyTrayOnUpdate) == "0")
            return;

        if (_updateTrayNotifiedThisSession)
            return;

        Db.SetSetting(UpdateCheckService.SettingLastNotifiedReleaseVersion, r.LatestVersion ?? "");
        _updateTrayNotifiedThisSession = true;

        var url = !string.IsNullOrEmpty(r.InstallerDownloadUrl)
            ? r.InstallerDownloadUrl!
            : UpdateCheckService.ReleasesPageUrl;

        void Balloon()
        {
            if (_tray is null)
                return;
            _pendingUpdateBalloonUrl = url;
            _tray.ShowBalloonTip(
                14000,
                $"What Am I Doing {r.LatestVersion} is available",
                "Click here to open the download in your browser. Quit this app before running the installer.",
                ToolTipIcon.Info);
        }

        if (Dispatcher.CheckAccess())
            Balloon();
        else
            _ = Dispatcher.BeginInvoke(Balloon);
    }

    private void HandleStartupFailure(Exception ex)
    {
        CrashLogger.Log("OnStartup", ex);
        try
        {
            System.Windows.MessageBox.Show(
                "What Am I Doing could not start and will close.\n\n" +
                "If you just enabled \"Start when I sign in\", restart Windows once, then try again.\n\n" +
                "Technical detail (for support):\n" + ex.Message,
                "What Am I Doing — startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            /* last resort */
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            /* non-owner */
        }

        _mutex?.Dispose();
        _mutex = null;
        Shutdown();
    }

    private void Tray_OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        ShowDashboard();
    }

    /// <summary>
    /// NotifyIcon and its context menu run on a WinForms thread. WPF windows must be shown on the WPF dispatcher thread.
    /// </summary>
    private bool _pinUnlockedThisSession;

    private bool _installerUpgradeExit;

    /// <summary>When true, <see cref="MainWindow"/> must allow close so the process can exit for an installer upgrade.</summary>
    public bool BypassMainWindowCloseCancel { get; set; }

    /// <summary>Closes the app after launching the downloaded Inno setup (user already confirmed).</summary>
    public void ExitForInstallerUpgrade()
    {
        _installerUpgradeExit = true;
        BypassMainWindowCloseCancel = true;
        Shutdown(0);
    }

    private void ShowDashboard()
    {
        void Core()
        {
            if (MainWindow is not MainWindow mw)
                return;
            // Viewing the dashboard / reports never requires a PIN; Settings and Rules gate separately.
            mw.Show();
            mw.WindowState = WindowState.Normal;
            mw.Activate();
            mw.RefreshReport();
            mw.ApplyContinuousRefreshFromSettings();
        }

        if (Dispatcher.CheckAccess())
            Core();
        else
            Dispatcher.BeginInvoke(Core);
    }

    /// <summary>
    /// PIN gate for Settings and Rules. If a PIN is set, prompts once per session (remembered until process exit).
    /// Tray Exit does not use this — it always prompts when a PIN is set (<see cref="ShutdownFromTray"/>).
    /// </summary>
    public bool EnsurePinUnlocked(Window? owner)
    {
        if (_pinUnlockedThisSession)
            return true;
        if (!PinManager.IsSet(Db))
        {
            _pinUnlockedThisSession = true;
            return true;
        }

        var prompt = new PinPromptWindow();
        if (owner is not null)
            prompt.Owner = owner;
        var ok = prompt.ShowDialog() == true;
        if (ok)
            _pinUnlockedThisSession = true;
        return ok;
    }

    private void ShutdownFromTray()
    {
        void Core()
        {
            // Exit always requires the PIN when one is configured — no session "already unlocked" shortcut.
            if (PinManager.IsSet(Db))
            {
                var prompt = new PinPromptWindow();
                if (prompt.ShowDialog() != true)
                    return;
            }

            Shutdown();
        }

        if (Dispatcher.CheckAccess())
            Core();
        else
            Dispatcher.BeginInvoke(Core);
    }

    /// <summary>
    /// Load the tray icon from the embedded WPF resource if available, falling back to the generic
    /// "application" system icon so the tray never ends up iconless on a broken install.
    /// </summary>
    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
            var info = System.Windows.Application.GetResourceStream(uri);
            if (info?.Stream is { } s)
                return new System.Drawing.Icon(s);
        }
        catch
        {
            /* fall through to system icon */
        }
        return System.Drawing.SystemIcons.Application;
    }

    public void RescheduleActivitySampling() => _sampler?.Reschedule();

    public void RescheduleScreenCaptures() => _screens?.Reschedule();

    private void PauseScreens(TimeSpan duration)
    {
        var until = duration <= TimeSpan.Zero ? (DateTime?)null : DateTime.UtcNow.Add(duration);
        Db.SetScreensPausedUntilUtc(until);
        _tray?.ShowBalloonTip(2000, "What Am I Doing",
            until is null ? "Screen captures resumed." : $"Screen captures paused until {until.Value.ToLocalTime():HH:mm}.",
            ToolTipIcon.Info);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (!_installerUpgradeExit)
                Db?.TryAppendLifecycleEvent("quit", "App closed or exited");
        }
        catch
        {
            /* best-effort */
        }

        _sampler?.Dispose();
        _sampler = null;
        _screens?.Dispose();
        _screens = null;
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
        base.OnExit(e);
    }

    /// <summary>
    /// Writes a pending import request and hard-restarts the process so the database file can be replaced
    /// without fighting the single-instance mutex.
    /// </summary>
    public void RestartForImport(string sourceSqlitePath)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var pendingPath = Path.Combine(AppPaths.DataDirectory, "pending_import.json");
        File.WriteAllText(pendingPath,
            JsonSerializer.Serialize(new { source = sourceSqlitePath }));

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            /* non-owner */
        }

        _mutex?.Dispose();
        _mutex = null;

        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            Arguments = "--complete-db-import",
            UseShellExecute = true,
        });
        Environment.Exit(0);
    }

    /// <summary>
    /// Second instance entry: replace the database from a path written by Settings → Import, then continue startup.
    /// </summary>
    private static void CompletePendingDatabaseImportIfNeeded(string[] args)
    {
        if (!args.Any(a => string.Equals(a, "--complete-db-import", StringComparison.OrdinalIgnoreCase)))
            return;

        var pendingPath = Path.Combine(AppPaths.DataDirectory, "pending_import.json");
        if (!File.Exists(pendingPath))
            return;

        try
        {
            var json = File.ReadAllText(pendingPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("source", out var srcEl))
                return;
            var src = srcEl.GetString();
            File.Delete(pendingPath);
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
            {
                System.Windows.MessageBox.Show(
                    "Could not import: the backup file was missing or the pending import was invalid.",
                    "What Am I Doing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.Copy(src, AppPaths.DatabasePath, overwrite: true);
            System.Windows.MessageBox.Show(
                "Your activity database was replaced from the backup file.\n\nIf anything looks wrong, restore again from another backup.",
                "Import complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("CompletePendingDatabaseImport", ex);
            System.Windows.MessageBox.Show(
                "Import failed: " + ex.Message,
                "What Am I Doing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
