using System;
using System.IO;
using AnimeFlow.Models;
using AnimeFlow.Services;

namespace AnimeFlow.Core
{
    /// <summary>
    /// Manages interpolation state and quality presets
    /// </summary>
    public class InterpolationManager
    {
        private readonly MpvPlayer _mpvPlayer;
        private readonly SettingsManager _settingsManager;
        private bool _isEnabled;
        private QualityPreset _currentPreset;

        public bool IsEnabled => _isEnabled;
        public QualityPreset CurrentPreset => _currentPreset;

        public InterpolationManager(MpvPlayer mpvPlayer, SettingsManager settingsManager)
        {
            _mpvPlayer = mpvPlayer ?? throw new ArgumentNullException(nameof(mpvPlayer));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _currentPreset = settingsManager.Settings.General.DefaultQualityPreset;
        }

        public void Enable()
        {
            if (_isEnabled)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[InterpolationManager] Enabling interpolation...");
                
                // Enable mpv's minterpolate with quality based on preset
                _mpvPlayer.EnableInterpolation(_currentPreset);

                _isEnabled = true;
                System.Diagnostics.Debug.WriteLine("[InterpolationManager] Interpolation enabled successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterpolationManager] Error: {ex}");
                throw new Exception($"Failed to enable interpolation: {ex.Message}", ex);
            }
        }

        public void Disable()
        {
            if (!_isEnabled)
                return;

            try
            {
                // Disable mpv's interpolation
                _mpvPlayer.DisableInterpolation();
                _isEnabled = false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disable interpolation: {ex.Message}", ex);
            }
        }

        public void SetPreset(QualityPreset preset)
        {
            if (_currentPreset == preset)
                return;

            _currentPreset = preset;

            // If interpolation is currently enabled, re-apply with new preset
            if (_isEnabled)
            {
                Disable();
                Enable();
            }
        }

        public PerformanceMetrics GetMetrics()
        {
            // Query real FPS from mpv (with error handling to prevent lag)
            double currentFps = _isEnabled ? 60.0 : 24.0;
            
            try
            {
                var fps = _mpvPlayer.GetEstimatedVfFps();
                if (fps > 0)
                {
                    currentFps = fps;
                }
            }
            catch
            {
                // Silently fall back to estimated FPS
            }

            // Return metrics
            return new PerformanceMetrics
            {
                CurrentFps = currentFps,
                DroppedFrames = 0,
                GpuUsage = 0.0f,  // Would need GPU monitoring library for real values
                VramUsage = 0,
                Latency = 0.0
            };
        }

        private InterpolationSettings GetInterpolationSettings(QualityPreset preset)
        {
            return preset switch
            {
                QualityPreset.Fast => new InterpolationSettings
                {
                    TargetHeight = 540,
                    SceneThreshold = 0.20,
                    RifeModel = 1, // Lite model
                    UhdMode = false,
                    ScalingAlgorithm = "bilinear"
                },
                QualityPreset.Balanced => new InterpolationSettings
                {
                    TargetHeight = 720,
                    SceneThreshold = 0.15,
                    RifeModel = 0, // Full model
                    UhdMode = false,
                    ScalingAlgorithm = "spline36"
                },
                QualityPreset.Beauty => new InterpolationSettings
                {
                    TargetHeight = 1080,
                    SceneThreshold = 0.10,
                    RifeModel = 0, // Full model
                    UhdMode = false,
                    ScalingAlgorithm = "lanczos"
                },
                QualityPreset.Custom => new InterpolationSettings
                {
                    TargetHeight = _settingsManager.Settings.Interpolation.TargetHeight,
                    SceneThreshold = _settingsManager.Settings.Interpolation.SceneThreshold,
                    RifeModel = _settingsManager.Settings.Interpolation.RifeModel,
                    UhdMode = _settingsManager.Settings.Interpolation.UhdMode,
                    ScalingAlgorithm = _settingsManager.Settings.Interpolation.ScalingAlgorithm
                },
                _ => throw new ArgumentException($"Unknown preset: {preset}")
            };
        }
    }

    public class PerformanceMetrics
    {
        public double CurrentFps { get; set; }
        public int DroppedFrames { get; set; }
        public float GpuUsage { get; set; }
        public int VramUsage { get; set; }
        public double Latency { get; set; }
    }

    public class InterpolationSettings
    {
        public int TargetHeight { get; set; }
        public double SceneThreshold { get; set; }
        public int RifeModel { get; set; }
        public bool UhdMode { get; set; }
        public string ScalingAlgorithm { get; set; } = "spline36";
    }
}
