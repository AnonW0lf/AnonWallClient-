using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Compression;

namespace AnonWallClient.Services;

public enum WallpaperFitMode
{
    Fill = 0,        // Stretch to fill screen (may distort)
    Fit = 1,         // Fit within screen (maintain aspect ratio, may have borders)
    Center = 2,      // Center image at original size
    Tile = 3,        // Tile the image
    Stretch = 4      // Stretch to fill (same as Fill)
}

public enum WallpaperType
{
    Wallpaper = 0,   // Desktop/Home screen wallpaper
    Lockscreen = 1   // Lock screen wallpaper
}

public enum LinkIdMode
{
    SharedLink = 0,      // Use same LinkId for both wallpaper and lockscreen  
    SeparateLinks = 1    // Use different LinkIds for wallpaper and lockscreen
}

public class AppSettings
{
    // Legacy single LinkId for backward compatibility
    public string LinkId { get; set; } = string.Empty;
    
    // New multi-LinkId support
    public string WallpaperLinkId { get; set; } = string.Empty;
    public string LockscreenLinkId { get; set; } = string.Empty;
    public LinkIdMode LinkIdMode { get; set; } = LinkIdMode.SharedLink;
    
    public string ApiKey { get; set; } = string.Empty;
    public string PanicUrl { get; set; } = string.Empty;
    public string PanicFilePath { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 15;
    public bool WifiOnly { get; set; } = false;
    public string WallpaperSaveFolder { get; set; } = string.Empty;
    public int MaxHistoryLimit { get; set; } = 20; // Default to 20, 0 means no history
    
    // New caching settings
    public bool EnableImageCache { get; set; } = true;
    public int MaxCacheSizeMB { get; set; } = 100; // Default 100MB cache
    public int CacheExpiryDays { get; set; } = 7; // Cache expires after 7 days
    
    // New wallpaper fit settings
    public WallpaperFitMode WallpaperFitMode { get; set; } = WallpaperFitMode.Fill;
    
    // Autostart settings
    public bool AutoStartEnabled { get; set; } = false;
    public int AutoStartIntervalHours { get; set; } = 1;
    
    // Add more settings as needed
}

public class SettingsService
{
    private readonly string _documentsPath;
    private readonly string _settingsPath;
    private readonly string _historyPath;
    private AppSettings? _settings;

    public SettingsService()
    {
        _documentsPath = GetAppDataDirectory();
        _settingsPath = Path.Combine(_documentsPath, "settings", "AnonWallClient.settings.json");
        _historyPath = Path.Combine(_documentsPath, "settings", "AnonWallClient.wallpaper_history.json");
        
        // Ensure directories exist
        try
        {
            var settingsDir = Path.GetDirectoryName(_settingsPath);
            var historyDir = Path.GetDirectoryName(_historyPath);
            
            if (!string.IsNullOrEmpty(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }
            if (!string.IsNullOrEmpty(historyDir))
            {
                Directory.CreateDirectory(historyDir);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating settings directories: {ex.Message}");
            
            // If external storage fails, try fallback to app directory
#if ANDROID
            try
            {
                _documentsPath = FileSystem.AppDataDirectory;
                _settingsPath = Path.Combine(_documentsPath, "settings", "AnonWallClient.settings.json");
                _historyPath = Path.Combine(_documentsPath, "settings", "AnonWallClient.wallpaper_history.json");
                
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
                
                System.Diagnostics.Debug.WriteLine($"Fallback to app directory: {_documentsPath}");
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback also failed: {fallbackEx.Message}");
            }
#endif
        }
        
        Load();
    }

    private string GetAppDataDirectory()
    {
#if ANDROID
        // Use internal storage: /Internal storage/AnonClient/
        var externalStorageDir = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
        if (!string.IsNullOrEmpty(externalStorageDir))
        {
            return Path.Combine(externalStorageDir, "AnonClient");
        }
        // Fallback to app-specific external storage
        return FileSystem.AppDataDirectory;
#else
        // Other platforms: Documents/AnonClient/
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AnonClient");
#endif
    }

    public AppSettings Settings => _settings ??= new AppSettings();

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                
                // Migration: if legacy LinkId is set but new LinkIds are empty, migrate
                MigrateLegacyLinkId();
            }
            else
            {
                _settings = new AppSettings();
                Save(); // Create default settings file
            }
            
            // Sync critical settings to Preferences for Android notification access
            SyncToPreferences();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            _settings = new AppSettings();
            try
            {
                Save(); // Try to save default settings
                SyncToPreferences();
            }
            catch
            {
                // If we can't save, just continue with in-memory settings
            }
        }
    }

    private void MigrateLegacyLinkId()
    {
        // If legacy LinkId is set but new ones are empty, migrate
        if (!string.IsNullOrEmpty(Settings.LinkId) && 
            string.IsNullOrEmpty(Settings.WallpaperLinkId) && 
            string.IsNullOrEmpty(Settings.LockscreenLinkId))
        {
            Settings.WallpaperLinkId = Settings.LinkId;
            Settings.LockscreenLinkId = Settings.LinkId;
            Settings.LinkIdMode = LinkIdMode.SharedLink;
            Save();
        }
    }

