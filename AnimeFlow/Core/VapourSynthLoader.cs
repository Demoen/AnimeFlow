using System;
using System.IO;
using System.Text;
using AnimeFlow.Core;

namespace AnimeFlow.Core
{
    /// <summary>
    /// Generates and manages VapourSynth scripts for interpolation
    /// </summary>
    public class VapourSynthLoader
    {
        // Removed r'' prefix from interpolated path strings to avoid trailing backslash escaping issues
        // We will manually escape backslashes in the injected C# string
        private const string ScriptTemplate = @"import vapoursynth as vs
import sys
import os

core = vs.core

# Paths
animeflow_root = '{{ANIMEFLOW_ROOT}}'
dependencies_path = os.path.join(animeflow_root, 'Dependencies')
vs_path = os.path.join(dependencies_path, 'vapoursynth')
rife_path = os.path.join(dependencies_path, 'rife')
model_path = '{{MODEL_PATH}}'

# Add to Python path
sys.path.insert(0, vs_path)
sys.path.insert(0, os.path.join(vs_path, 'vapoursynth64', 'plugins'))
sys.path.insert(0, os.path.join(vs_path, 'vapoursynth64', 'coreplugins'))

# Try to import RIFE - support both vsrife (Python) and rife (DLL-based) plugins
rife_available = False
try:
    import vsrife
    rife_available = True
    rife_method = 'vsrife'
    core.log_message(vs.MESSAGE_TYPE_INFORMATION, 'Using vsrife (Python-based RIFE)')
except ImportError:
    try:
        # Fallback to DLL-based RIFE plugin
        if hasattr(core, 'rife'):
            rife_available = True
            rife_method = 'dll'
            core.log_message(vs.MESSAGE_TYPE_INFORMATION, 'Using DLL-based RIFE plugin')
    except:
        pass

if not rife_available:
    core.log_message(vs.MESSAGE_TYPE_CRITICAL, 'RIFE plugin not available! Install vsrife or librife.dll')
    raise ImportError('RIFE plugin not found')

# Settings
target_height = {{TARGET_HEIGHT}}
scene_threshold = {{SCENE_THRESHOLD}}
gpu_id = {{GPU_ID}}
rife_model = {{RIFE_MODEL}}
uhd_mode = {{UHD_MODE}}
scaling_algorithm = '{{SCALING_ALGORITHM}}'

def detect_real_fps(clip):
    '''Detect the real framerate of content, accounting for duplicate frames in 60fps containers'''
    src_fps_num = clip.fps.numerator
    src_fps_den = clip.fps.denominator
    src_fps = src_fps_num / src_fps_den
    
    # Check if it's a 60fps stream
    if 59 < src_fps < 61:
        core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Detected 60fps container ({src_fps:.3f}fps) - checking for duplicate frames')
        
        # Sample first 300 frames to detect pattern
        sample_size = min(300, clip.num_frames)
        
        # Use frame difference to detect duplicates
        # Most 24fps content in 60fps containers follows a 3:2 pulldown pattern
        # We'll decimate to recover the original framerate
        
        try:
            # Method 1: Try VDecimate (best for telecined content)
            if hasattr(core, 'vivtc'):
                # First decimate 60->30 by removing every other frame
                clip_30 = core.std.SelectEvery(clip, cycle=2, offsets=0)
                # Then use VDecimate to detect and remove remaining duplicates
                clip_decimated = core.vivtc.VDecimate(clip_30, cycle=5, chroma=True)
                
                # Check if frames were actually removed
                if clip_decimated.num_frames < clip_30.num_frames:
                    core.log_message(vs.MESSAGE_TYPE_INFORMATION, '60fps -> 24fps: Removed duplicate frames (3:2 pulldown detected)')
                    return clip_decimated, 23.976
                else:
                    core.log_message(vs.MESSAGE_TYPE_INFORMATION, '60fps -> 30fps: No pulldown detected, content is 30fps')
                    return clip_30, 30.0
            else:
                # Method 2: Simple decimation (no vivtc available)
                core.log_message(vs.MESSAGE_TYPE_WARNING, 'vivtc not available - using simple decimation')
                # Assume 24fps content (most common for anime)
                # Decimate 60 -> 24 by selecting frames 0,0,1,2,2 pattern
                clip_24 = core.std.SelectEvery(clip, cycle=5, offsets=[0, 2, 4])
                return clip_24, 23.976
        except Exception as e:
            core.log_message(vs.MESSAGE_TYPE_WARNING, f'Frame decimation failed: {e} - using source fps')
            return clip, src_fps
    
