using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class SettingsWindow
{
    public SettingsWindow()
    {
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
            YouTubeScaleBox.Text = App.Db.GetYouTubeContextIdleScale()
                .ToString("0.##", CultureInfo.CurrentCulture);

            ScreensEnabledBox.IsChecked = App.Db.GetScreensEnabled();
            ScreensIntervalSecondsBox.Text = Math.Max(5, App.Db.GetScreensIntervalMs() / 1000).ToString(CultureInfo.CurrentCulture);
            ScreensRetentionDaysBox.Text = App.Db.GetScreensRetentionDays().ToString(CultureInfo.CurrentCulture);
            ScreensExclusionBox.Text = App.Db.GetScreensExcludedProcesses();

            AutoStartBox.IsChecked = AutoStartService.IsEnabled();
            RequirePinBox.IsChecked = PinManager.IsSet(App.Db);
            KeepDataAfterUninstallBox.IsChecked = App.Db.GetSetting("keep_data_after_uninstall") == "1";
            LifecycleLoggingBox.IsChecked = App.Db.GetLifecycleLoggingEnabled();
            WatchdogRestartBox.IsChecked = App.Db.GetWatchdogRestartEnabled();
            UpdatePinRowVisibility();

            PopulateChartCategoryCombo();
            ReloadChartColors();
        };
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
        var cat = ChartCategoryCombo.Text?.Trim() ?? "";
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
        if (!double.TryParse(IdleMinutesBox.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var idleMin)
            && !double.TryParse(IdleMinutesBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out idleMin))
        {
            System.Windows.MessageBox.Show("Enter a number for idle minutes (e.g. 2 or 1.5).", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(SampleSecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var sampleSec)
            && !int.TryParse(SampleSecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sampleSec))
        {
            System.Windows.MessageBox.Show("Enter a whole number for sample interval in seconds.", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
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
            return;
        }

        idleMin = Math.Clamp(idleMin, 0.5, 180);
        thinkingMin = Math.Clamp(thinkingMin, 0, 60);
        sampleSec = Math.Clamp(sampleSec, 1, 120);

        if (!TryCommitChartColorRow(requireCategory: false))
            return;

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
            return;
        }

        yScale = Math.Clamp(yScale, 1, AppDatabase.YouTubeContextIdleScaleMax);

        App.Db.SetPassiveMediaAudioEngagementEnabled(PassiveMediaAudioBox.IsChecked == true);
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
                    return;
                }
                PinManager.Set(App.Db, newPin);
            }
            else if (!PinManager.IsSet(App.Db))
            {
                System.Windows.MessageBox.Show("Set a PIN of at least 4 characters or uncheck the PIN option.", "Settings",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            PinManager.Clear(App.Db);
        }

        if (System.Windows.Application.Current is App app)
        {
            app.RescheduleActivitySampling();
            app.RescheduleScreenCaptures();
            if (app.MainWindow is MainWindow mw)
                mw.RefreshReport();
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
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
}
