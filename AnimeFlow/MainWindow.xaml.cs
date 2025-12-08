using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AnimeFlow.Core;
using AnimeFlow.Models;
using AnimeFlow.Services;
using Microsoft.Win32;
using System.Threading.Tasks;

// Alias WinForms to avoid conflicts
using WinForms = System.Windows.Forms;

namespace AnimeFlow
{
    public partial class MainWindow : Window
    {
        private MpvPlayer? _mpvPlayer;
        private InterpolationManager? _interpolationManager;
        private SettingsManager? _settingsManager;
        private readonly DispatcherTimer _updateTimer;
        private bool _isSeeking = false;
        private WinForms.Panel? _videoPanel;
        private bool _pendingInterpolationEnable = false; // Track if interpolation should be enabled after load

        public MainWindow()
        {
            InitializeComponent();

            // Initialize update timer for UI updates
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // Reduced frequency for better performance
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize settings
                _settingsManager = new SettingsManager();
                await _settingsManager.LoadAsync();

                // Hide video container initially (show placeholder instead)
                VideoContainer.Visibility = Visibility.Collapsed;

                // Create WinForms Panel for MPV with black background
                _videoPanel = new WinForms.Panel
                {
                    Dock = WinForms.DockStyle.Fill,
                    BackColor = System.Drawing.Color.Black
                };
                
                // Set the WindowsFormsHost background to black
                VideoHostHost.Background = System.Windows.Media.Brushes.Black;
                VideoContainer.Background = System.Windows.Media.Brushes.Black;
                
                // Add panel to host
                VideoHostHost.Child = _videoPanel;
                
                // Force the panel to create its handle immediately
                var handle = _videoPanel.Handle;
                
                // Small delay to ensure WinForms control is fully initialized
                await Task.Delay(50);

                // Initialize mpv player
                _mpvPlayer = new MpvPlayer();
                _mpvPlayer.StateChanged += MpvPlayer_StateChanged;
                _mpvPlayer.Error += MpvPlayer_Error;
                _mpvPlayer.LogMessage += MpvPlayer_LogMessage;
                _mpvPlayer.FileLoaded += MpvPlayer_FileLoaded;
                
                // Initialize with the Panel's handle
                _mpvPlayer.Initialize(handle);

                // Initialize interpolation manager
                _interpolationManager = new InterpolationManager(_mpvPlayer, _settingsManager);

                // Note: No dependency check needed - using mpv's built-in interpolation
                // which requires no Python or external plugins

                // Apply saved settings
                ApplySettings();

                // Start update timer
                _updateTimer.Start();

                UpdateStatus("Ready");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to initialize AnimeFlow:\n{ex.Message}", 
                    "Initialization Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Stop timer first
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                }

                // Disable interpolation before disposing
                if (_interpolationManager != null && _interpolationManager.IsEnabled)
                {
                    try
                    {
                        _interpolationManager.Disable();
                    }
                    catch { /* Ignore errors during cleanup */ }
                }

                // Dispose mpv player
                if (_mpvPlayer != null)
                {
                    try
                    {
                        _mpvPlayer.Dispose();
                    }
                    catch { /* Ignore errors during disposal */ }
                }