    elif 29.5 < src_fps < 30.5:
        core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Detected 30fps stream - checking for 3:2 pulldown')
        try:
            if hasattr(core, 'vivtc'):
                clip_decimated = core.vivtc.VDecimate(clip, cycle=5, chroma=True)
                if clip_decimated.num_frames < clip.num_frames:
                    core.log_message(vs.MESSAGE_TYPE_INFORMATION, '30fps -> 24fps: Removed 3:2 pulldown')
                    return clip_decimated, 23.976
        except Exception as e:
            core.log_message(vs.MESSAGE_TYPE_WARNING, f'Pulldown removal failed: {e}')
        return clip, src_fps
    
    # Native 24/25fps or other
    core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Native framerate detected: {src_fps:.3f}fps')
    return clip, src_fps

def anime_interpolate(clip):
    # Get source properties
    src_width = clip.width
    src_height = clip.height
    
    core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Source: {src_width}x{src_height}')
    
    # Detect real FPS (handle 60fps containers with 24fps content)
    clip, src_fps = detect_real_fps(clip)
    
    # Calculate target FPS and multiplier
    if 23.5 < src_fps < 24.5:
        multiplier = 2.5  # 24 → 60
        target_fps_num = 60
        target_fps_den = 1
        core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Interpolating 24fps -> 60fps (multiplier: {multiplier})')
    elif 29.5 < src_fps < 30.5:
        multiplier = 2  # 30 → 60
        target_fps_num = 60
        target_fps_den = 1
        core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Interpolating 30fps -> 60fps (multiplier: {multiplier})')
    elif 24.5 < src_fps < 25.5:
        multiplier = 2  # 25 → 50 (PAL)
        target_fps_num = 50
        target_fps_den = 1
        core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Interpolating 25fps -> 50fps (multiplier: {multiplier})')
    else:
        multiplier = 2
        target_fps_num = int(src_fps * 2)
        target_fps_den = 1
        core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'Interpolating {src_fps}fps -> {target_fps_num}fps (multiplier: {multiplier})')
    
    # Convert to RGB
    if scaling_algorithm == 'bilinear':
        clip = core.resize.Bilinear(clip, format=vs.RGBS, matrix_in_s='709')
    elif scaling_algorithm == 'lanczos':
        clip = core.resize.Lanczos(clip, format=vs.RGBS, matrix_in_s='709')
    else:  # spline36
        clip = core.resize.Spline36(clip, format=vs.RGBS, matrix_in_s='709')
    
    # Intelligent downscaling
    original_width = src_width
    original_height = src_height
    needs_upscale = False
    
    if src_height > target_height:
        scale_factor = target_height / src_height
        new_width = int(src_width * scale_factor)
        new_height = target_height
        
        new_width = new_width - (new_width % 2)
        new_height = new_height - (new_height % 2)
        
        if scaling_algorithm == 'bilinear':
            clip = core.resize.Bilinear(clip, new_width, new_height)
        elif scaling_algorithm == 'lanczos':
            clip = core.resize.Lanczos(clip, new_width, new_height)
        else:
            clip = core.resize.Spline36(clip, new_width, new_height)
        
        needs_upscale = True
    
    # Scene change detection
    if hasattr(core, 'misc'):
        clip = core.misc.SCDetect(clip, threshold=scene_threshold)
    else:
        core.log_message(vs.MESSAGE_TYPE_WARNING, 'misc plugin not found, skipping scene detection')
    
    # Log initialization success for UI confirmation
    core.log_message(vs.MESSAGE_TYPE_INFORMATION, 'AnimeFlow: Interpolation initialized')

    # RIFE interpolation
    try:
        if rife_method == 'vsrife':
            # Python-based vsrife
            clip = vsrife.RIFE(
                clip,
                model=rife_model,
                fps_num=target_fps_num,
                fps_den=target_fps_den,
                scale=1.0,
                device_index=gpu_id,
                tta=False,
                uhd=uhd_mode,
                sc=True  # Scene change detection
            )
        else:
            # DLL-based RIFE
            clip = core.rife.RIFE(
                clip,
                model=rife_model,
                multiplier=multiplier,
                gpu_id=gpu_id,
                tta=False,
                uhd=uhd_mode,
                sc=True
            )
        core.log_message(vs.MESSAGE_TYPE_INFORMATION, f'RIFE interpolation applied: {target_fps_num}fps')
    except Exception as e:
        core.log_message(vs.MESSAGE_TYPE_CRITICAL, f'RIFE failed: {e}')
        raise
    
