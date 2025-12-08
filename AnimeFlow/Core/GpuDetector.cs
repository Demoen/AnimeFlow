using System;
using System.Management;
using System.Runtime.InteropServices;

namespace AnimeFlow.Core
{
    /// <summary>
    /// Detects GPU capabilities and recommends optimal settings
    /// </summary>
    public class GpuDetector
    {
        public GpuInfo DetectGpu()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "Unknown";
                    
                    // Check if it's an NVIDIA GPU
                    if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    {
                        var vram = Convert.ToInt64(obj["AdapterRAM"] ?? 0) / (1024 * 1024); // Convert to MB
                        
                        return new GpuInfo
                        {
                            Name = name,
                            VramMB = vram,
                            Vendor = GpuVendor.NVIDIA,
                            Tier = DetermineGpuTier(name),
                            HasVulkanSupport = CheckVulkanSupport()
                        };
                    }
                }

                // No NVIDIA GPU found
                return new GpuInfo
                {
                    Name = "Unknown",
                    VramMB = 0,
                    Vendor = GpuVendor.Unknown,
                    Tier = GpuTier.Unsupported,
                    HasVulkanSupport = false
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to detect GPU: {ex.Message}", ex);
            }
        }

        public bool HasVulkanSupport()
        {
            return CheckVulkanSupport();
        }

        public GpuTier GetGpuTier()
        {
            var gpuInfo = DetectGpu();
            return gpuInfo.Tier;
        }

        public float GetGpuUsage()
        {
            // This would require NVML (NVIDIA Management Library) or similar
            // For now, return a placeholder value
            return 0.0f;
        }

        public InterpolationSettings GetRecommendedSettings()
        {
            var gpuInfo = DetectGpu();

            return gpuInfo.Tier switch
            {
                GpuTier.Entry => new InterpolationSettings
                {
                    TargetHeight = 540,
                    SceneThreshold = 0.20,
                    RifeModel = 1, // Lite model
                    UhdMode = false,
                    ScalingAlgorithm = "bilinear"
                },
                GpuTier.Mid => new InterpolationSettings
                {
                    TargetHeight = 720,
                    SceneThreshold = 0.15,
                    RifeModel = 0, // Full model
                    UhdMode = false,
                    ScalingAlgorithm = "spline36"
                },
                GpuTier.High => new InterpolationSettings
                {
                    TargetHeight = 1080,
                    SceneThreshold = 0.10,
                    RifeModel = 0, // Full model
                    UhdMode = false,
                    ScalingAlgorithm = "lanczos"
                },
                _ => new InterpolationSettings
                {
                    TargetHeight = 540,
                    SceneThreshold = 0.20,
                    RifeModel = 1,
                    UhdMode = false,
                    ScalingAlgorithm = "bilinear"
                }
            };
        }

        private GpuTier DetermineGpuTier(string gpuName)
        {
            var name = gpuName.ToUpperInvariant();

            // RTX 40 series
            if (name.Contains("RTX 40"))
                return GpuTier.High;

            // RTX 30 series (high-end)
            if (name.Contains("RTX 3090") || name.Contains("RTX 3080") || 
                name.Contains("RTX 3070") || name.Contains("RTX 4070") ||
                name.Contains("RTX 4080") || name.Contains("RTX 4090"))
                return GpuTier.High;

            // RTX 30 series (mid-range)
            if (name.Contains("RTX 3060") || name.Contains("RTX 3050") ||
                name.Contains("RTX 4060") || name.Contains("RTX 4050"))
                return GpuTier.Mid;

            // RTX 20 series
            if (name.Contains("RTX 20"))
                return GpuTier.Entry;

            // GTX 16 series
            if (name.Contains("GTX 16"))
                return GpuTier.Entry;

            // Older or unknown
            return GpuTier.Unsupported;
        }

        private bool CheckVulkanSupport()
        {
            try
            {
                // Try to load Vulkan DLL
                var vulkanDll = LoadLibrary("vulkan-1.dll");
                if (vulkanDll != IntPtr.Zero)
                {
                    FreeLibrary(vulkanDll);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
    }

    public class GpuInfo
    {
        public string Name { get; set; } = "Unknown";
        public long VramMB { get; set; }
        public GpuVendor Vendor { get; set; }
        public GpuTier Tier { get; set; }
        public bool HasVulkanSupport { get; set; }
    }

    public enum GpuVendor
    {
        Unknown,
        NVIDIA,
        AMD,
        Intel
    }

    public enum GpuTier
    {
        Unsupported,
        Entry,    // RTX 2060, GTX 1660 Ti
        Mid,      // RTX 3060, 4060
        High      // RTX 3070+, 4070+
    }
}
