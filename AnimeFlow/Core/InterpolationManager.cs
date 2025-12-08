using System;
using System.IO;
using AnimeFlow.Models;
using AnimeFlow.Services;

namespace AnimeFlow.Core
{
    /// <summary>
    /// Manages RIFE-based AI interpolation state and quality presets
    /// </summary>
    public class InterpolationManager
    {
        private readonly MpvPlayer _mpvPlayer;
        private readonly SettingsManager _settingsManager;
        private readonly VapourSynthLoader _vsLoader;
        private bool _isEnabled;
        private QualityPreset _currentPreset;
        private string? _currentScriptPath;

        public bool IsEnabled => _isEnabled;
        public QualityPreset CurrentPreset => _currentPreset;

        public InterpolationManager(MpvPlayer mpvPlayer, SettingsManager settingsManager)
        {
            _mpvPlayer = mpvPlayer ?? throw new ArgumentNullException(nameof(mpvPlayer));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _vsLoader = new VapourSynthLoader();
            _currentPreset = settingsManager.Settings.General.DefaultQualityPreset;
            
            // Cleanup old VapourSynth scripts on startup
            _vsLoader.CleanupOldScripts();
        }

        public void Enable()
        {
            if (_isEnabled)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[InterpolationManager] Enabling RIFE AI interpolation...");
                
                // Get interpolation settings based on current preset
                var settings = GetInterpolationSettings(_currentPreset);
                
                // Generate VapourSynth script with anime-optimized RIFE configuration
                _currentScriptPath = _vsLoader.GenerateScript(settings);
                
                System.Diagnostics.Debug.WriteLine($"[InterpolationManager] VapourSynth script generated: {_currentScriptPath}");
                
                // Validate the script
                if (!_vsLoader.ValidateScript(_currentScriptPath))
                {
                    throw new Exception("Generated VapourSynth script validation failed");
                }
                
                // Apply VapourSynth filter to mpv
                _mpvPlayer.ApplyVapourSynthFilter(_currentScriptPath);

                _isEnabled = true;
                System.Diagnostics.Debug.WriteLine("[InterpolationManager] RIFE AI interpolation enabled successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterpolationManager] Error: {ex}");
                
                // Cleanup script if it was created
                if (_currentScriptPath != null && File.Exists(_currentScriptPath))
                {
                    try { File.Delete(_currentScriptPath); } catch { }
                    _currentScriptPath = null;
                }
                
                throw new Exception($"Failed to enable RIFE interpolation: {ex.Message}", ex);
            }
        }

        public void Disable()
        {
            if (!_isEnabled)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[InterpolationManager] Disabling RIFE interpolation...");
                
                // Remove VapourSynth filter from mpv
                _mpvPlayer.RemoveVapourSynthFilter();
                
                // Delete the temporary VapourSynth script
                if (_currentScriptPath != null && File.Exists(_currentScriptPath))
                {
                    try
                    {
                        File.Delete(_currentScriptPath);
                        System.Diagnostics.Debug.WriteLine($"[InterpolationManager] Deleted script: {_currentScriptPath}");
                    }
                    catch (Exception delEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[InterpolationManager] Failed to delete script: {delEx.Message}");
                    }
                    _currentScriptPath = null;
                }
                
                _isEnabled = false;
                System.Diagnostics.Debug.WriteLine("[InterpolationManager] RIFE interpolation disabled successfully");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disable RIFE interpolation: {ex.Message}", ex);
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
                System.Diagnostics.Debug.WriteLine($"[InterpolationManager] Changing preset to: {preset}");
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
                    RifeModel = 18, // rife-v4.6 lite model
                    UhdMode = false,
                    ScalingAlgorithm = "bilinear"
                },
                QualityPreset.Balanced => new InterpolationSettings
                {
                    TargetHeight = 720,
                    SceneThreshold = 0.15,
                    RifeModel = 15, // rife-v4.6 full model (anime-optimized)
                    UhdMode = false,
                    ScalingAlgorithm = "spline36"
                },
                QualityPreset.Beauty => new InterpolationSettings
                {
                    TargetHeight = 1080,
                    SceneThreshold = 0.10,
                    RifeModel = 15, // rife-v4.6 full model
                    UhdMode = true, // Enable UHD mode for high-res content
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
