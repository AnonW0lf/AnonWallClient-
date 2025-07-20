# AnonWallClient

A modern, cross-platform client for [Walltaker](https://github.com/PawCorp/walltaker) built with .NET 8 and MAUI.

![Platform Support](https://img.shields.io/badge/platforms-Android%20%7C%20Windows%20%7C%20iOS%20%7C%20macOS-blue)
![.NET Version](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)
![Version](https://img.shields.io/badge/version-1.0.0-orange)

## 🎯 Overview

AnonWallClient is a feature-rich, cross-platform desktop and mobile application that integrates with the Walltaker API to automatically manage wallpapers. It provides a seamless experience across Windows, Android, iOS, and macOS platforms with persistent settings, wallpaper history, advanced polling options, enterprise-grade autostart capabilities, and comprehensive file management.

## 🚀 Quick Start

### Build from Source
```bash
# Clone repository
git clone https://github.com/yourusername/AnonWallClient.git
cd AnonWallClient

# Use .NET CLI directly
dotnet restore
dotnet build -c Release
```

## ✨ Features

### 🎯 Core Functionality
- **Automatic Wallpaper Polling** - Continuously checks for new wallpapers from Walltaker API
- **Cross-Platform Support** - Native apps for Windows, Android, iOS, and macOS
- **Background Service** - Keeps running in the background with foreground notifications
- **Persistent Settings** - JSON-based settings that survive app uninstalls

### 🖼️ Advanced Wallpaper Management  
- **Multi-LinkID Support** - Use separate LinkIDs for desktop wallpaper and lockscreen
- **Dual Wallpaper Types** - Independent control of desktop and lockscreen wallpapers
- **Real-time Updates** - Automatic wallpaper setting when new content is available
- **Wallpaper History** - View and manage your wallpapers with thumbnails and metadata
- **Save Wallpapers** - Download and save any wallpaper from history to device storage
- **Current Wallpaper Display** - See details about your currently set wallpaper
- **Wallpaper Fit Modes** - Fill, Fit, Center, Tile, or Stretch options

### 🔒 Platform-Specific Lockscreen Support
- **Windows** - True lockscreen wallpaper support via Windows Registry with backup/restore
- **Android** - Native lockscreen wallpaper setting with separate LinkID support
- **iOS/macOS** - Saves wallpapers with user instructions for manual setup

### 🚀 Enterprise AutoStart System
- **Windows** - Task Scheduler integration with admin privileges and UAC handling
- **Android** - Boot receiver with configurable startup preferences
- **macOS** - Launch Agents with automatic login startup
- **Configurable Intervals** - Run every 1-24 hours plus login startup
- **Status Monitoring** - Real-time autostart status and configuration testing

### 🗂️ Intelligent Caching System
- **Image Caching** - Configurable cache (10MB-1GB) with automatic expiry (1-30 days)
- **Cache Management** - View cache info, clear cache, and monitor cache usage
- **Performance Optimization** - Faster wallpaper changes with cached images
- **Storage Efficiency** - Automatic cleanup of expired cached images

### 📁 Enhanced File Management & Storage
- **Organized Directory Structure** - Clean, logical file organization across platforms
- **Cross-Platform Storage** - Consistent storage paths with platform-specific optimizations
- **Comprehensive Logging** - Persistent file-based logging with automatic rotation
- **Smart Permission Handling** - Automatic permission requests with graceful fallbacks
- **Data Export/Import** - Complete backup and restore functionality with ZIP compression

#### Directory Structure
```
📁 AnonClient/                    # Main app directory
├── 📁 settings/                  # Settings and configuration files
│   ├── AnonWallClient.settings.json
│   ├── AnonWallClient.wallpaper_history.json
│   └── panic_wallpaper.jpg
├── 📁 logs/                      # Application logs with rotation
│   ├── app.log                   # Current log file
│   └── app_backup_*.log          # Rotated log files (max 5)
├── 📁 cache/                     # Image cache with size management
│   └── [cached_images]           # Cached wallpaper files
└── 📁 pictures/                  # Saved wallpapers
    └── [downloaded_wallpapers]   # User-saved wallpapers
```

#### Storage Locations by Platform
- **Android**: `/Internal storage/AnonClient/` (with fallback to app directory)
- **Windows**: `Documents/AnonClient/`
- **iOS/macOS**: `Documents/AnonClient/`

### ⚙️ Advanced Configuration
- **Polling Interval Control** - Customize check frequency (5 seconds to unlimited)
- **Wi-Fi Only Mode** - Restrict API calls to Wi-Fi connections only for data saving
- **Configurable History Limit** - Set maximum wallpapers to keep in history (0-∞, 0 disables history)
- **Enhanced Panic System** - Quick-access safe wallpaper with lockscreen restore capabilities

### 📱 Platform-Specific Features
- **Android**: Foreground service with notification controls, boot receiver for auto-start, comprehensive storage permissions
- **Windows**: System tray integration, Task Scheduler, Windows Registry lockscreen support
- **iOS/macOS**: Native integration with platform UI patterns, Launch Agents
- **Cross-Platform**: Responsive UI that adapts to different screen sizes

### 🎮 Interactive Features
- **Wallpaper Responses** - Rate wallpapers with custom response types and text
- **History Management** - Reset history, view detailed wallpaper information
- **Real-time UI Updates** - Automatic refresh when wallpapers are added or removed
- **Advanced Settings UI** - Organized sections with platform-specific controls

## 🏗️ Development

### Prerequisites
- .NET 8.0 SDK
- Platform-specific development tools:
  - **Android**: Android SDK, Java 11+
  - **Windows**: Windows 10/11 SDK
  - **iOS/macOS**: Xcode (macOS only)

### Local Development
```bash
# Restore dependencies
dotnet restore

# Build for specific platform
dotnet build -f net8.0-android           # Android
dotnet build -f net8.0-windows10.0.19041.0  # Windows
dotnet build -f net8.0-ios               # iOS (macOS only)
dotnet build -f net8.0-maccatalyst       # macOS (macOS only)

# Run on specific platform
dotnet run -f net8.0-windows10.0.19041.0  # Windows
```

### Building Releases
```bash
# Debug build
dotnet build

# Release build  
dotnet build -c Release

# Platform-specific release
dotnet publish -f net8.0-android -c Release
```

## 🛠️ Configuration

### Initial Setup
1. **Get Walltaker Credentials**
   - Visit [Walltaker](https://walltaker.joi.how/) and create an account
   - Obtain your Link ID and API Key from your account settings

2. **Configure the App**
   - Launch AnonWallClient
   - Navigate to Settings
   - Choose LinkID mode (Shared or Separate for wallpaper/lockscreen)
   - Enter your Link ID(s) and API Key
   - Configure polling interval and other preferences
   - Set up autostart (if desired) and caching options
   - Save settings to start the background service

### Enhanced Settings File
Settings are stored in `AnonClient/settings/AnonWallClient.settings.json`:

```json
{
  "LinkIdMode": 0,
  "WallpaperLinkId": "your-wallpaper-link-id",
  "LockscreenLinkId": "your-lockscreen-link-id",
  "ApiKey": "your-api-key",
  "PollingIntervalSeconds": 15,
  "WifiOnly": false,
  "MaxHistoryLimit": 20,
  "PanicUrl": "https://example.com/safe-image.jpg",
  "PanicFilePath": "/path/to/local/safe-image.jpg",
  "WallpaperSaveFolder": "/path/to/wallpaper/folder",
  "EnableImageCache": true,
  "MaxCacheSizeMB": 100,
  "CacheExpiryDays": 7,
  "WallpaperFitMode": 0,
  "AutoStartEnabled": false,
  "AutoStartIntervalHours": 1
}
```

## 🏛️ Architecture

### Project Structure
```
AnonWallClient/
├── Background/           # Background services and polling logic
├── Platforms/           # Platform-specific implementations
│   ├── Android/        # Android services, boot receiver, autostart, permissions
│   ├── Windows/        # Windows services, registry, task scheduler
│   ├── iOS/           # iOS wallpaper services
│   └── MacCatalyst/   # macOS services and launch agents
├── Services/           # Core business logic and API integration
│   ├── AutoStart/     # Cross-platform autostart interfaces
│   ├── Cache/         # Image caching system
│   ├── Logging/       # File-based logging with rotation
│   └── Panic/         # Enhanced panic mode services
├── Views/              # UI pages and user interface
└── Resources/          # Images, styles, and assets
```

### Key Components
- **PollingService**: Manages background wallpaper checking
- **WalltakerService**: Handles API communication with Walltaker
- **SettingsService**: Manages persistent JSON settings with organized file structure
- **WallpaperHistoryService**: Tracks and manages wallpaper history
- **ImageCacheService**: Intelligent image caching with configurable limits
- **AppLogService**: File-based logging with automatic rotation and management
- **IAutoStartService**: Cross-platform autostart abstraction
- **PanicService**: Enhanced panic mode with lockscreen restore
- **Platform Services**: Handle wallpaper setting on each platform

### Dependencies
- **Microsoft.Maui.Controls** 8.0.70 - Cross-platform UI framework
- **CommunityToolkit.Maui** 9.0.0 - Additional UI components and helpers
- **Microsoft.Extensions.Http** 8.0.0 - HTTP client factory and services
- **H.NotifyIcon.Maui** 2.1.0 - Windows system tray support
- **TaskScheduler** 2.10.1 - Windows Task Scheduler integration (Windows only)

## 📱 Platform Details

### Android Requirements
- Android 5.0+ (API level 21)
- **Permissions**: Internet, Network State, Set Wallpaper, Foreground Service, Boot Receiver, External Storage
- **Storage Permissions**: Automatic handling of API-level specific permissions (API 29-33+)
- **Features**: Full lockscreen support, boot autostart, foreground service, comprehensive storage access

#### Android Storage Permissions
- **API 33+**: `READ_MEDIA_IMAGES`
- **API 29-32**: `READ_EXTERNAL_STORAGE`
- **API ≤28**: `READ_EXTERNAL_STORAGE` + `WRITE_EXTERNAL_STORAGE`
- **API 30+**: `MANAGE_EXTERNAL_STORAGE` (for full access, optional)

### Windows Requirements  
- Windows 10 version 1809 or later
- **Admin Privileges**: Required for Task Scheduler and Registry lockscreen features
- **Features**: True lockscreen support via Registry, Task Scheduler autostart, system tray

### iOS Requirements
- iOS 11.0 or later
- Development certificate required for installation
- **Limitations**: No lockscreen wallpaper API, no autostart support

### macOS Requirements
- macOS 10.13 or later
- Catalyst framework for native macOS experience
- **Features**: Launch Agents autostart, wallpaper saving with user instructions

## 🚀 AutoStart System

### Platform Support Matrix

| Platform | Technology | Admin Required | Features |
|----------|------------|----------------|----------|
| **Windows** | Task Scheduler | Yes (UAC) | Login + Interval scheduling |
| **Android** | Boot Receiver | No | Boot startup + Preferences |
| **macOS** | Launch Agents | No | Login + Interval scheduling |
| **iOS** | Not Supported | N/A | Platform limitations |

### Configuration Options
- **Enable/Disable**: Toggle autostart functionality
- **Interval Scheduling**: Run every 1-24 hours
- **Status Monitoring**: Real-time status display
- **Test Configuration**: Verify autostart setup
- **Platform Detection**: Shows relevant options per platform

## 📊 Logging & Debugging

### File-Based Logging System
- **Persistent Logs**: All logs saved to `AnonClient/logs/app.log`
- **Automatic Rotation**: Files rotated when exceeding 10MB
- **Backup Management**: Keeps last 5 backup files with timestamps
- **Cross-Platform**: Works consistently across all platforms
- **Export Capability**: Full log export functionality

### Debugging Features
- **Real-time UI Logs**: Live log display in the app
- **Timestamped Entries**: Precise timing with millisecond accuracy
- **Service Identification**: Clear component/service identification
- **Error Stack Traces**: Detailed error information when available

### Log Management
- **Memory Efficiency**: UI shows last 1000 entries only
- **Background Writing**: Non-blocking file operations
- **Thread Safety**: Concurrent access protection
- **Cleanup Automation**: Old log file cleanup

## 🔒 Enhanced Security & Privacy

### Windows Registry Lockscreen
- **Backup System**: Automatically backs up original lockscreen settings
- **Restore Capability**: Can restore original settings during panic mode
- **Admin Handling**: Graceful UAC prompts with fallback options
- **Non-Destructive**: Never permanently modifies system without backup

### Panic Mode Enhancements
- **Dual Restoration**: Sets panic wallpaper for both desktop and lockscreen
- **Original Restore**: Button to restore backed-up wallpapers (Windows)
- **Platform-Aware**: Different behavior optimized per platform
- **Error Recovery**: Comprehensive error handling and user feedback

### Android Storage Security
- **Permission Management**: Automatic API-level appropriate permission requests
- **Graceful Fallbacks**: Continues functioning with limited permissions
- **User Guidance**: Clear instructions for permission grants
- **Privacy Respect**: Only requests necessary permissions

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Related Projects

- [Walltaker](https://github.com/PawCorp/walltaker) - The main Walltaker server and web interface
- [Walltaker API Documentation](https://walltaker.joi.how/help/client_guide) - API reference for client developers

## 💬 Support

- **Issues**: Report bugs and request features via [GitHub Issues](https://github.com/yourusername/AnonWallClient/issues)
- **Discussions**: Community discussions via [GitHub Discussions](https://github.com/yourusername/AnonWallClient/discussions)
- **Walltaker Community**: Join the main Walltaker community for general support

## 🏆 Acknowledgments

- Thanks to the Walltaker team for creating the platform and API
- .NET MAUI team for the excellent cross-platform framework
- Community contributors and testers
- Microsoft TaskScheduler library for Windows automation capabilities

---

**Note**: This application is an independent client for Walltaker and is not officially affiliated with the Walltaker project.
