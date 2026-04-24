using System.Globalization;
using System.Windows;
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
            UpdatePinRowVisibility();
        };
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
            System.Windows.MessageBox.Show("Enter a number (1–10) for the YouTube idle factor.", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
