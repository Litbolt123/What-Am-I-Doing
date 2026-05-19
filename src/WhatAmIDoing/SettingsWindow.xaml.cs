using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class SettingsWindow
{
    private string? _settingsFingerprint;
    private bool _suppressUnsavedClosePrompt;
    private string? _pendingInstallerUrl;
    private string? _pendingInstallerVersion;

    public SettingsWindow()
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var idleMs = App.Db.GetIdleThresholdMs();
            var thinkingMs = App.Db.GetThinkingExtraMs();
            var sampleMs = App.Db.GetSampleIntervalMs();
            IdleMinutesBox.Text = (idleMs / 60000.0).ToString("0.###", CultureInfo.CurrentCulture);
            ThinkingMinutesBox.Text = (thinkingMs / 60000.0).ToString("0.###", CultureInfo.CurrentCulture);
            SampleSecondsBox.Text = Math.Max(1, sampleMs / 1000).ToString(CultureInfo.CurrentCulture);
            AudioDetectionBox.IsChecked = App.Db.GetAudioDetectionEnabled();
            PassiveMediaAudioBox.IsChecked = App.Db.GetPassiveMediaAudioEngagementEnabled();
            PassiveMediaPeakBox.IsChecked = App.Db.GetPassiveMediaPeakFallbackEnabled();
            ControllerInputBox.IsChecked = App.Db.GetControllerInputEngagementEnabled();
            YouTubeScaleBox.Text = App.Db.GetYouTubeContextIdleScale()
                .ToString("0.##", CultureInfo.CurrentCulture);

            ScreensEnabledBox.IsChecked = App.Db.GetScreensEnabled();
            ScreensIntervalSecondsBox.Text = Math.Max(5, App.Db.GetScreensIntervalMs() / 1000).ToString(CultureInfo.CurrentCulture);
            ScreensRetentionDaysBox.Text = App.Db.GetScreensRetentionDays().ToString(CultureInfo.CurrentCulture);
            ScreensExclusionBox.Text = App.Db.GetScreensExcludedProcesses();

            AutoStartBox.IsChecked = AutoStartService.IsEnabled();
            StartInSystemTrayBox.IsChecked = StartupTrayService.IsStartInTrayEnabled(App.Db);
            DesktopShortcutBox.IsChecked = App.Db.GetDesktopShortcutEnabled();
            if (DesktopShortcutBox.IsChecked == true && !DesktopShortcutService.ShortcutExists())
                DesktopShortcutService.TryCreate();

            RequirePinBox.IsChecked = PinManager.IsSet(App.Db);
            KeepDataAfterUninstallBox.IsChecked = App.Db.GetSetting("keep_data_after_uninstall") == "1";
            LifecycleLoggingBox.IsChecked = App.Db.GetLifecycleLoggingEnabled();
            WatchdogRestartBox.IsChecked = App.Db.GetWatchdogRestartEnabled();
            UpdatePinRowVisibility();

            UiLargeTextBox.IsChecked = App.Db.GetSetting(AccessibilityUi.SettingLargeText) == "1";
            UiHighContrastBox.IsChecked = App.Db.GetSetting(AccessibilityUi.SettingHighContrast) == "1";
            UiKeyboardHelpersBox.IsChecked = App.Db.GetSetting(AccessibilityUi.SettingKeyboardHelpers) == "1";
            BackupReminderBox.IsChecked = App.Db.GetSetting("backup_reminder_enabled") == "1";
            AutoCheckUpdatesBox.IsChecked = App.Db.GetSetting(UpdateCheckService.SettingAutoCheckUpdates) != "0";
            UpdateTrayNotifyBox.IsChecked = App.Db.GetSetting(UpdateCheckService.SettingNotifyTrayOnUpdate) != "0";
            QuietHoursEnabledBox.IsChecked = App.Db.GetSetting("quiet_hours_enabled") == "1";
            QuietStartHourBox.Text = App.Db.GetSetting("quiet_start_hour") ?? AppSettingsDefaults.QuietStartHour;
            QuietEndHourBox.Text = App.Db.GetSetting("quiet_end_hour") ?? AppSettingsDefaults.QuietEndHour;
            TutorialNotesBox.Text = App.Db.GetSetting(DashboardTutorialService.SettingNotes) ?? "";

            PopulateChartCategoryCombo();
            ReloadChartColors();

            _settingsFingerprint = ComputeSettingsFingerprint();

            AccessibilityUi.Apply(this, App.Db);
        };

        Closing += SettingsWindow_OnClosing;
    }

    private string ComputeSettingsFingerprint()
    {
        static string B(bool? x) => x == true ? "1" : "0";

        return string.Join("|",
            IdleMinutesBox.Text.Trim(),
            ThinkingMinutesBox.Text.Trim(),
            SampleSecondsBox.Text.Trim(),
            B(AudioDetectionBox.IsChecked),
            B(PassiveMediaAudioBox.IsChecked),
            B(PassiveMediaPeakBox.IsChecked),
            B(ControllerInputBox.IsChecked),
            YouTubeScaleBox.Text.Trim(),
            B(ScreensEnabledBox.IsChecked),
            ScreensIntervalSecondsBox.Text.Trim(),
            ScreensRetentionDaysBox.Text.Trim(),
            ScreensExclusionBox.Text.Trim(),
            B(AutoStartBox.IsChecked),
            B(StartInSystemTrayBox.IsChecked),
            B(DesktopShortcutBox.IsChecked),
            B(RequirePinBox.IsChecked),
            (PinBox.Password ?? "").Length.ToString(CultureInfo.InvariantCulture),
            B(KeepDataAfterUninstallBox.IsChecked),
            B(LifecycleLoggingBox.IsChecked),
            B(WatchdogRestartBox.IsChecked),
            EditableComboHelper.GetText(ChartCategoryCombo),
            ChartColorHexBox.Text.Trim(),
            B(UiLargeTextBox.IsChecked),
            B(UiHighContrastBox.IsChecked),
            B(UiKeyboardHelpersBox.IsChecked),
            B(BackupReminderBox.IsChecked),
            B(AutoCheckUpdatesBox.IsChecked),
            B(UpdateTrayNotifyBox.IsChecked),
            B(QuietHoursEnabledBox.IsChecked),
            QuietStartHourBox.Text.Trim(),
            QuietEndHourBox.Text.Trim(),
            TutorialNotesBox.Text.Trim());
    }

    private void SettingsWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_suppressUnsavedClosePrompt)
            return;

        if (_settingsFingerprint is null)
            return;

        if (ComputeSettingsFingerprint() == _settingsFingerprint)
            return;

        var r = System.Windows.MessageBox.Show(
            "You have unsaved changes in Settings. Save before closing?",
            "Settings",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (r == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (r == MessageBoxResult.No)
            return;

        if (!TryPersistSettings())
            e.Cancel = true;
    }

    private sealed class ChartColorDisplayRow
    {
        public string Category { get; init; } = "";
        public string ColorHex { get; init; } = "";
    }

    private void PopulateChartCategoryCombo()
    {
        var cats = App.Db.GetRules()
            .Select(r => r.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ChartCategoryCombo.ItemsSource = cats;
        if (cats.Count > 0 && ChartCategoryCombo.SelectedIndex < 0)
            ChartCategoryCombo.SelectedIndex = 0;
    }

    private void ReloadChartColors()
    {
        ChartColorOverridesList.ItemsSource = App.Db.GetAllCategoryColors()
            .Select(t => new ChartColorDisplayRow { Category = t.Category, ColorHex = t.ColorHex })
            .ToList();
    }

    private void PickChartColor_OnClick(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.ColorDialog();
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;
        var c = dlg.Color;
        ChartColorHexBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private void AddChartColor_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryCommitChartColorRow(requireCategory: true))
            return;
        RefreshMainReportIfOpen();
    }

    /// <summary>
    /// Persists the category + hex in the chart row when valid. If <paramref name="requireCategory"/> is true,
    /// an empty category shows a prompt (Save color button). For main Settings Save, use false: empty category skips silently.
    /// </summary>
    /// <returns>False if a category was entered but hex is invalid (caller should abort).</returns>
    private bool TryCommitChartColorRow(bool requireCategory)
    {
        var cat = EditableComboHelper.GetText(ChartCategoryCombo);
        if (cat.Length == 0)
        {
            if (requireCategory)
            {
                System.Windows.MessageBox.Show("Choose or type a category label.", "Chart colors",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return true;
        }

        if (!CategoryColors.TryNormalizeHex(ChartColorHexBox.Text, out var hex))
        {
            System.Windows.MessageBox.Show(
                requireCategory
                    ? "Enter a valid color: #RGB or #RRGGBB (or use Pick…)."
                    : "Chart color: enter a valid color (#RGB or #RRGGBB), or clear the category field if you are not changing colors.",
                requireCategory ? "Chart colors" : "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        App.Db.SetCategoryColor(cat, hex);
        ReloadChartColors();
        return true;
    }

    private static void RefreshMainReportIfOpen()
    {
        if (System.Windows.Application.Current is App app && app.MainWindow is MainWindow mw)
            mw.RefreshReport();
    }

    private void RemoveChartColor_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ChartColorDisplayRow row)
            return;
        App.Db.DeleteCategoryColor(row.Category);
        ReloadChartColors();
        RefreshMainReportIfOpen();
    }

    private void RequirePinBox_OnChanged(object sender, RoutedEventArgs e) => UpdatePinRowVisibility();

    private void UpdatePinRowVisibility()
    {
        if (PinRow is null)
            return;
        PinRow.Visibility = RequirePinBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void About_OnClick(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryPersistSettings())
            return;

        DialogResult = true;
    }

    /// <summary>Persists all settings fields. Returns false if validation failed.</summary>
    private bool TryPersistSettings()
    {
        if (!double.TryParse(IdleMinutesBox.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var idleMin)
            && !double.TryParse(IdleMinutesBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out idleMin))
        {
            System.Windows.MessageBox.Show("Enter a number for idle minutes (e.g. 2 or 1.5).", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(SampleSecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var sampleSec)
            && !int.TryParse(SampleSecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sampleSec))
        {
            System.Windows.MessageBox.Show("Enter a whole number for sample interval in seconds.", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        double thinkingMin;
        var thinkingText = ThinkingMinutesBox.Text.Trim();
        if (thinkingText.Length == 0)
        {
            thinkingMin = 0;
        }
        else if (!double.TryParse(thinkingText, NumberStyles.Float, CultureInfo.CurrentCulture, out thinkingMin)
                 && !double.TryParse(thinkingText, NumberStyles.Float, CultureInfo.InvariantCulture, out thinkingMin))
        {
            System.Windows.MessageBox.Show(
                "Enter a number (or 0) for Thinking minutes.",
                "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        idleMin = Math.Clamp(idleMin, 0.5, 180);
        thinkingMin = Math.Clamp(thinkingMin, 0, 60);
        sampleSec = Math.Clamp(sampleSec, 1, 120);

        if (!TryCommitChartColorRow(requireCategory: false))
            return false;

        var idleMs = (int)Math.Round(idleMin * 60_000);
        var thinkingMs = (int)Math.Round(thinkingMin * 60_000);
        var sampleMs = sampleSec * 1000;

        App.Db.SetIdleThresholdMs(idleMs);
        App.Db.SetThinkingExtraMs(thinkingMs);
        App.Db.SetSampleIntervalMs(sampleMs);
        App.Db.SetAudioDetectionEnabled(AudioDetectionBox.IsChecked == true);

        if (!double.TryParse(YouTubeScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var yScale)
            && !double.TryParse(YouTubeScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out yScale))
        {
            System.Windows.MessageBox.Show(
                $"Enter a number (1–{AppDatabase.YouTubeContextIdleScaleMax}) for the YouTube idle factor.",
                "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        yScale = Math.Clamp(yScale, 1, AppDatabase.YouTubeContextIdleScaleMax);

        App.Db.SetPassiveMediaAudioEngagementEnabled(PassiveMediaAudioBox.IsChecked == true);
        App.Db.SetPassiveMediaPeakFallbackEnabled(PassiveMediaPeakBox.IsChecked == true);
        App.Db.SetControllerInputEngagementEnabled(ControllerInputBox.IsChecked == true);
        App.Db.SetYouTubeContextIdleScale(yScale);

        App.Db.SetScreensEnabled(ScreensEnabledBox.IsChecked == true);
        if (int.TryParse(ScreensIntervalSecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var screensSec)
            || int.TryParse(ScreensIntervalSecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out screensSec))
        {
            App.Db.SetScreensIntervalMs(Math.Clamp(screensSec, 5, 60 * 60) * 1000);
        }

        if (int.TryParse(ScreensRetentionDaysBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var retainDays)
            || int.TryParse(ScreensRetentionDaysBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out retainDays))
        {
            App.Db.SetScreensRetentionDays(Math.Clamp(retainDays, 1, 365));
        }

        App.Db.SetScreensExcludedProcesses(ScreensExclusionBox.Text.Trim());

        AutoStartService.SetEnabled(AutoStartBox.IsChecked == true);
        App.Db.SetSetting(StartupTrayService.SettingStartInSystemTray,
            StartInSystemTrayBox.IsChecked == true ? "1" : "0");

        App.Db.SetDesktopShortcutEnabled(DesktopShortcutBox.IsChecked == true);
        if (DesktopShortcutBox.IsChecked == true)
        {
            if (!DesktopShortcutService.TryCreate())
            {
                System.Windows.MessageBox.Show(
                    "Could not create the Desktop shortcut (permission or Windows Script Host issue).",
                    "Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        else
            DesktopShortcutService.TryRemove();

        App.Db.SetSetting("keep_data_after_uninstall", KeepDataAfterUninstallBox.IsChecked == true ? "1" : "0");

        App.Db.SetLifecycleLoggingEnabled(LifecycleLoggingBox.IsChecked == true);
        var wantWatchdog = WatchdogRestartBox.IsChecked == true;
        App.Db.SetWatchdogRestartEnabled(wantWatchdog);
        if (wantWatchdog)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !WatchdogTaskHelper.TryRegister(exe))
            {
                System.Windows.MessageBox.Show(
                    "Could not register the restart task (schtasks). You may need to run the app once normally, " +
                    "or register the task manually. The setting was saved; try Save again after fixing permissions.",
                    "Restart helper",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        else
        {
            WatchdogTaskHelper.TryUnregister();
        }

        if (RequirePinBox.IsChecked == true)
        {
            var newPin = PinBox.Password ?? "";
            if (newPin.Length > 0)
            {
                if (newPin.Length < 4)
                {
                    System.Windows.MessageBox.Show("PIN must be at least 4 characters.", "Settings",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                PinManager.Set(App.Db, newPin);
            }
            else if (!PinManager.IsSet(App.Db))
            {
                System.Windows.MessageBox.Show("Set a PIN of at least 4 characters or uncheck the PIN option.", "Settings",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        else
        {
            PinManager.Clear(App.Db);
        }

        App.Db.SetSetting(AccessibilityUi.SettingLargeText, UiLargeTextBox.IsChecked == true ? "1" : "0");
        App.Db.SetSetting(AccessibilityUi.SettingHighContrast, UiHighContrastBox.IsChecked == true ? "1" : "0");
        App.Db.SetSetting(AccessibilityUi.SettingKeyboardHelpers, UiKeyboardHelpersBox.IsChecked == true ? "1" : "0");
        App.Db.SetSetting("backup_reminder_enabled", BackupReminderBox.IsChecked == true ? "1" : "0");
        App.Db.SetSetting(UpdateCheckService.SettingAutoCheckUpdates, AutoCheckUpdatesBox.IsChecked == true ? "1" : "0");
        App.Db.SetSetting(UpdateCheckService.SettingNotifyTrayOnUpdate, UpdateTrayNotifyBox.IsChecked == true ? "1" : "0");

        if (!int.TryParse(QuietStartHourBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var qhStart))
            qhStart = 22;
        if (!int.TryParse(QuietEndHourBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var qhEnd))
            qhEnd = 7;
        qhStart = Math.Clamp(qhStart, 0, 23);
        qhEnd = Math.Clamp(qhEnd, 0, 23);
        App.Db.SetSetting("quiet_hours_enabled", QuietHoursEnabledBox.IsChecked == true ? "1" : "0");
        App.Db.SetSetting("quiet_start_hour", qhStart.ToString(CultureInfo.InvariantCulture));
        App.Db.SetSetting("quiet_end_hour", qhEnd.ToString(CultureInfo.InvariantCulture));
        App.Db.SetSetting(DashboardTutorialService.SettingNotes, TutorialNotesBox.Text.Trim());

        if (System.Windows.Application.Current is App app)
        {
            app.RescheduleActivitySampling();
            app.RescheduleScreenCaptures();
            if (app.MainWindow is MainWindow mw)
            {
                mw.RefreshReport();
                mw.ApplyAccessibilityFromSettings();
            }
        }

        _settingsFingerprint = ComputeSettingsFingerprint();
        return true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        _suppressUnsavedClosePrompt = true;
        DialogResult = false;
        Close();
    }

    private void ExportDatabase_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SQLite database (*.sqlite3)|*.sqlite3|All files (*.*)|*.*",
            FileName = $"what-am-i-doing-backup-{DateTime.Now:yyyy-MM-dd}.sqlite3",
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            App.Db.BackupDatabaseToFile(dlg.FileName);
            BackupReminderService.RecordManualBackup(App.Db);
            System.Windows.MessageBox.Show(
                $"Saved backup:\n{dlg.FileName}",
                "Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Could not export: " + ex.Message,
                "Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportDatabase_OnClick(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            "Import replaces ALL current activity data, rules, and settings with the chosen backup file.\n\nThe app will restart immediately.\n\nContinue?",
            "Import database",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
            return;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "SQLite database (*.sqlite3)|*.sqlite3|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true)
            return;

        if (System.Windows.Application.Current is App app)
            app.RestartForImport(dlg.FileName);
    }

    private void SupportBundle_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Zip archive (*.zip)|*.zip",
            FileName = $"what-am-i-doing-support-{DateTime.Now:yyyy-MM-dd}.zip",
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            SupportBundleService.WriteBundleZip(dlg.FileName, App.Db);
            System.Windows.MessageBox.Show(
                $"Saved support bundle:\n{dlg.FileName}",
                "Support bundle",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Could not create bundle: " + ex.Message,
                "Support bundle",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void HandoffZip_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Zip archive (*.zip)|*.zip",
            FileName = $"what-am-i-doing-handoff-{DateTime.Now:yyyy-MM-dd}.zip",
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            TwoPcHandoffService.WriteHandoffZip(dlg.FileName);
            BackupReminderService.RecordManualBackup(App.Db);
            System.Windows.MessageBox.Show(
                $"Saved handoff zip:\n{dlg.FileName}",
                "PC handoff",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Could not create zip: " + ex.Message,
                "PC handoff",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string GetDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var i = info.IndexOf('+');
            var s = (i >= 0 ? info[..i] : info).Trim();
            if (s.Length > 0 && char.IsDigit(s[0]))
                return s;
        }

        return asm.GetName().Version?.ToString(4) ?? "0.0.0";
    }

    private async void CheckUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsInstallerUpgradePanel.Visibility = Visibility.Collapsed;
        _pendingInstallerUrl = null;
        _pendingInstallerVersion = null;

        SettingsUpdateStatusText.Text = "Checking GitHub Releases…";
        var r = await UpdateCheckService.CheckLatestReleaseAsync();
        if (r.NoPublishedReleases)
        {
            SettingsUpdateStatusText.Text =
                "No published release on GitHub yet (the Releases page may be empty until the first installer is uploaded). " +
                "Use “Open Releases page” to check in your browser.";
            return;
        }

        if (!r.Success)
        {
            SettingsUpdateStatusText.Text = "Could not check for updates: " + (r.ErrorMessage ?? "unknown error");
            return;
        }

        var cur = GetDisplayVersion();
        if (r.IsNewerThanCurrent)
        {
            UpdateAvailabilityCache.Set(r.LatestVersion, r.InstallerDownloadUrl);
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                mw.RefreshCatchUpFromApp();

            SettingsUpdateStatusText.Text =
                $"A newer release is available on GitHub (release {r.LatestVersion}, this app {cur}). " +
                (string.IsNullOrEmpty(r.InstallerDownloadUrl)
                    ? "No installer file was attached to that release — use Open Releases page to download manually."
                    : "Use “Download and run installer…” below, or open the Releases / download link in your browser.");

            if (!string.IsNullOrEmpty(r.InstallerDownloadUrl))
            {
                _pendingInstallerUrl = r.InstallerDownloadUrl;
                _pendingInstallerVersion = r.LatestVersion ?? "";
                SettingsInstallerUpgradePanel.Visibility = Visibility.Visible;
            }
        }
        else
        {
            UpdateAvailabilityCache.Clear();
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                mw.RefreshCatchUpFromApp();

            SettingsUpdateStatusText.Text =
                $"You are up to date with the highest published release tag we found ({r.LatestVersion}).";
        }
    }

    private void OpenReleases_OnClick(object sender, RoutedEventArgs e) =>
        UpdateCheckService.OpenReleasesInBrowser();

    private async void DownloadAndRunInstaller_OnClick(object sender, RoutedEventArgs e)
    {
        var url = _pendingInstallerUrl;
        var ver = _pendingInstallerVersion;
        if (string.IsNullOrEmpty(url))
        {
            SettingsUpdateStatusText.Text = "Checking for download link…";
            var r = await UpdateCheckService.CheckLatestReleaseAsync();
            if (!r.Success || !r.IsNewerThanCurrent || string.IsNullOrEmpty(r.InstallerDownloadUrl))
            {
                System.Windows.MessageBox.Show(
                    "No installer download is available right now. Use “Open Releases page” and download " +
                    "WhatAmIDoing-Setup-….exe manually.",
                    "Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            url = r.InstallerDownloadUrl;
            ver = r.LatestVersion ?? "";
        }

        var q = System.Windows.MessageBox.Show(
            "What Am I Doing will STOP completely: it will not keep running in the system tray.\n\n" +
            "The installer will be saved under your user Temp folder, then the setup program will start.\n\n" +
            "If Windows shows SmartScreen, use “More info” / “Run anyway” only if you trust this download from GitHub.\n\n" +
            "Continue?",
            "Download and install update?",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (q != MessageBoxResult.OK)
            return;

        try
        {
            IsEnabled = false;
            SettingsDownloadRunInstallerButton.IsEnabled = false;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            SettingsUpdateStatusText.Text = "Downloading installer (this may take a minute)…";

            var (path, err) = await UpdateCheckService.DownloadInstallerToTempAsync(url, ver).ConfigureAwait(true);
            if (path is null)
            {
                System.Windows.MessageBox.Show(err ?? "Download failed.", "Update", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Saved the installer but could not start it:\n" + ex.Message +
                    "\n\nYou can run this file yourself:\n" + path,
                    "Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var cur = GetDisplayVersion();
            var target = string.IsNullOrWhiteSpace(ver) ? "(unknown release)" : ver.Trim();
            try
            {
                App.Db.TryAppendLifecycleEvent("quit_update",
                    $"Stopped to install a newer version (this app was {cur}; GitHub release {target}). App fully exited (not tray) so the installer could run.");
            }
            catch
            {
                /* best-effort */
            }

            if (System.Windows.Application.Current is App app)
                app.ExitForInstallerUpgrade();
            else
                Environment.Exit(0);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
            IsEnabled = true;
            SettingsDownloadRunInstallerButton.IsEnabled = true;
        }
    }

    private void ReplayDashboardTour_OnClick(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mw)
        {
            _suppressUnsavedClosePrompt = true;
            Close();
            mw.StartDashboardTutorial(replay: true);
            return;
        }

        System.Windows.MessageBox.Show(
            "Open the dashboard first, then use Settings → Replay dashboard tour, or click Tour on the dashboard.",
            "Dashboard tour",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenTrackingHelp_OnClick(object sender, RoutedEventArgs e)
    {
        var w = new TrackingHelpWindow { Owner = this };
        w.LoadContent();
        w.ShowDialog();
    }
}
