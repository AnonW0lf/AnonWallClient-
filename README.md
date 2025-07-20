# AnonWallClient

A modern, cross-platform client for [Walltaker](https://github.com/PawCorp/walltaker) built with .NET 8 and MAUI.

![Platform Support](https://img.shields.io/badge/platforms-Android%20%7C%20Windows%20%7C%20iOS%20%7C%20macOS-blue)
![.NET Version](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)
![Version](https://img.shields.io/badge/version-1.0.0--alpha.1-orange)

## ?? Overview

AnonWallClient is a feature-rich, cross-platform desktop and mobile application that integrates with the Walltaker API to automatically manage wallpapers. It provides a seamless experience across Windows, Android, iOS, and macOS platforms with persistent settings, wallpaper history, and advanced polling options.

> **?? Alpha Release**: This is an alpha version intended for testing and feedback. While all core features are implemented and functional, please report any issues you encounter.

## ? Features

### ?? Core Functionality
- **Automatic Wallpaper Polling** - Continuously checks for new wallpapers from Walltaker API
- **Cross-Platform Support** - Native apps for Windows, Android, iOS, and macOS
- **Background Service** - Keeps running in the background with foreground notifications
- **Persistent Settings** - JSON-based settings that survive app uninstalls

### ?? Wallpaper Management  
- **Real-time Updates** - Automatic wallpaper setting when new content is available
- **Wallpaper History** - View and manage your last 20 wallpapers with thumbnails
- **Save Wallpapers** - Download and save any wallpaper from history to device storage
- **Current Wallpaper Display** - See details about your currently set wallpaper

### ?? Advanced Configuration
- **Polling Interval Control** - Customize check frequency (5 seconds to 5 minutes)
- **Wi-Fi Only Mode** - Restrict API calls to Wi-Fi connections only for data saving
- **Configurable History Limit** - Set maximum wallpapers to keep in history (0-?, 0 disables history)
- **Panic Wallpaper** - Quick-access safe wallpaper for emergency situations
- **Data Export/Import** - Backup and restore settings and history

### ?? Platform-Specific Features
- **Android**: Foreground service with notification controls, boot receiver for auto-start
- **Windows**: System tray integration with context menu and panic button
- **iOS/macOS**: Native integration with platform UI patterns
- **Cross-Platform**: Responsive UI that adapts to different screen sizes

### ?? Interactive Features
- **Wallpaper Responses** - Rate wallpapers with custom response types and text
- **History Management** - Reset history, view detailed wallpaper information
- **Real-time UI Updates** - Automatic refresh when wallpapers are added or removed

## ?? Getting Started

### Prerequisites
- .NET 8.0 SDK
- Platform-specific development tools:
  - **Android**: Android SDK, Java 11+
  - **Windows**: Windows 10/11 SDK
  - **iOS/macOS**: Xcode (macOS only)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/AnonWallClient.git
   cd AnonWallClient
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build for your platform**
   ```bash
   # Android
   dotnet build -f net8.0-android
   
   # Windows
   dotnet build -f net8.0-windows10.0.19041.0
   
   # iOS (macOS only)
   dotnet build -f net8.0-ios
   
   # macOS Catalyst (macOS only)
   dotnet build -f net8.0-maccatalyst
   ```

### Configuration

1. **Get Walltaker Credentials**
   - Visit [Walltaker](https://walltaker.joi.how/) and create an account
   - Obtain your Link ID and API Key from your account settings

2. **Configure the App**
   - Launch AnonWallClient
   - Navigate to Settings
   - Enter your Link ID and API Key
   - Configure polling interval and other preferences
   - Save settings to start the background service

## ?? Usage

### Basic Operation
1. **Initial Setup**: Enter your Walltaker credentials in Settings
2. **Background Service**: The app automatically starts polling for new wallpapers
3. **Notifications**: Receive notifications when new wallpapers are set
4. **History**: View and manage your wallpaper history in the History tab

### Advanced Features
- **Panic Mode**: Set a safe wallpaper via notification button or system tray
- **Wi-Fi Only**: Enable to restrict polling to Wi-Fi connections
- **Data Management**: Export/import your settings and history
- **Response System**: Rate wallpapers with custom responses

## ??? Architecture

### Project Structure
```
AnonWallClient/
??? Background/           # Background services and polling logic
??? Platforms/           # Platform-specific implementations
?   ??? Android/        # Android-specific services and permissions
?   ??? Windows/        # Windows-specific features (system tray)
??? Services/           # Core business logic and API integration
??? Views/              # UI pages and user interface
??? Resources/          # Images, styles, and assets
```

### Key Components
- **PollingService**: Manages background wallpaper checking
- **WalltakerService**: Handles API communication with Walltaker
- **SettingsService**: Manages persistent JSON settings
- **WallpaperHistoryService**: Tracks and manages wallpaper history
- **Platform Services**: Handle wallpaper setting on each platform

## ?? Configuration Options

### Settings File
Settings are stored in `AnonWallClient.settings.json` in the device's Documents folder:

```json
{
  "LinkId": "your-link-id",
  "ApiKey": "your-api-key",
  "PollingIntervalSeconds": 15,
  "WifiOnly": false,
  "MaxHistoryLimit": 20,
  "PanicUrl": "https://example.com/safe-image.jpg",
  "PanicFilePath": "/path/to/local/safe-image.jpg",
  "WallpaperSaveFolder": "/path/to/wallpaper/folder"
}
```

### Platform-Specific Defaults
- **Android**: Wallpapers saved to `Pictures/AnonWallClient`
- **Windows**: Wallpapers saved to `Pictures/AnonWallClient`  
- **iOS/macOS**: Wallpapers saved to `Documents/AnonWallClient`

## ??? Development

### Dependencies
- **Microsoft.Maui.Controls** 8.0.70 - Cross-platform UI framework
- **CommunityToolkit.Maui** 9.0.0 - Additional UI components and helpers
- **Microsoft.Extensions.Http** 8.0.0 - HTTP client factory and services
- **H.NotifyIcon.Maui** 2.1.0 - Windows system tray support

### Building from Source
```bash
# Debug build
dotnet build

# Release build  
dotnet build -c Release

# Platform-specific release
dotnet publish -f net8.0-android -c Release
```

### Running Tests
```bash
dotnet test
```

## ?? Permissions

### Android
- `INTERNET` - API communication
- `ACCESS_NETWORK_STATE` - Wi-Fi only mode
- `SET_WALLPAPER` - Setting wallpapers
- `FOREGROUND_SERVICE` - Background operation
- `RECEIVE_BOOT_COMPLETED` - Auto-start after boot
- `WAKE_LOCK` - Prevent sleep during operations
- `READ_MEDIA_IMAGES` - Accessing local images for panic wallpaper

### Windows
- Network access for API communication
- File system access for wallpaper management

## ?? Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## ?? License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ?? Related Projects

- [Walltaker](https://github.com/PawCorp/walltaker) - The main Walltaker server and web interface
- [Walltaker API Documentation](https://walltaker.joi.how/help/client_guide) - API reference for client developers

## ?? Support

- **Issues**: Report bugs and request features via [GitHub Issues](https://github.com/yourusername/AnonWallClient/issues)
- **Discussions**: Community discussions via [GitHub Discussions](https://github.com/yourusername/AnonWallClient/discussions)
- **Walltaker Community**: Join the main Walltaker community for general support

## ?? Acknowledgments

- Thanks to the Walltaker team for creating the platform and API
- .NET MAUI team for the excellent cross-platform framework
- Community contributors and testers

---

**Note**: This application is an independent client for Walltaker and is not officially affiliated with the Walltaker project.
