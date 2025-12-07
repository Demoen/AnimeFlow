using System;

namespace AnimeFlow.Models
{
    public class AppSettings
    {
        public GeneralSettings General { get; set; } = new();
        public VideoSettings Video { get; set; } = new();
        public InterpolationSettingsModel Interpolation { get; set; } = new();
        public AdvancedSettings Advanced { get; set; } = new();
    }

    public class GeneralSettings
    {
        public bool AutoStartInterpolation { get; set; } = true;
        public QualityPreset DefaultQualityPreset { get; set; } = QualityPreset.Balanced;
        public bool CheckUpdatesOnStartup { get; set; } = true;
        public bool RememberWindowState { get; set; } = true;
        public bool ShowOsdMessages { get; set; } = true;
    }

    public class VideoSettings
    {
        public int GpuId { get; set; } = 0;
        public string GpuApi { get; set; } = "vulkan";
        public string Hwdec { get; set; } = "auto-safe";
        public int Volume { get; set; } = 100;
        public bool Deband { get; set; } = true;
    }

    public class InterpolationSettingsModel
    {
        public int TargetHeight { get; set; } = 720;
        public double SceneThreshold { get; set; } = 0.15;
        public int RifeModel { get; set; } = 0;
        public bool UhdMode { get; set; } = false;
        public string ScalingAlgorithm { get; set; } = "spline36";
        public string ModelPath { get; set; } = "models/rife-v4.6";
    }

    public class AdvancedSettings
    {
        public string LogLevel { get; set; } = "Info";
        public int CacheSize { get; set; } = 400;
        public bool EnableExperimentalFeatures { get; set; } = false;
        public bool EnableLogging { get; set; } = true;
        public int MaxLogFiles { get; set; } = 10;
    }

    public enum QualityPreset
    {
        Fast,
        Balanced,
        Beauty,
        Custom
    }
}