    # Upscale back to original resolution
    if needs_upscale:
        if scaling_algorithm == 'bilinear':
            clip = core.resize.Bilinear(clip, original_width, original_height)
        elif scaling_algorithm == 'lanczos':
            clip = core.resize.Lanczos(clip, original_width, original_height)
        else:
            clip = core.resize.Spline36(clip, original_width, original_height)
    
    # Convert back to YUV
    if scaling_algorithm == 'bilinear':
        clip = core.resize.Bilinear(clip, format=vs.YUV420P8, matrix_s='709')
    elif scaling_algorithm == 'lanczos':
        clip = core.resize.Lanczos(clip, format=vs.YUV420P8, matrix_s='709')
    else:
        clip = core.resize.Spline36(clip, format=vs.YUV420P8, matrix_s='709')
    
    # Set output FPS
    clip = core.std.AssumeFPS(clip, fpsnum=target_fps_num, fpsden=target_fps_den)
    
    return clip

# Entry point
# Use video_in provided by mpv
if 'video_in' in globals():
    clip = video_in
    clip = anime_interpolate(clip)
    clip.set_output()
else:
    core.log_message(vs.MESSAGE_TYPE_CRITICAL, 'Error: video_in not found. This script must be run via mpv.')
    raise RuntimeError('video_in not found')
";

        public string GenerateScript(InterpolationSettings settings)
        {
            try
            {
                // Get AnimeFlow root directory
                var animeflowRoot = AppDomain.CurrentDomain.BaseDirectory;
                
                // Truncate trailing backslash if present to avoid escaping issues
                if (animeflowRoot.EndsWith("\\"))
                    animeflowRoot = animeflowRoot.Substring(0, animeflowRoot.Length - 1);

                var modelPath = Path.Combine(animeflowRoot, "Dependencies", "models", "rife-v4.6");

                // Replace placeholders
                // Use double backslash for Python strings
                var script = ScriptTemplate
                    .Replace("{{ANIMEFLOW_ROOT}}", animeflowRoot.Replace("\\", "\\\\"))
                    .Replace("{{MODEL_PATH}}", modelPath.Replace("\\", "\\\\"))
                    .Replace("{{TARGET_HEIGHT}}", settings.TargetHeight.ToString())
                    .Replace("{{SCENE_THRESHOLD}}", settings.SceneThreshold.ToString("F2"))
                    .Replace("{{GPU_ID}}", "0")
                    .Replace("{{RIFE_MODEL}}", settings.RifeModel.ToString())
                    .Replace("{{UHD_MODE}}", settings.UhdMode ? "True" : "False")
                    .Replace("{{SCALING_ALGORITHM}}", settings.ScalingAlgorithm);

                // Create temp directory if it doesn't exist
                var tempDir = Path.Combine(animeflowRoot, "temp");
                Directory.CreateDirectory(tempDir);

                // Generate unique filename
                var scriptPath = Path.Combine(tempDir, $"animeflow_{Guid.NewGuid():N}.vpy");

                // Write script to file
                File.WriteAllText(scriptPath, script, Encoding.UTF8);

                return scriptPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate VapourSynth script: {ex.Message}", ex);
            }
        }

        public bool ValidateScript(string scriptPath)
        {
            if (!File.Exists(scriptPath))
                return false;

            try
            {
                var content = File.ReadAllText(scriptPath);
                
                // Basic validation: check for required elements
                return content.Contains("import vapoursynth") &&
                       (content.Contains("vsrife") || content.Contains("core.rife")) &&
                       content.Contains("clip.set_output()");
            }
            catch
            {
                return false;
            }
        }

        public void CleanupOldScripts()
        {
            try
            {
                var animeflowRoot = AppDomain.CurrentDomain.BaseDirectory;
                var tempDir = Path.Combine(animeflowRoot, "temp");

                if (!Directory.Exists(tempDir))
                    return;

                var files = Directory.GetFiles(tempDir, "animeflow_*.vpy");
                foreach (var file in files)
                {
                    try
                    {
                        // Delete scripts older than 1 hour
                        var fileInfo = new FileInfo(file);
                        if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromHours(1))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Ignore individual file errors
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