                // Save settings
                if (_settingsManager != null)
                {
                    try
                    {
                        _settingsManager.SaveAsync().Wait(TimeSpan.FromSeconds(2));
                    }
                    catch { /* Ignore settings save errors */ }
                }
            }
            catch
            {
                // Ignore all errors during window closing to prevent crash
            }
        }

        #region File Operations

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Video File",
                Filter = "Video Files|*.mkv;*.mp4;*.avi;*.webm;*.mov;*.flv;*.wmv;*.m4v;*.m4v|All Files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                LoadFile(dialog.FileName);
            }
        }

        private void OpenURL_Click(object sender, RoutedEventArgs e)
        {
            var urlDialog = new URLInputDialog();
            if (urlDialog.ShowDialog() == true)
            {
                LoadFile(urlDialog.Url);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                // Always hide placeholder and show video container when loading
                PlaceholderOverlay.Visibility = Visibility.Collapsed;
                VideoContainer.Visibility = Visibility.Visible;
                
                // Log what we're trying to load
                var isUrl = Uri.IsWellFormedUriString(path, UriKind.Absolute);
                var isFile = System.IO.File.Exists(path);
                
                OnLogMessage($"LoadFile called with: {path}");
                OnLogMessage($"Is URL: {isUrl}, Is File: {isFile}");
                
                // Direct load for valid path or URL
                // MpvPlayer now handles yt-dlp resolution internally via script-opts
                if (isFile || isUrl)
                {
                    var displayName = isUrl ? path : System.IO.Path.GetFileName(path);
                    UpdateStatus($"Loading: {displayName}");
                    
                    OnLogMessage($"Attempting to load: {path}");
                    _mpvPlayer?.LoadFile(path);
                    
                    // Show loading message for URLs (they take longer)
                    if (isUrl)
                    {
                        ShowOsdMessage("Loading stream... Please wait");
                        UpdateStatus("Fetching stream from URL...");
                    }
                    
                    // Set pending flag for auto-interpolation
                    // Interpolation will be enabled after FILE_LOADED event
                    if (_settingsManager?.Settings.General.AutoStartInterpolation == true)
                    {
                        _pendingInterpolationEnable = true;
                        OnLogMessage("Auto-interpolation will be enabled after load");
                    }
                }
                else
                {
                    OnLogMessage($"Invalid path/URL: {path}");
                    ShowOsdMessage("Invalid file or URL");
                    // Restore placeholder if load failed
                    PlaceholderOverlay.Visibility = Visibility.Visible;
                    VideoContainer.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                OnLogMessage($"LoadFile error: {ex}");
                ShowOsdMessage($"Error loading file: {ex.Message}");
                UpdateStatus("Error");
                // Restore placeholder on error
                PlaceholderOverlay.Visibility = Visibility.Visible;
                VideoContainer.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Playback Controls

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_mpvPlayer == null) return;

            if (_mpvPlayer.IsPaused)
            {
                _mpvPlayer.Play();
                PlayPauseButton.Content = "⏸";
            }
            else
            {
                _mpvPlayer.Pause();
                PlayPauseButton.Content = "▶";
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _mpvPlayer?.Stop();
            PlayPauseButton.Content = "▶";
            SeekSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
        }

        private void SeekBackward_Click(object sender, RoutedEventArgs e)
        {
            _mpvPlayer?.Seek(-10);
        }

        private void SeekForward_Click(object sender, RoutedEventArgs e)
        {
            _mpvPlayer?.Seek(10);
        }

        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isSeeking = true;
        }

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isSeeking = false;
            if (_mpvPlayer != null)
            {
                var position = SeekSlider.Value / 100.0 * _mpvPlayer.Duration;
                _mpvPlayer.SeekAbsolute(position);
            }
        }

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isSeeking && _mpvPlayer != null)
            {
                var position = SeekSlider.Value / 100.0 * _mpvPlayer.Duration;
                CurrentTimeText.Text = FormatTime(position);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_mpvPlayer != null)
            {
                _mpvPlayer.Volume = (int)VolumeSlider.Value;
                VolumeText.Text = $"{(int)VolumeSlider.Value}%";
            }
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
            }
            else
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
        }

        #endregion

        #region Interpolation

        private void ToggleInterpolation_Click(object sender, RoutedEventArgs e)
        {
            if (_interpolationManager == null) return;

            try 
            {
                if (_interpolationManager.IsEnabled)
                {
                    _interpolationManager.Disable();
                    ToggleInterpolationButton.Content = "Enable Interpolation";
                    InterpolationStatusText.Text = "Interpolation: OFF";
                    InterpolationStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                    ShowOsdMessage("Interpolation disabled");
                }
                else
                {
                    _interpolationManager.Enable();
                    ToggleInterpolationButton.Content = "Disable Interpolation";
                    InterpolationStatusText.Text = "Interpolation: ON (RIFE AI)";
                    InterpolationStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    ShowOsdMessage("RIFE AI Interpolation enabled");
                }
            }
            catch (Exception ex)
            {
                ShowOsdMessage($"Interpolation Error: {ex.Message}");
                UpdateStatus("Interpolation Error");
            }
        }

        private void QualityPreset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_interpolationManager == null) return;

            var preset = QualityPresetCombo.SelectedIndex switch
            {
                0 => QualityPreset.Fast,
                1 => QualityPreset.Balanced,
                2 => QualityPreset.Beauty,
                3 => QualityPreset.Custom,
                _ => QualityPreset.Balanced
            };

            _interpolationManager.SetPreset(preset);
            ShowOsdMessage($"Quality preset: {preset}");
        }

        #endregion

        #region Settings

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settingsManager);
            if (settingsWindow.ShowDialog() == true)
            {
                ApplySettings();
            }
        }

        private void ApplySettings()
        {
            if (_settingsManager == null) return;

            // Apply quality preset
            QualityPresetCombo.SelectedIndex = _settingsManager.Settings.General.DefaultQualityPreset switch
            {
                QualityPreset.Fast => 0,
                QualityPreset.Balanced => 1,
                QualityPreset.Beauty => 2,
                QualityPreset.Custom => 3,
                _ => 1
            };

            // Apply volume
            VolumeSlider.Value = _settingsManager.Settings.Video.Volume;
        }

        #endregion

        #region UI Updates

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_mpvPlayer == null) return;

            try
            {
                // Update playback position (only if not seeking to avoid jumpiness)
                if (!_isSeeking && _mpvPlayer.Duration > 0)
                {
                    var progress = (_mpvPlayer.Position / _mpvPlayer.Duration) * 100;
                    if (Math.Abs(SeekSlider.Value - progress) > 0.5) // Only update if changed significantly
                    {
                        SeekSlider.Value = progress;
                    }
                    CurrentTimeText.Text = FormatTime(_mpvPlayer.Position);
                    DurationText.Text = FormatTime(_mpvPlayer.Duration);
                }

                // Update FPS and video info
                if (_interpolationManager != null && _mpvPlayer.IsPlaying)
                {
                    var videoInfo = _mpvPlayer.GetVideoInfo();
                    var currentFps = videoInfo.EstimatedFps > 0 ? videoInfo.EstimatedFps : videoInfo.ContainerFps;
                    
                    // Show actual measured FPS
                    FpsText.Text = $"FPS: {currentFps:F1}";
                    
                    // Show interpolation status with more detail
                    if (_interpolationManager.IsEnabled)
                    {
                        var targetFps = videoInfo.IsLikely60FpsContainer ? "Smart 60" : "60";
                        GpuText.Text = $"RIFE AI: {targetFps}fps";
                    }
                    else
                    {
                        GpuText.Text = "RIFE: OFF";
                    }
                    
                    // Show resolution in status if available
                    if (videoInfo.Width != "0" && videoInfo.Height != "0")
                    {
                        var resolution = $"{videoInfo.Width}x{videoInfo.Height}";
                        if (!StatusText.Text.Contains(resolution) && _mpvPlayer.IsPlaying)
                        {
                            UpdateStatus($"Playing - {resolution} @ {currentFps:F1}fps");
                        }
                    }
                }
            }
            catch
            {
                // Ignore any errors in UI updates to prevent crashes
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        private async void ShowOsdMessage(string message)
        {
            OsdMessageText.Text = message;
            OsdMessage.Visibility = Visibility.Visible;

            await Task.Delay(2000);

            OsdMessage.Visibility = Visibility.Collapsed;
        }

        private string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.ToString(ts.Hours > 0 ? @"hh\:mm\:ss" : @"mm\:ss");
        }

        #endregion

        #region Event Handlers

        private void MpvPlayer_StateChanged(object? sender, PlaybackStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.NewState)
                {
                    case PlaybackState.Playing:
                        PlayPauseButton.Content = "⏸";
                        UpdateStatus("Playing");
                        break;
                    case PlaybackState.Paused:
                        PlayPauseButton.Content = "▶";
                        UpdateStatus("Paused");
                        break;
                    case PlaybackState.Stopped:
                        PlayPauseButton.Content = "▶";
                        UpdateStatus("Stopped");
                        break;
                }
            });
        }

        private void MpvPlayer_Error(object? sender, ErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Filter out harmless messages if necessary, but good to see errors
                if (!e.Message.Contains("property update"))
                {
                    ShowOsdMessage($"Error: {e.Message}");
                    Console.WriteLine($"[MPV Error] {e.Message}");
                }
            });
        }

        #endregion

        #region Drag and Drop

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    LoadFile(files[0]);
                }
            }
            else if (e.Data.GetDataPresent(System.Windows.DataFormats.Text))
            {
                var text = (string)e.Data.GetData(System.Windows.DataFormats.Text);
                if (Uri.IsWellFormedUriString(text, UriKind.Absolute))
                {
                    LoadFile(text);
                }
            }
        }

        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) || 
                e.Data.GetDataPresent(System.Windows.DataFormats.Text))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                DropOverlay.Visibility = Visibility.Visible;
                e.Handled = true;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
                DropOverlay.Visibility = Visibility.Collapsed;
                e.Handled = false;
            }
        }

        private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion

        private void CheckDependencies()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var pluginsPath = System.IO.Path.Combine(basePath, "Dependencies", "vapoursynth", "vapoursynth64", "plugins");
            
            // Check for RIFE plugin - can be Python package (vsrife) or DLL
            var rifePluginExists = false;
            
            // First check for Python package (modern vsrife)
            try {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "-c \"import vsrife\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit();
                rifePluginExists = (process?.ExitCode == 0);
            } catch { }
            
            // Fallback: check for DLL-based plugin
            if (!rifePluginExists && System.IO.Directory.Exists(pluginsPath))
            {
                rifePluginExists = System.IO.File.Exists(System.IO.Path.Combine(pluginsPath, "librife.dll")) ||
                     System.IO.File.Exists(System.IO.Path.Combine(pluginsPath, "RIFE.dll")) ||
                     System.IO.File.Exists(System.IO.Path.Combine(pluginsPath, "vs_rife.dll"));
            }
            
            if (!rifePluginExists)
            {
                var message = "⚠️ RIFE Plugin Not Found!\n\n" +
                    "Interpolation will not work without the RIFE plugin.\n\n" +
                    "To fix this:\n" +
                    "1. Open PowerShell in the project directory\n" +
                    "2. Run: scripts\\Download-Dependencies.ps1\n" +
                    "   This will automatically install the RIFE plugin\n\n" +
                    "OR manually:\n" +
                    "1. cd Dependencies\\vapoursynth\n" +
                    "2. python vsrepo.py install vsrife\n\n" +
                    "See DEPENDENCY_STATUS.md for detailed instructions.\n\n" +
                    "Continue without interpolation?";
                
                var result = System.Windows.MessageBox.Show(
                    message,
                    "Missing Dependency",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning
                );
                
                if (result == System.Windows.MessageBoxResult.No)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                else
                {
                    // Disable interpolation controls
                    ToggleInterpolationButton.IsEnabled = false;
                    ToggleInterpolationButton.ToolTip = "RIFE plugin not installed";
                    InterpolationStatusText.Text = "Interpolation: UNAVAILABLE";
                    InterpolationStatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }

        #region Keyboard Shortcuts

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    PlayPause_Click(sender, e);
                    break;
                case Key.Left:
                    SeekBackward_Click(sender, e);
                    break;
                case Key.Right:
                    SeekForward_Click(sender, e);
                    break;
                case Key.F:
                    Fullscreen_Click(sender, e);
                    break;
                case Key.I:
                    ToggleInterpolation_Click(sender, e);
                    break;
                case Key.Escape:
                    if (WindowState == WindowState.Maximized)
                    {
                        WindowState = WindowState.Normal;
                        WindowStyle = WindowStyle.SingleBorderWindow;
                    }
                    break;
            }
        }

        #endregion

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutDialog = new AboutDialog();
            aboutDialog.ShowDialog();
        }
        private void MpvPlayer_LogMessage(object? sender, string message)
        {
            // Always log to debug output for developer analysis
            System.Diagnostics.Debug.WriteLine($"[MPV] {message}");

            // Also write to a log file for easier debugging
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "animeflow_debug.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { /* Ignore logging errors */ }

            // Check for VapourSynth errors
            if (message.Contains("vapoursynth") && (message.Contains("error") || message.Contains("exception") || message.Contains("failed")))
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Interpolation Error: Check logs";
                    _mpvPlayer?.ShowOsdText($"Interpolation Error: {message}", 5000);
                });
            }
            
            // Confirm Interpolation success
            if (message.Contains("AnimeFlow: Interpolation initialized"))
            {
                 Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "RIFE AI Interpolation Active (60fps)";
                    _mpvPlayer?.ShowOsdText("RIFE AI Interpolation Active", 3000);
                });
            }
        }

        private void MpvPlayer_FileLoaded(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatus("File loaded");
                
                // Get video information
                if (_mpvPlayer != null)
                {
                    var videoInfo = _mpvPlayer.GetVideoInfo();
                    var resolution = $"{videoInfo.Width}x{videoInfo.Height}";
                    var fps = videoInfo.ContainerFps > 0 ? $"{videoInfo.ContainerFps:F2}fps" : "Unknown fps";
                    
                    OnLogMessage($"Video loaded: {resolution} @ {fps}");
                    OnLogMessage($"Codec: {videoInfo.VideoCodec}");
                    
                    if (videoInfo.IsLikely60FpsContainer)
                    {
                        OnLogMessage("⚠️ Detected 60fps container - may contain 24/30fps content");
                        ShowOsdMessage("60fps container detected - enabling smart interpolation");
                    }
                }
                
                // Enable interpolation if it was pending
                if (_pendingInterpolationEnable && _interpolationManager != null)
                {
                    try
                    {
                        _interpolationManager.Enable();
                        _pendingInterpolationEnable = false;
                        
                        // Update UI
                        ToggleInterpolationButton.Content = "Disable Interpolation";
                        InterpolationStatusText.Text = "Interpolation: ON (RIFE AI)";
                        InterpolationStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                        
                        ShowOsdMessage("RIFE AI Interpolation enabled");
                        UpdateStatus("Playing with RIFE AI interpolation");
                    }
                    catch (Exception ex)
                    {
                        ShowOsdMessage($"Failed to enable interpolation: {ex.Message}");
                        UpdateStatus("Error enabling interpolation");
                        _pendingInterpolationEnable = false;
                    }
                }
            });
        }

        private void OnLogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[AnimeFlow] {message}");
            
            // Also write to log file
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "animeflow_debug.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { /* Ignore logging errors */ }
        }
    }
}
