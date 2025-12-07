# AnimeFlow 🎬

![AnimeFlow Screenshot](Screenshot%202025-12-08%20000421.png)

**Real-time Anime Frame Interpolation Player**

Transform your anime watching experience with smooth 60fps playback using AI-powered frame interpolation. AnimeFlow intelligently converts 24fps anime to buttery-smooth 60fps in real-time.

[![Build Status](https://github.com/Demoen/animeflow/workflows/Build%20and%20Test/badge.svg)](https://github.com/Demoen/animeflow/actions)
[![Release](https://img.shields.io/github/v/release/Demoen/animeflow)](https://github.com/Demoen/animeflow/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## ✨ Features

### Core Functionality
- 🎯 **Real-time 60fps Interpolation** - GPU-accelerated frame interpolation using mpv's built-in engine
- 🌐 **YouTube Support** - Direct streaming from YouTube URLs with smart 60fps container detection
- 📁 **Local Playback** - Supports MP4, MKV, AVI, WebM, MOV, and more
- 🎨 **Modern UI** - Clean, dark-themed interface with intuitive controls
- 🖱️ **Drag & Drop** - Simply drag videos into the player

### Advanced Features
- 🤖 **Smart FPS Detection** - Automatically detects and handles 60fps containers with 24fps content
- 🎛️ **Quality Presets** - Fast, Balanced, and Beauty modes for different GPUs
- 🔧 **Hardware Acceleration** - D3D11 GPU decode for H264/HEVC/VP9 (software fallback for AV1)
- 📊 **Real-time Monitoring** - Live FPS counter and resolution display
- ⚡ **Optimized Streaming** - Large buffers and smart caching for smooth YouTube playback

## 🚀 Quick Start

### Prerequisites
- **Windows 10/11** (64-bit)
- **.NET 8.0 Runtime** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **GPU** - NVIDIA GTX 1660 or better recommended (RTX series ideal)

### Installation

1. **Download** the latest release from [Releases](https://github.com/Demoen/animeflow/releases)
2. **Extract** the ZIP file to a folder
3. **Run** `AnimeFlow.exe`
4. **Enjoy!** All dependencies are included

### Building from Source

```powershell
# Clone the repository
git clone https://github.com/Demoen/animeflow.git
cd animeflow

# Build
cd AnimeFlow
dotnet build --configuration Release

# Run
dotnet run --configuration Release
```

## 🎮 Usage

### Playing Local Videos
1. Click **"Open File"** or drag & drop a video
2. Click **"Enable Interpolation"** (or press `I`)
3. Select quality preset from the dropdown
4. Enjoy smooth 60fps!

### Playing YouTube Videos
1. Click **"Open URL"**
2. Paste YouTube URL
3. Wait **15-20 seconds** for buffering
4. Interpolation applies automatically!

### Keyboard Shortcuts
| Key | Action |
|-----|--------|
| `Space` | Play/Pause |
| `I` | Toggle Interpolation |
| `F` | Fullscreen |
| `Left/Right` | Seek ±10s |
| `Esc` | Exit Fullscreen |

## 🎨 Quality Presets

### Fast
- **Target**: 540p processing
- **GPU Usage**: ~30-50%
- **Best for**: GTX 1660, RTX 2060
- **Quality**: Good

### Balanced (Recommended)
- **Target**: 720p processing
- **GPU Usage**: ~50-70%
- **Best for**: RTX 3060, RTX 4060
- **Quality**: Excellent

### Beauty
- **Target**: 1080p processing
- **GPU Usage**: ~70-90%
- **Best for**: RTX 3070+, RTX 4070+
- **Quality**: Maximum

## 🔧 Technical Details

### Architecture
- **Player**: libmpv (latest)
- **Interpolation**: mpv's display-resample with advanced temporal filtering
- **Hardware Decode**: D3D11VA for H264/HEVC/VP9, software fallback for AV1
- **Rendering**: gpu-next with D3D11 backend
- **Streaming**: yt-dlp for YouTube support

### Interpolation Engine
```
Source FPS Detection → Duplicate Frame Removal → GPU Interpolation → 60fps Output
    (24/30fps)              (if needed)           (temporal filters)
```

### YouTube Optimization
- Automatically detects 60fps containers with 24fps content
- Removes duplicate frames before interpolation
- Large buffers (500MB) for smooth streaming
- Smart quality selection (max 1080p)

## 📊 Performance

| GPU | Fast | Balanced | Beauty |
|-----|------|----------|--------|
| RTX 4090 | ✅ Excellent | ✅ Excellent | ✅ Excellent |
| RTX 4070 | ✅ Excellent | ✅ Excellent | ✅ Excellent |
| RTX 3070 | ✅ Excellent | ✅ Excellent | ✅ Very Good |
| RTX 3060 | ✅ Excellent | ✅ Very Good | ⚠️ Good |
| RTX 2060 | ✅ Very Good | ⚠️ Good | ❌ Limited |
| GTX 1660 | ✅ Good | ⚠️ Fair | ❌ Not Recommended |

## 🐛 Troubleshooting

### Video is black
- **Wait 15-20 seconds** for YouTube videos to buffer
- Check if file/URL is valid
- Try lowering quality preset
- Check `animeflow_debug.log` for errors

### Stuttering/Low FPS
- Lower quality preset (try Fast)
- Close other GPU-intensive applications
- Update GPU drivers
- Try 720p content instead of 4K

### YouTube URL not working
- Ensure internet connection
- Update `yt-dlp.exe` in `Dependencies/tools/`
- Try a different video
- Check firewall settings

### Interpolation not smooth
- Verify GPU supports DirectX 11
- Monitor GPU usage (shouldn't be at 100%)
- Try Balanced preset
- Ensure adequate GPU cooling

## 📝 Logs

Application logs are saved to:
```
animeflow_debug.log
```

Contains detailed information about:
- Video loading process
- FPS detection results
- Interpolation status
- Error messages and codes

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **[mpv](https://mpv.io/)** - Excellent media player foundation
- **[yt-dlp](https://github.com/yt-dlp/yt-dlp)** - YouTube streaming support
- **[ModernWpfUI](https://github.com/Kinnara/ModernWpf)** - Modern WPF styling
- **Anime Community** - For inspiring this project

## 📧 Contact

- **Issues**: [GitHub Issues](https://github.com/Demoen/animeflow/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Demoen/animeflow/discussions)

## ⭐ Star History

If you find AnimeFlow useful, please consider giving it a star!

---

**Made with ❤️ for the anime community**

*Transform your anime experience - one frame at a time* ✨

