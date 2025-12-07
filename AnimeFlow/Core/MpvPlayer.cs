using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Globalization;
using AnimeFlow.Models;
using System.IO;

namespace AnimeFlow.Core
{
    /// <summary>
    /// Wrapper for libmpv providing video playback functionality
    /// </summary>
    public class MpvPlayer : IDisposable
    {
        private IntPtr _mpvHandle;
        private IntPtr _windowHandle;
        private bool _isInitialized;
        private bool _disposed;
        private System.Windows.Threading.DispatcherTimer? _propertyTimer;
        private int _volume = 100;

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
        public event EventHandler<ErrorEventArgs>? Error;
        public event EventHandler<string>? LogMessage; // New event for logs
        public event EventHandler? FileLoaded; // Fired when a file/URL is fully loaded

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public double Position { get; private set; }
        public double Duration { get; private set; }

        public int Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 100);
                if (_isInitialized && !_disposed)
                {
                    try
                    {
                        SetProperty("volume", _volume.ToString(CultureInfo.InvariantCulture));
                    }
                    catch { /* Ignore error during property set */ }
                }
            }
        }

        #region P/Invoke Declarations

        private const string MpvDll = "libmpv-2.dll";

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mpv_create();

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_initialize(IntPtr mpvHandle);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_command(IntPtr mpvHandle, IntPtr[] args);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_set_property_string(IntPtr mpvHandle, byte[] name, byte[] value);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mpv_get_property_string(IntPtr mpvHandle, byte[] name);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_set_property(IntPtr mpvHandle, byte[] name, int format, ref long data);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_get_property(IntPtr mpvHandle, byte[] name, int format, ref double data);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void mpv_terminate_destroy(IntPtr mpvHandle);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void mpv_free(IntPtr data);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_request_log_messages(IntPtr mpvHandle, byte[] min_level);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mpv_wait_event(IntPtr mpvHandle, double timeout);

        private const int MPV_FORMAT_INT64 = 4;
        private const int MPV_FORMAT_DOUBLE = 5;

        private const int MPV_EVENT_NONE = 0;
        private const int MPV_EVENT_SHUTDOWN = 1;
        private const int MPV_EVENT_LOG_MESSAGE = 2;
        private const int MPV_EVENT_GET_PROPERTY_REPLY = 3;
        private const int MPV_EVENT_SET_PROPERTY_REPLY = 4;
        private const int MPV_EVENT_COMMAND_REPLY = 5;
        private const int MPV_EVENT_START_FILE = 6;
        private const int MPV_EVENT_END_FILE = 7;
        private const int MPV_EVENT_FILE_LOADED = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct mpv_event
        {
            public int event_id;
            public int error;
            public ulong reply_userdata;
            public IntPtr data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct mpv_event_end_file
        {
            public int reason;
            public int error;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct mpv_event_log_message
        {
            public IntPtr prefix;
            public IntPtr level;
            public IntPtr text;
            public int log_level;
        }

        #endregion

        static MpvPlayer()
        {
            // Set up custom DLL resolver to find libmpv in Dependencies folder
            NativeLibrary.SetDllImportResolver(typeof(MpvPlayer).Assembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName == MpvDll)
                {
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    var dllPath = Path.Combine(basePath, "Dependencies", "mpv", MpvDll);
                    
                    if (File.Exists(dllPath) && NativeLibrary.TryLoad(dllPath, out var handle))
                    {
                        return handle;
                    }
                }
                return IntPtr.Zero;
            });

            // Configure VapourSynth environment
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var vsPath = Path.Combine(basePath, "Dependencies", "vapoursynth");
                
                // Add VapourSynth to PATH for DLL discovery
                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                if (!path.Contains(vsPath))
                {
                    Environment.SetEnvironmentVariable("PATH", $"{vsPath};{path}");
                }
            }
            catch { /* Ignore errors in static constructor */ }
        }

        public void Initialize(IntPtr windowHandle)
        {
            if (_isInitialized)
                throw new InvalidOperationException("MpvPlayer already initialized");

            try
            {
                // Store window handle first
                _windowHandle = windowHandle;
                
                // Create mpv instance
                _mpvHandle = mpv_create();
                if (_mpvHandle == IntPtr.Zero)
                    throw new Exception("Failed to create mpv instance");

                // Request log messages (use 'info' instead of 'debug' to reduce spam)
                mpv_request_log_messages(_mpvHandle, GetUtf8Bytes("info"));

                // Configure VapourSynth paths
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var vsPath = Path.Combine(basePath, "Dependencies", "vapoursynth");
                var vsDllPath = Path.Combine(vsPath, "VapourSynth.dll");
                
                // Set VapourSynth library path
                if (File.Exists(vsDllPath))
                {
                    SetOption("vapoursynth-path", vsDllPath.Replace("\\", "\\\\"));
                }

                // CRITICAL: Set window ID BEFORE initializing mpv
                long wid = _windowHandle.ToInt64();
                SetOption("wid", wid.ToString());
                
                // Set options before initialization
                SetOption("vo", "gpu-next");  // Use modern GPU renderer
                SetOption("gpu-api", "d3d11"); // D3D11 for Windows
                SetOption("hwdec", "auto-safe"); // Auto-safe allows software fallback
                SetOption("hwdec-codecs", "h264,hevc,vp9"); // Only hwdec for these codecs (not AV1)
                SetOption("keep-open", "yes");
                SetOption("osc", "no"); // Disable default on-screen controller
                SetOption("input-default-bindings", "no");
                SetOption("input-vo-keyboard", "no");
                
                // Force GPU usage for rendering
                SetOption("gpu-context", "d3d11"); // Use D3D11 context
                SetOption("d3d11-exclusive-fs", "no"); // Windowed mode
                SetOption("gpu-shader-cache-dir", Path.Combine(basePath, "shader_cache")); // Cache shaders
                
                // High-quality rendering options (GPU-accelerated)
                SetOption("scale", "ewa_lanczossharp"); // Best spatial upscaling (GPU)
                SetOption("cscale", "ewa_lanczossharp"); // Best chroma upscaling (GPU)
                SetOption("dscale", "mitchell"); // Good downscaling (GPU)
                SetOption("correct-downscaling", "yes"); // Proper downscaling
                SetOption("sigmoid-upscaling", "yes"); // Better light handling (GPU)
                SetOption("linear-upscaling", "yes"); // Linear light for upscaling
                SetOption("deband", "no"); // Disabled by default (enabled with interpolation)
                
                // Force GPU usage for decoding and rendering
                // Note: Allow software fallback for unsupported codecs (like AV1)
                SetOption("vd-lavc-dr", "auto"); // Auto direct rendering
                SetOption("vd-lavc-software-fallback", "yes"); // CRITICAL: Allow software decode fallback
                SetOption("opengl-pbo", "yes"); // Enable PBO for better GPU performance
                
                // Performance optimizations for smooth playback
                SetOption("video-sync", "audio"); // Default sync mode
                
                // Enhanced caching for YouTube and streaming
                SetOption("cache", "yes"); // Enable cache
                SetOption("cache-on-disk", "no"); // RAM cache is faster
                SetOption("demuxer-max-bytes", "500M"); // Large buffer for streaming
                SetOption("demuxer-max-back-bytes", "150M"); // Larger back buffer
                SetOption("cache-secs", "30"); // 30 second cache for streams
                SetOption("demuxer-readahead-secs", "20"); // Aggressive readahead
                
                // YouTube-specific optimizations
                SetOption("force-seekable", "yes"); // Make streams seekable
                SetOption("stream-buffer-size", "4096k"); // 4MB stream buffer
                
                // Reduce latency but maintain smoothness
                SetOption("audio-buffer", "0.2"); // Balanced audio buffer
                SetOption("audio-stream-silence", "yes"); // Handle audio gaps
                
                // Frame dropping for smoothness (important for streaming)
                SetOption("framedrop", "vo"); // Drop frames if needed to maintain smoothness
                
                // Force black background (prevent white flash)
                SetOption("background", "#000000");
                SetOption("force-window", "yes"); // Force window creation

                // Configure yt-dlp for better YouTube performance
                var toolsPath = Path.Combine(basePath, "Dependencies", "tools");
                var ytdlPath = Path.Combine(toolsPath, "yt-dlp.exe");
                
                OnLogMessage($"Checking for yt-dlp at: {ytdlPath}");
                
                if (File.Exists(ytdlPath))
                {
                    OnLogMessage("yt-dlp found, configuring...");
                    
                    // CRITICAL: Tell mpv exactly where yt-dlp is
                    // Use script-opts to configure ytdl_hook
                    var escapedPath = ytdlPath.Replace("\\", "/"); // Use forward slashes for mpv
                    SetOption("script-opts", $"ytdl_hook-ytdl_path={escapedPath}");
                    
                    OnLogMessage($"yt-dlp path configured: {escapedPath}");
                }
                else
                {
                    OnLogMessage($"WARNING: yt-dlp not found at {ytdlPath}");
                    OnLogMessage("URL playback will not work without yt-dlp!");
                }
                
                // Ensure ytdl hook is enabled
                SetOption("ytdl", "yes");
                OnLogMessage("ytdl hook enabled");
                
                // YouTube-specific quality preferences
                SetOption("ytdl-format", "bestvideo[height<=1080]+bestaudio/best[height<=1080]/best");
                OnLogMessage("ytdl format set to best quality (max 1080p)");
                
                // Add user agent to avoid detection
                SetOption("ytdl-raw-options", "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                OnLogMessage("ytdl user-agent configured");

                // Initialize mpv
                var result = mpv_initialize(_mpvHandle);
                if (result < 0)
                    throw new Exception($"Failed to initialize mpv: {result}");

                _isInitialized = true;

                // Set initial volume
                SetProperty("volume", _volume.ToString(CultureInfo.InvariantCulture));

                // Start property update timer
                _propertyTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100) // Balanced poll rate
                };
                _propertyTimer.Tick += (s, e) => {
                    UpdateProperties();
                    ProcessEvents();
                };
                _propertyTimer.Start();
                
                OnLogMessage($"MpvPlayer initialized successfully with window handle {windowHandle}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize mpv player: {ex.Message}", ex);
            }
        }

        public void LoadFile(string path)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("MpvPlayer not initialized");

            try
            {
                OnLogMessage($"LoadFile: {path}");
                
                var args = new[]
                {
                    GetUtf8Ptr("loadfile"),
                    GetUtf8Ptr(path),
                    IntPtr.Zero
                };

                var result = mpv_command(_mpvHandle, args);
                
                foreach (var ptr in args)
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }

                if (result < 0)
                {
                    var error = $"Failed to load file (mpv error {result})";
                    OnLogMessage(error);
                    throw new Exception(error);
                }

                OnLogMessage("LoadFile command sent successfully");
                IsPlaying = true;
                IsPaused = false;
                OnStateChanged(PlaybackState.Playing);
            }
            catch (Exception ex)
            {
                OnLogMessage($"LoadFile exception: {ex}");
                OnError($"Failed to load file: {ex.Message}");
                throw;
            }
        }

        public void Play()
        {
            SetProperty("pause", "no");
            IsPlaying = true;
            IsPaused = false;
            OnStateChanged(PlaybackState.Playing);
        }

        public void Pause()
        {
            SetProperty("pause", "yes");
            IsPlaying = false;
            IsPaused = true;
            OnStateChanged(PlaybackState.Paused);
        }

        public void Stop()
        {
            var args = new[]
            {
                GetUtf8Ptr("stop"),
                IntPtr.Zero
            };

            mpv_command(_mpvHandle, args);

            foreach (var ptr in args)
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }

            IsPlaying = false;
            IsPaused = false;
            Position = 0;
            OnStateChanged(PlaybackState.Stopped);
        }

        public void Seek(double seconds)
        {
            var args = new[]
            {
                GetUtf8Ptr("seek"),
                GetUtf8Ptr(seconds.ToString(CultureInfo.InvariantCulture)),
                IntPtr.Zero
            };

            mpv_command(_mpvHandle, args);

            foreach (var ptr in args)
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        public void SeekAbsolute(double seconds)
        {
            var args = new[]
            {
                GetUtf8Ptr("seek"),
                GetUtf8Ptr(seconds.ToString(CultureInfo.InvariantCulture)),
                GetUtf8Ptr("absolute"),
                IntPtr.Zero
            };

            mpv_command(_mpvHandle, args);

            foreach (var ptr in args)
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        public void ShowOsdText(string text, int durationMs = 3000)
        {
            if (_mpvHandle == IntPtr.Zero) return;

            var args = new[]
            {
                GetUtf8Ptr("show-text"),
                GetUtf8Ptr(text),
                GetUtf8Ptr(durationMs.ToString(CultureInfo.InvariantCulture)),
                IntPtr.Zero
            };

            mpv_command(_mpvHandle, args);

            foreach (var ptr in args)
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        public void ApplyVapourSynthFilter(string scriptPath)
        {
            try
            {
                // Escape backslashes for MPV property argument
                var escapedPath = scriptPath.Replace("\\", "\\\\");
                var filterString = $"vapoursynth=\"{escapedPath}\"";
                
                // Log the filter being applied
                OnLogMessage($"Applying VapourSynth filter: {filterString}");
                OnLogMessage($"Script path: {scriptPath}");
                
                SetProperty("vf", filterString);
                
                // Verify the filter was applied
                OnLogMessage("VapourSynth filter applied successfully");
            }
            catch (Exception ex)
            {
                OnError($"Failed to apply VapourSynth filter: {ex.Message}");
                OnLogMessage($"Filter error details: {ex}");
            }
        }

        public void RemoveVapourSynthFilter()
        {
            try
            {
                SetProperty("vf", "");
            }
            catch (Exception ex)
            {
                OnError($"Failed to remove VapourSynth filter: {ex.Message}");
            }
        }

        public void EnableInterpolation(QualityPreset preset = QualityPreset.Balanced)
        {
            try
            {
                OnLogMessage($"Enabling high-quality 60fps GPU interpolation with preset: {preset}");
                
                // Use display-resample for smoothest interpolation to display refresh rate
                SetProperty("video-sync", "display-resample");
                SetProperty("interpolation", "yes");
                
                // Enable motion interpolation for smoother results
                try
                {
                    SetProperty("video-sync-max-video-change", "10"); // Tolerant for streams
                    SetProperty("video-sync-max-audio-change", "0.1"); // Keep audio in sync
                }
                catch { }
                
                // Configure high-quality temporal interpolation based on preset
                switch (preset)
                {
                    case QualityPreset.Fast:
                        // Fast: Simple but smooth
                        SetProperty("tscale", "linear");
                        try
                        {
                            SetProperty("tscale-clamp", "0.0");
                        }
                        catch { }
                        // Enable light debanding for GPU load
                        EnableDebanding(light: true);
                        break;
                        
                    case QualityPreset.Beauty:
                        // Beauty: Maximum quality with advanced filtering (high GPU usage)
                        SetProperty("tscale", "mitchell");
                        try
                        {
                            SetProperty("tscale-antiring", "1.0"); // Maximum antiring
                            SetProperty("tscale-clamp", "0.0");
                            SetProperty("tscale-blur", "0.9"); // Slight blur for smoother motion
                            SetProperty("tscale-radius", "3.0"); // Larger radius for quality
                        }
                        catch { }
                        // Enable full debanding for maximum quality
                        EnableDebanding(light: false);
                        break;
                        
                    case QualityPreset.Balanced:
                    default:
                        // Balanced: Oversample is best for anime (zero-latency, perfect for 24→60fps)
                        SetProperty("tscale", "oversample");
                        // Enable moderate debanding
                        EnableDebanding(light: true);
                        break;
                }
                
                // Additional quality improvements (GPU-accelerated)
                try
                {
                    // Enable better frame blending
                    SetProperty("blend-subtitles", "video");
                }
                catch { }
                
                OnLogMessage("High-quality 60fps GPU interpolation enabled successfully");
            }
            catch (Exception ex)
            {
                OnError($"Failed to enable interpolation: {ex.Message}");
                throw;
            }
        }

        private void EnableDebanding(bool light)
        {
            try
            {
                SetProperty("deband", "yes"); // Enable debanding (GPU load)
                
                if (light)
                {
                    // Light debanding (lower GPU usage)
                    SetProperty("deband-iterations", "2");
                    SetProperty("deband-threshold", "32");
                    SetProperty("deband-range", "16");
                    SetProperty("deband-grain", "24"); // Add grain to hide banding
                }
                else
                {
                    // Full debanding (higher GPU usage)
                    SetProperty("deband-iterations", "4");
                    SetProperty("deband-threshold", "48");
                    SetProperty("deband-range", "24");
                    SetProperty("deband-grain", "48"); // More grain
                }
            }
            catch
            {
                // Ignore if debanding not supported
            }
        }

        public void DisableInterpolation()
        {
            try
            {
                OnLogMessage("Disabling interpolation...");
                
                // Disable interpolation
                SetProperty("interpolation", "no");
                SetProperty("video-sync", "audio");
                
                // Disable debanding to reduce GPU load
                try
                {
                    SetProperty("deband", "no");
                    SetProperty("blend-subtitles", "no");
                }
                catch { }
                
                OnLogMessage("Interpolation disabled successfully");
            }
            catch (Exception ex)
            {
                OnError($"Failed to disable interpolation: {ex.Message}");
                throw;
            }
        }

        public double GetEstimatedVfFps()
        {
            if (!_isInitialized || _disposed)
                return 0.0;

            try
            {
                // Query estimated-vf-fps property from mpv
                double fps = 0.0;
                mpv_get_property(_mpvHandle, GetUtf8Bytes("estimated-vf-fps"), MPV_FORMAT_DOUBLE, ref fps);
                return fps;
            }
            catch
            {
                return 0.0;
            }
        }

        public double GetContainerFps()
        {
            if (!_isInitialized || _disposed)
                return 0.0;

            try
            {
                // Query container fps property from mpv
                double fps = 0.0;
                mpv_get_property(_mpvHandle, GetUtf8Bytes("container-fps"), MPV_FORMAT_DOUBLE, ref fps);
                return fps;
            }
            catch
            {
                return 0.0;
            }
        }

        public VideoInfo GetVideoInfo()
        {
            if (!_isInitialized || _disposed)
                return new VideoInfo();

            try
            {
                var info = new VideoInfo();
                
                // Get container FPS
                double containerFps = 0.0;
                mpv_get_property(_mpvHandle, GetUtf8Bytes("container-fps"), MPV_FORMAT_DOUBLE, ref containerFps);
                info.ContainerFps = containerFps;
                
                // Get estimated FPS
                double estimatedFps = 0.0;
                mpv_get_property(_mpvHandle, GetUtf8Bytes("estimated-vf-fps"), MPV_FORMAT_DOUBLE, ref estimatedFps);
                info.EstimatedFps = estimatedFps > 0 ? estimatedFps : containerFps;
                
                // Get resolution
                info.Width = GetPropertyString("width");
                info.Height = GetPropertyString("height");
                
                // Get codec
                info.VideoCodec = GetPropertyString("video-codec");
                info.AudioCodec = GetPropertyString("audio-codec");
                
                // Detect if it's likely a 60fps container with lower fps content
                if (containerFps >= 59.0 && containerFps <= 61.0)
                {
                    info.IsLikely60FpsContainer = true;
                    // In 60fps containers, the actual content is often 24/30fps
                    // We'll let VapourSynth handle detection
                }
                
                return info;
            }
            catch
            {
                return new VideoInfo();
            }
        }

        private void UpdateProperties()
        {
            if (!_isInitialized || _disposed)
                return;

            try
            {
                // Update position
                double pos = 0;
                mpv_get_property(_mpvHandle, GetUtf8Bytes("time-pos"), MPV_FORMAT_DOUBLE, ref pos);
                Position = pos;

                // Update duration
                double dur = 0;
                mpv_get_property(_mpvHandle, GetUtf8Bytes("duration"), MPV_FORMAT_DOUBLE, ref dur);
                Duration = dur;
            }
            catch
            {
                // Ignore property update errors
            }
        }

        private void ProcessEvents()
        {
            if (!_isInitialized || _disposed) return;

            while (true)
            {
                // Timeout 0 to return immediately if no events
                var eventPtr = mpv_wait_event(_mpvHandle, 0); 
                if (eventPtr == IntPtr.Zero) break;

                var eventStruct = Marshal.PtrToStructure<mpv_event>(eventPtr);
                if (eventStruct.event_id == MPV_EVENT_NONE) break;

                switch (eventStruct.event_id)
                {
                    case MPV_EVENT_LOG_MESSAGE:
                        if (eventStruct.data != IntPtr.Zero)
                        {
                            var logMsg = Marshal.PtrToStructure<mpv_event_log_message>(eventStruct.data);
                            string text = Marshal.PtrToStringUTF8(logMsg.text) ?? "";
                            string prefix = Marshal.PtrToStringUTF8(logMsg.prefix) ?? "";
                            string level = Marshal.PtrToStringUTF8(logMsg.level) ?? "";
                            
                            // Fire log event
                            LogMessage?.Invoke(this, $"[{prefix}] {text}".Trim());
                        }
                        break;
                    
                    case MPV_EVENT_START_FILE:
                        OnLogMessage("MPV_EVENT_START_FILE - Starting file load");
                        break;
                    
                    case MPV_EVENT_FILE_LOADED:
                        // File/URL has been loaded and is ready for playback
                        OnLogMessage("MPV_EVENT_FILE_LOADED - File loaded successfully");
                        FileLoaded?.Invoke(this, EventArgs.Empty);
                        break;
                    
                    case MPV_EVENT_END_FILE:
                        // Check for errors - this is critical for debugging!
                        if (eventStruct.data != IntPtr.Zero)
                        {
                            var endFileData = Marshal.PtrToStructure<mpv_event_end_file>(eventStruct.data);
                            if (endFileData.error != 0)
                            {
                                // Map common error codes
                                string errorMsg = endFileData.error switch
                                {
                                    -2 => "Generic error",
                                    -4 => "Network error / URL unreachable",
                                    -6 => "Unsupported format",
                                    -10 => "File not found",
                                    _ => $"Error code {endFileData.error}"
                                };
                                
                                OnLogMessage($"MPV_EVENT_END_FILE - ERROR: {errorMsg} (reason: {endFileData.reason})");
                                OnError($"Playback failed: {errorMsg}. Check if yt-dlp is working.");
                            }
                            else
                            {
                                OnLogMessage($"MPV_EVENT_END_FILE - Normal end (reason: {endFileData.reason})");
                            }
                        }
                        else
                        {
                            if (eventStruct.error != 0)
                            {
                                OnLogMessage($"MPV_EVENT_END_FILE - Event error: {eventStruct.error}");
                                OnError($"Playback ended with error: {eventStruct.error}");
                            }
                            else
                            {
                                OnLogMessage("MPV_EVENT_END_FILE - Normal end (no data)");
                            }
                        }
                        break;
                }
            }
        }

        private void SetOption(string name, string value)
        {
            mpv_set_property_string(_mpvHandle, GetUtf8Bytes(name), GetUtf8Bytes(value));
        }

        private void SetOption(string name, long value)
        {
            mpv_set_property(_mpvHandle, GetUtf8Bytes(name), MPV_FORMAT_INT64, ref value);
        }

        private void SetProperty(string name, string value)
        {
            var result = mpv_set_property_string(_mpvHandle, GetUtf8Bytes(name), GetUtf8Bytes(value));
            if (result < 0)
            {
                var error = $"Failed to set property '{name}' to '{value}': error code {result}";
                OnLogMessage(error);
                throw new Exception(error);
            }
        }

        private void SetProperty(string name, ref long value)
        {
            mpv_set_property(_mpvHandle, GetUtf8Bytes(name), MPV_FORMAT_INT64, ref value);
        }

        private string GetPropertyString(string name)
        {
            var ptr = mpv_get_property_string(_mpvHandle, GetUtf8Bytes(name));
            if (ptr == IntPtr.Zero)
                return string.Empty;

            var result = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
            mpv_free(ptr);
            return result;
        }

        private static byte[] GetUtf8Bytes(string str)
        {
            return System.Text.Encoding.UTF8.GetBytes(str + "\0");
        }

        private static IntPtr GetUtf8Ptr(string str)
        {
            var bytes = GetUtf8Bytes(str);
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        private void OnStateChanged(PlaybackState state)
        {
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(state));
        }

        private void OnError(string message)
        {
            Error?.Invoke(this, new ErrorEventArgs(message));
        }

        private void OnLogMessage(string message)
        {
            LogMessage?.Invoke(this, message);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                // Stop the update timer first
                if (_propertyTimer != null)
                {
                    _propertyTimer.Stop();
                    _propertyTimer = null;
                }

                // Stop playback before destroying
                if (_mpvHandle != IntPtr.Zero && _isInitialized)
                {
                    try
                    {
                        var args = new[] { GetUtf8Ptr("quit"), IntPtr.Zero };
                        mpv_command(_mpvHandle, args);
                        foreach (var ptr in args)
                        {
                            if (ptr != IntPtr.Zero)
                                Marshal.FreeHGlobal(ptr);
                        }

                        // Small delay to allow mpv to shutdown gracefully
                        System.Threading.Thread.Sleep(50);
                    }
                    catch { /* Ignore errors during shutdown */ }

                    // Terminate and destroy mpv handle
                    try
                    {
                        mpv_terminate_destroy(_mpvHandle);
                        _mpvHandle = IntPtr.Zero;
                    }
                    catch { /* Ignore errors during termination */ }
                }
            }
            catch { /* Ignore all errors during disposal */ }
            
            GC.SuppressFinalize(this);
        }

        ~MpvPlayer()
        {
            // Don't call Dispose from finalizer to avoid cross-thread issues
        }
    }

    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public PlaybackState NewState { get; }

        public PlaybackStateChangedEventArgs(PlaybackState newState)
        {
            NewState = newState;
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        public string Message { get; }

        public ErrorEventArgs(string message)
        {
            Message = message;
        }
    }

    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    public class VideoInfo
    {
        public double ContainerFps { get; set; }
        public double EstimatedFps { get; set; }
        public string Width { get; set; } = "0";
        public string Height { get; set; } = "0";
        public string VideoCodec { get; set; } = "";
        public string AudioCodec { get; set; } = "";
        public bool IsLikely60FpsContainer { get; set; }
    }
}
