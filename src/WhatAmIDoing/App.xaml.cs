using System.Threading;
using System.Windows;
using System.Windows.Forms;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;
using Application = System.Windows.Application;

namespace WhatAmIDoing;

public partial class App : Application
{
    private const string MutexName = @"Global\WhatAmIDoing_8B4E2A1C_SingleInstance";
    private Mutex? _mutex;
    private NotifyIcon? _tray;
    private ActivitySamplingService? _sampler;
    private ScreenCaptureService? _screens;

    public static AppDatabase Db { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
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

        base.OnStartup(e);

        CrashLogger.Install();

        Db = new AppDatabase();
        Db.Initialize();

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

        var main = new MainWindow();
        MainWindow = main;

        var startMinimized = e.Args.Any(a =>
            string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));

        if (!startMinimized)
            ShowDashboard();
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

    private void ShowDashboard()
    {
        void Core()
        {
            if (MainWindow is not MainWindow mw)
                return;
            if (!EnsurePinUnlocked(null))
                return;
            mw.Show();
            mw.WindowState = WindowState.Normal;
            mw.Activate();
            mw.RefreshReport();
        }

        if (Dispatcher.CheckAccess())
            Core();
        else
            Dispatcher.BeginInvoke(Core);
    }

    /// <summary>
    /// Returns true if the user is allowed to interact with sensitive UI. If a PIN is set we prompt
    /// once per session and remember the answer until the app exits.
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
            if (!EnsurePinUnlocked(null))
                return;
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
}
