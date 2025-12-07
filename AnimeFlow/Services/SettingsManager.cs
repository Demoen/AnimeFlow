using System;
using System.IO;
using System.Threading.Tasks;
using AnimeFlow.Models;
using Newtonsoft.Json;

namespace AnimeFlow.Services
{
    /// <summary>
    /// Manages application settings persistence
    /// </summary>
    public class SettingsManager
    {
        private readonly string _settingsPath;
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        public event EventHandler? SettingsChanged;

        public SettingsManager()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnimeFlow"
            );

            Directory.CreateDirectory(appDataPath);
            _settingsPath = Path.Combine(appDataPath, "settings.json");
            _settings = new AppSettings();
        }

        public async Task LoadAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    // First run, create default settings
                    _settings = CreateDefaultSettings();
                    await SaveAsync();
                }
            }
            catch (Exception ex)
            {
                // If loading fails, use default settings
                _settings = CreateDefaultSettings();
                throw new Exception($"Failed to load settings: {ex.Message}", ex);
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                await File.WriteAllTextAsync(_settingsPath, json);
                OnSettingsChanged();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public void UpdateSettings(Action<AppSettings> updateAction)
        {
            updateAction(_settings);
            Task.Run(async () => await SaveAsync()).Wait();
        }

        private AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                General = new GeneralSettings
                {
                    AutoStartInterpolation = true,
                    DefaultQualityPreset = QualityPreset.Balanced,
                    CheckUpdatesOnStartup = true,
                    RememberWindowState = true,
                    ShowOsdMessages = true
                },
                Video = new VideoSettings
                {
                    GpuId = 0,
                    GpuApi = "vulkan",
                    Hwdec = "auto-safe",
                    Volume = 100,
                    Deband = true
                },
                Interpolation = new InterpolationSettingsModel
                {
                    TargetHeight = 720,
                    SceneThreshold = 0.15,
                    RifeModel = 0,
                    UhdMode = false,
                    ScalingAlgorithm = "spline36",
                    ModelPath = "models/rife-v4.6"
                },
                Advanced = new AdvancedSettings
                {
                    LogLevel = "Info",
                    CacheSize = 400,
                    EnableExperimentalFeatures = false,
                    EnableLogging = true,
                    MaxLogFiles = 10
                }
            };
        }

        private void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
