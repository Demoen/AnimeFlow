# AnimeFlow 🎬

![AnimeFlow Screenshot](Screenshot%202025-12-08%20000421.png)

**Real-time Anime Frame Interpolation Player with AI-Powered RIFE**

Transform your anime watching experience with smooth 60fps playback using AI-powered RIFE frame interpolation. AnimeFlow uses cutting-edge deep learning to intelligently generate intermediate frames in real-time, converting 24fps anime to buttery-smooth 60fps.

[![Build Status](https://github.com/Demoen/animeflow/workflows/Build%20and%20Test/badge.svg)](https://github.com/Demoen/animeflow/actions)
[![Release](https://img.shields.io/github/v/release/Demoen/animeflow)](https://github.com/Demoen/animeflow/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## ✨ Features

### Core Functionality
- 🤖 **RIFE AI Interpolation** - Deep learning-powered frame generation using Real-Time Intermediate Flow Estimation
- ⚡ **GPU-Accelerated** - Utilizes CUDA for real-time processing on NVIDIA RTX GPUs with FP16 precision
- 🌐 **YouTube Support** - Direct streaming from YouTube URLs with smart 60fps container detection
- 📁 **Local Playback** - Supports MP4, MKV, AVI, WebM, MOV, and more
- 🎨 **Modern UI** - Clean, dark-themed interface with intuitive controls
- 🖱️ **Drag & Drop** - Simply drag videos into the player

### Advanced Features
- 🎯 **Real-time Optimizations** - Multi-threaded processing, intelligent downscaling, and frame caching
- 🧠 **Smart FPS Detection** - Automatically detects and handles 60fps containers with 24fps content
- 🎛️ **Quality Presets** - Fast, Balanced, and Beauty modes optimized for different GPUs
- 🔧 **Hardware Acceleration** - D3D11 GPU decode for H264/HEVC/VP9, VapourSynth R73 integration
- 📊 **Real-time Monitoring** - Live FPS counter and resolution display
- ⚡ **Optimized Streaming** - Large buffers and smart caching for smooth YouTube playback

## 🚀 Quick Start

### Prerequisites
- **Windows 10/11** (64-bit)
- **.NET 8.0 Runtime** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **GPU** - NVIDIA RTX 2060 or better recommended (RTX 2070+ ideal for RIFE AI)
- **Python 3.8** - [Download here](https://www.python.org/downloads/release/python-3810/) *(for RIFE AI)*
- **VapourSynth R73** - [Download here](https://github.com/vapoursynth/vapoursynth/releases/download/R73/VapourSynth-x64-R73.exe) *(for RIFE AI)*

### Installation

**Option A: Full RIFE AI Setup (Recommended)**

1. **Download** the latest release from [Releases](https://github.com/Demoen/animeflow/releases)
2. **Extract** the ZIP file to a folder
3. **Install Prerequisites**:
   - Install Python 3.8 (check "Add Python to PATH")
   - Install VapourSynth R73 with Python 3.8 support
4. **Install Python Packages**:
   ```powershell
   # GPU PyTorch with CUDA 11.8
   python -m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
   
   # VapourSynth + RIFE
   python -m pip install vapoursynth
   python -m pip install vsrife --no-deps
   python -m pip install numpy requests tqdm
   ```
5. **Run** `AnimeFlow.exe`
6. **Enjoy AI-powered 60fps!**

**Option B: Quick Start (Basic Interpolation)**

If you don't want to set up RIFE AI:
1. Download and extract
2. Install .NET 8.0 Runtime only
3. Run `AnimeFlow.exe`
4. Uses mpv's built-in GPU interpolation (still excellent!)

### Building from Source

```powershell
# Clone the repository
git clone https://github.com/Demoen/animeflow.git
cd animeflow

# Download dependencies (required, ~3.5GB, takes 10-20 min)
cd scripts
.\Download-Dependencies.ps1
cd ..

# Build
cd AnimeFlow
dotnet build --configuration Release

# Run
dotnet run --configuration Release
```

**Note**: The `Download-Dependencies.ps1` script downloads VapourSynth, mpv, RIFE models, PyTorch (~2GB), and other required components. This is a one-time setup.

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
- **Target**: 720p processing with scale=0.5
- **GPU Usage**: ~30-40%
- **Best for**: RTX 2060, RTX 3060
- **Quality**: Good, real-time performance

### Balanced (Recommended)
- **Target**: 720p processing with scale=0.5
- **GPU Usage**: ~40-60%
- **Best for**: RTX 2070, RTX 3060 Ti, RTX 4060
- **Quality**: Excellent, smooth real-time

### Beauty
- **Target**: 720p processing with scale=1.0
- **GPU Usage**: ~60-80%
- **Best for**: RTX 3070+, RTX 4070+
- **Quality**: Maximum quality, near real-time

## 🔧 Technical Details

### Architecture
- **Player**: libmpv (latest)
- **AI Engine**: RIFE (Real-Time Intermediate Flow Estimation) via vsrife
- **Framework**: VapourSynth R73 for video processing pipeline
- **Deep Learning**: PyTorch with CUDA 11.8 and FP16 precision
- **Hardware Decode**: D3D11VA for H264/HEVC/VP9
- **Rendering**: gpu-next with D3D11 backend
- **Streaming**: yt-dlp for YouTube support

### RIFE AI Pipeline
```
Input (24fps) → VapourSynth → RGB Conversion → RIFE Neural Network →
                                                  ↓
Multi-threaded → Frame Generation → YUV Conversion → Output (60fps)
Processing       (GPU/FP16)         (Fast Bilinear)
```

### Real-Time Optimizations
- **Multi-threading**: Uses all CPU cores for parallel processing
- **Resolution Management**: 1080p+ downscaled to 720p for RIFE, then upscaled
- **FP16 Precision**: 2x faster inference on RTX GPUs
- **Internal Scaling**: RIFE processes at 0.5x scale for high-res content
- **Frame Caching**: 100-frame buffer prevents stuttering
- **Fast Algorithms**: Bilinear scaling for speed over quality

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
| RTX 3080 | ✅ Excellent | ✅ Excellent | ✅ Very Good |
| RTX 3070 | ✅ Excellent | ✅ Very Good | ✅ Very Good |
| RTX 3060 | ✅ Very Good | ✅ Very Good | ⚠️ Good |
| RTX 2070 | ✅ Very Good | ✅ Good | ⚠️ Good |
| RTX 2060 | ✅ Good | ⚠️ Fair | ❌ Limited |

*Performance based on RIFE AI interpolation with FP16 precision*

## 🐛 Troubleshooting

### "Could not initialize VapourSynth scripting"
- Install VapourSynth R73 system-wide: [Download here](https://github.com/vapoursynth/vapoursynth/releases/download/R73/VapourSynth-x64-R73.exe)
- Ensure Python 3.8 is installed and in PATH
- Run: `python -m pip install vapoursynth vsrife`

### Python/PyTorch errors
- Verify Python 3.8 is installed (not 3.9, 3.10, 3.11+)
- Install GPU PyTorch: `python -m pip install torch --index-url https://download.pytorch.org/whl/cu118`
- Check GPU drivers are up to date

### Video is black
- **Wait 15-20 seconds** for YouTube videos to buffer
- Check if file/URL is valid
- Try lowering quality preset
- Check `animeflow_debug.log` for errors

### Stuttering/Low FPS with RIFE
- Lower quality preset (try Fast)
- Ensure GPU drivers are updated
- Close other GPU-intensive applications
- Monitor GPU temperature and usage
- 1080p content is auto-downscaled for performance

### Want basic interpolation without RIFE setup?
- Skip Python/VapourSynth installation
- App will use mpv's built-in GPU interpolation
- Still provides excellent 60fps results!

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

## 📦 For Developers

### CI/CD Pipeline

This project uses pre-built dependencies for faster CI builds:

1. **Pre-built Package**: Dependencies are packaged once and uploaded to GitHub Releases
2. **CI Downloads**: Workflows download the pre-built package instead of building from scratch  
3. **Result**: Builds complete in ~2-5 minutes instead of ~20 minutes

### Updating Dependencies

If you need to update dependencies (e.g., newer mpv, RIFE models):

```powershell
# 1. Download fresh dependencies
cd scripts
.\Download-Dependencies.ps1 -Force

# 2. Package them
.\Package-Dependencies.ps1 -Version "latest"

# 3. Upload to GitHub Release
# - Create/edit release with tag 'deps-v1'
# - Upload: release-artifacts/Dependencies-Windows-x64-latest.zip
```

See [scripts/DEPENDENCIES.md](scripts/DEPENDENCIES.md) for detailed instructions.

### Project Structure

```
AnimeFlow/
├── AnimeFlow/              # Main WPF application
│   ├── Core/              # MPV player, VapourSynth integration
│   ├── Models/            # Data models
│   └── Services/          # Settings management
├── Dependencies/          # External tools (gitignored)
│   ├── mpv/              # Video player
│   ├── vapoursynth/      # Video processing
│   ├── rife/             # RIFE models
│   └── tools/            # yt-dlp
├── scripts/              # PowerShell automation scripts
└── .github/workflows/    # CI/CD pipelines
```

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **[RIFE](https://github.com/hzwer/Practical-RIFE)** - Real-Time Intermediate Flow Estimation neural network
- **[vsrife](https://github.com/HolyWu/vs-rife)** - VapourSynth RIFE plugin
- **[VapourSynth](https://github.com/vapoursynth/vapoursynth)** - Professional video processing framework
- **[PyTorch](https://pytorch.org/)** - Deep learning framework powering RIFE
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

