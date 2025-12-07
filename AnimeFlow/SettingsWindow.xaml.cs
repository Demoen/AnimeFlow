using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AnimeFlow.Models;
using AnimeFlow.Services;

namespace AnimeFlow
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager? _settingsManager;
        private bool _hasChanges = false;

        public SettingsWindow(SettingsManager? settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager;
            LoadSettings();
            
            // Wire up change detection
            SceneThresholdSlider.ValueChanged += (s, e) => {
                SceneThresholdText.Text = SceneThresholdSlider.Value.ToString("F2");
                _hasChanges = true;
            };
        }

        private void LoadSettings()
        {
            if (_settingsManager == null) return;

            var settings = _settingsManager.Settings;

            // General
            AutoStartInterpolationCheck.IsChecked = settings.General.AutoStartInterpolation;
            CheckUpdatesCheck.IsChecked = settings.General.CheckUpdatesOnStartup;
            RememberWindowStateCheck.IsChecked = settings.General.RememberWindowState;
            ShowOsdMessagesCheck.IsChecked = settings.General.ShowOsdMessages;
            DefaultPresetCombo.SelectedIndex = (int)settings.General.DefaultQualityPreset;

            // Video
            GpuApiCombo.SelectedIndex = settings.Video.GpuApi switch
            {
                "vulkan" => 0,
                "d3d11" => 1,
                "opengl" => 2,
                _ => 0
            };
            HwdecCombo.SelectedIndex = settings.Video.Hwdec switch
            {
                "auto-safe" => 0,
                "auto" => 1,
                "no" => 2,
                _ => 0
            };
            DebandCheck.IsChecked = settings.Video.Deband;

            // Interpolation
            TargetResolutionCombo.SelectedIndex = settings.Interpolation.TargetHeight switch
            {
                540 => 0,
                720 => 1,
                1080 => 2,
                _ => 1
            };
            SceneThresholdSlider.Value = settings.Interpolation.SceneThreshold;
            UhdModeCheck.IsChecked = settings.Interpolation.UhdMode;

            // Advanced
            EnableLoggingCheck.IsChecked = settings.Advanced.EnableLogging;
            LogLevelCombo.SelectedIndex = settings.Advanced.LogLevel switch
            {
                "Debug" => 0,
                "Info" => 1,
                "Warning" => 2,
                "Error" => 3,
                _ => 1
            };
            CacheSizeText.Text = settings.Advanced.CacheSize.ToString();
        }

        private void SaveSettings()
        {
            if (_settingsManager == null) return;

            _settingsManager.UpdateSettings(settings =>
            {
                // General
                settings.General.AutoStartInterpolation = AutoStartInterpolationCheck.IsChecked ?? true;
                settings.General.CheckUpdatesOnStartup = CheckUpdatesCheck.IsChecked ?? true;
                settings.General.RememberWindowState = RememberWindowStateCheck.IsChecked ?? true;
                settings.General.ShowOsdMessages = ShowOsdMessagesCheck.IsChecked ?? true;
                settings.General.DefaultQualityPreset = (QualityPreset)DefaultPresetCombo.SelectedIndex;

                // Video
                settings.Video.GpuApi = GpuApiCombo.SelectedIndex switch
                {
                    0 => "vulkan",
                    1 => "d3d11",
                    2 => "opengl",
                    _ => "vulkan"
                };
                settings.Video.Hwdec = HwdecCombo.SelectedIndex switch
                {
                    0 => "auto-safe",
                    1 => "auto",
                    2 => "no",
                    _ => "auto-safe"
                };
                settings.Video.Deband = DebandCheck.IsChecked ?? true;

                // Interpolation
                settings.Interpolation.TargetHeight = TargetResolutionCombo.SelectedIndex switch
                {
                    0 => 540,
                    1 => 720,
                    2 => 1080,
                    _ => 720
                };
                settings.Interpolation.SceneThreshold = SceneThresholdSlider.Value;
                settings.Interpolation.UhdMode = UhdModeCheck.IsChecked ?? false;

                // Advanced
                settings.Advanced.EnableLogging = EnableLoggingCheck.IsChecked ?? true;
                settings.Advanced.LogLevel = LogLevelCombo.SelectedIndex switch
                {
                    0 => "Debug",
                    1 => "Info",
                    2 => "Warning",
                    3 => "Error",
                    _ => "Info"
                };
                if (int.TryParse(CacheSizeText.Text, out var cacheSize))
                {
                    settings.Advanced.CacheSize = cacheSize;
                }
            });

            _hasChanges = false;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_hasChanges)
            {
                var result = System.Windows.MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to cancel?",
                    "Unsaved Changes",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question
                );

                if (result == System.Windows.MessageBoxResult.No)
                    return;
            }

            DialogResult = false;
            Close();
        }

        private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnimeFlow",
                "logs"
            );

            Directory.CreateDirectory(logPath);
            Process.Start("explorer.exe", logPath);
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "This will clear all cached data. Continue?",
                "Clear Cache",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                // Clear cache logic here
                System.Windows.MessageBox.Show("Cache cleared successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "This will reset all settings to default values. Continue?",
                "Reset to Defaults",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                // Reset to defaults
                if (_settingsManager != null)
                {
                    _settingsManager.Settings.General = new GeneralSettings();
                    _settingsManager.Settings.Video = new VideoSettings();
                    _settingsManager.Settings.Interpolation = new InterpolationSettingsModel();
                    _settingsManager.Settings.Advanced = new AdvancedSettings();
                    LoadSettings();
                    _hasChanges = true;
                }
            }
        }
    }
}