    private void SyncToPreferences()
    {
        // Sync critical settings to Preferences for Android notification and boot receiver access
        // For backward compatibility, sync the primary wallpaper LinkId as the main LinkId
        var primaryLinkId = GetPrimaryLinkId();
        Preferences.Set("LinkId", primaryLinkId);
        Preferences.Set("panic_file_path", Settings.PanicFilePath);
        Preferences.Set("panic_url", Settings.PanicUrl);
        
        // Sync Wi-Fi only setting for Android foreground service access
        Preferences.Set("wifi_only", Settings.WifiOnly);
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            
            File.WriteAllText(_settingsPath, json);
            
            // Update preferences sync
            SyncToPreferences();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            throw; // Re-throw so caller knows save failed
        }
    }

    public async Task<bool> ExportDataAsync(string targetFile)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            
            using var archive = ZipFile.Open(targetFile, ZipArchiveMode.Create);
            
            if (File.Exists(_settingsPath))
                archive.CreateEntryFromFile(_settingsPath, Path.GetFileName(_settingsPath));
            
            if (File.Exists(_historyPath))
                archive.CreateEntryFromFile(_historyPath, Path.GetFileName(_historyPath));
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error exporting data: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ImportDataAsync(string sourceFile)
    {
        try
        {
            if (!File.Exists(sourceFile))
                return false;

            // Create backup of existing files
            var backupFolder = Path.Combine(_documentsPath, $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupFolder);
            
            if (File.Exists(_settingsPath))
                File.Copy(_settingsPath, Path.Combine(backupFolder, Path.GetFileName(_settingsPath)));
            
            if (File.Exists(_historyPath))
                File.Copy(_historyPath, Path.Combine(backupFolder, Path.GetFileName(_historyPath)));

            // Extract imported data
            ZipFile.ExtractToDirectory(sourceFile, _documentsPath, true);
            
            Load(); // Reload settings after import
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error importing data: {ex.Message}");
            return false;
        }
    }

    public string GetDataFolderPath() => _documentsPath;

    // LinkId management methods
    public LinkIdMode GetLinkIdMode() => Settings.LinkIdMode;
    public void SetLinkIdMode(LinkIdMode value) { Settings.LinkIdMode = value; Save(); }

    public string GetWallpaperLinkId()
    {
        return Settings.LinkIdMode == LinkIdMode.SharedLink 
            ? GetSharedLinkId() 
            : Settings.WallpaperLinkId;
    }

    public string GetLockscreenLinkId()
    {
        return Settings.LinkIdMode == LinkIdMode.SharedLink 
            ? GetSharedLinkId() 
            : Settings.LockscreenLinkId;
    }

    public string GetSharedLinkId()
    {
        // Return WallpaperLinkId as the shared link, or legacy LinkId if available
        return !string.IsNullOrEmpty(Settings.WallpaperLinkId) 
            ? Settings.WallpaperLinkId 
            : Settings.LinkId;
    }

    public string GetPrimaryLinkId()
    {
        // Return the primary LinkId for backward compatibility
        return GetWallpaperLinkId();
    }

    public void SetWallpaperLinkId(string value) 
    { 
        Settings.WallpaperLinkId = value?.Trim() ?? string.Empty;
        
        // If in shared mode, also update the legacy LinkId
        if (Settings.LinkIdMode == LinkIdMode.SharedLink)
        {
            Settings.LinkId = Settings.WallpaperLinkId;
        }
        
        Save(); 
    }

    public void SetLockscreenLinkId(string value) 
    { 
        Settings.LockscreenLinkId = value?.Trim() ?? string.Empty; 
        Save(); 
    }

    public void SetSharedLinkId(string value)
    {
        var trimmedValue = value?.Trim() ?? string.Empty;
        Settings.WallpaperLinkId = trimmedValue;
        Settings.LockscreenLinkId = trimmedValue;
        Settings.LinkId = trimmedValue; // Legacy compatibility
        Save();
    }

    // Legacy method for backward compatibility
    public string GetLinkId() => GetPrimaryLinkId();
    public void SetLinkId(string value) 
    {
        var trimmedValue = value?.Trim() ?? string.Empty;
        Settings.LinkId = trimmedValue;
        
        // If new LinkIds are empty, set them too for migration
        if (string.IsNullOrEmpty(Settings.WallpaperLinkId) && string.IsNullOrEmpty(Settings.LockscreenLinkId))
        {
            Settings.WallpaperLinkId = trimmedValue;
            Settings.LockscreenLinkId = trimmedValue;
        }
        
        Save();
    }

    public string GetApiKey() => Settings.ApiKey;
    public void SetApiKey(string value) { Settings.ApiKey = value?.Trim() ?? string.Empty; Save(); }

    public string GetPanicUrl() => Settings.PanicUrl;
    public void SetPanicUrl(string value) 
    { 
        Settings.PanicUrl = value?.Trim() ?? string.Empty; 
        Save(); 
        
        // Also store in Preferences for Android notification access
        Preferences.Set("panic_url", Settings.PanicUrl);
    }

    public string GetPanicFilePath() => Settings.PanicFilePath;
    public void SetPanicFilePath(string value) 
    { 
        Settings.PanicFilePath = value?.Trim() ?? string.Empty; 
        Save(); 
        
        // Also store in Preferences for Android notification access
        Preferences.Set("panic_file_path", Settings.PanicFilePath);
    }

    public int GetPollingIntervalSeconds() => Math.Max(5, Settings.PollingIntervalSeconds);
    public void SetPollingIntervalSeconds(int value) { Settings.PollingIntervalSeconds = Math.Max(5, value); Save(); }

    public bool GetWifiOnly() => Settings.WifiOnly;
    public void SetWifiOnly(bool value) 
    { 
        Settings.WifiOnly = value; 
        Save(); 
        
        // Also store in Preferences for Android service access
        Preferences.Set("wifi_only", Settings.WifiOnly);
    }

    public string GetWallpaperSaveFolder()
    {
        if (!string.IsNullOrEmpty(Settings.WallpaperSaveFolder))
        {
            return Settings.WallpaperSaveFolder;
        }

        // Return platform-specific default folder if not set
        var defaultFolder = GetDefaultWallpaperSaveFolder();
        
        // Automatically set and save the default folder
        Settings.WallpaperSaveFolder = defaultFolder;
        Save();
        
        return defaultFolder;
    }

    public void SetWallpaperSaveFolder(string value) { Settings.WallpaperSaveFolder = value?.Trim() ?? string.Empty; Save(); }

    public int GetMaxHistoryLimit() => Math.Max(0, Settings.MaxHistoryLimit);
    public void SetMaxHistoryLimit(int value) { Settings.MaxHistoryLimit = Math.Max(0, value); Save(); }

    public bool IsHistoryEnabled() => GetMaxHistoryLimit() > 0;

    // New caching settings methods
    public bool GetEnableImageCache() => Settings.EnableImageCache;
    public void SetEnableImageCache(bool value) { Settings.EnableImageCache = value; Save(); }

    public int GetMaxCacheSizeMB() => Math.Max(10, Math.Min(1000, Settings.MaxCacheSizeMB)); // Between 10MB and 1GB
    public void SetMaxCacheSizeMB(int value) { Settings.MaxCacheSizeMB = Math.Max(10, Math.Min(1000, value)); Save(); }

    public int GetCacheExpiryDays() => Math.Max(1, Math.Min(30, Settings.CacheExpiryDays)); // Between 1 and 30 days
    public void SetCacheExpiryDays(int value) { Settings.CacheExpiryDays = Math.Max(1, Math.Min(30, value)); Save(); }

    // New wallpaper fit settings methods
    public WallpaperFitMode GetWallpaperFitMode() => Settings.WallpaperFitMode;
    public void SetWallpaperFitMode(WallpaperFitMode value) { Settings.WallpaperFitMode = value; Save(); }

    // Autostart settings methods
    public bool GetAutoStartEnabled() => Settings.AutoStartEnabled;
    public void SetAutoStartEnabled(bool value) { Settings.AutoStartEnabled = value; Save(); }

    public int GetAutoStartIntervalHours() => Math.Max(1, Math.Min(24, Settings.AutoStartIntervalHours)); // Between 1 and 24 hours
    public void SetAutoStartIntervalHours(int value) { Settings.AutoStartIntervalHours = Math.Max(1, Math.Min(24, value)); Save(); }

    // Helper method to get LinkId for specific wallpaper type
    public string GetLinkIdForType(WallpaperType wallpaperType)
    {
        return wallpaperType == WallpaperType.Lockscreen 
            ? GetLockscreenLinkId() 
            : GetWallpaperLinkId();
    }

    // Network connectivity helper for debugging
    public string GetNetworkStatusInfo()
    {
        try
        {
            var networkAccess = Connectivity.NetworkAccess;
            var profiles = Connectivity.ConnectionProfiles;
            var wifiOnly = GetWifiOnly();
            
            return $"Network Access: {networkAccess}, " +
                   $"Profiles: {string.Join(", ", profiles)}, " +
                   $"Wi-Fi Only: {wifiOnly}, " +
                   $"Has Wi-Fi: {profiles.Contains(ConnectionProfile.WiFi)}";
        }
        catch (Exception ex)
        {
            return $"Error getting network status: {ex.Message}";
        }
    }

    private string GetDefaultWallpaperSaveFolder()
    {
#if ANDROID
        // Android: Use /Internal storage/AnonClient/pictures/
        var externalStorageDir = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
        if (!string.IsNullOrEmpty(externalStorageDir))
        {
            return Path.Combine(externalStorageDir, "AnonClient", "pictures");
        }
        // Fallback: Use internal Pictures/AnonClient
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(picturesPath, "AnonClient");
#elif WINDOWS
        // Windows: Use Pictures/AnonClient
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(picturesPath, "AnonClient");
#elif IOS || MACCATALYST
        // iOS/macOS: Use Documents/AnonClient/pictures (more accessible than Pictures on iOS)
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AnonClient", "pictures");
#else
        // Fallback for other platforms
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AnonClient", "pictures");
#endif
    }
}
