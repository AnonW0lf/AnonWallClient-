using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Compression;

namespace AnonWallClient.Services;

public class AppSettings
{
    public string LinkId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string PanicUrl { get; set; } = string.Empty;
    public string PanicFilePath { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 15;
    public bool WifiOnly { get; set; } = false;
    public string WallpaperSaveFolder { get; set; } = string.Empty;
    public int MaxHistoryLimit { get; set; } = 20; // Default to 20, 0 means no history
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
        _documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _settingsPath = Path.Combine(_documentsPath, "AnonWallClient.settings.json");
        _historyPath = Path.Combine(_documentsPath, "AnonWallClient.wallpaper_history.json");
        Load();
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

    private void SyncToPreferences()
    {
        // Sync critical settings to Preferences for Android notification and boot receiver access
        Preferences.Set("LinkId", Settings.LinkId);
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

    // Convenience methods with validation
    public string GetLinkId() => Settings.LinkId;
    public void SetLinkId(string value) 
    { 
        Settings.LinkId = value?.Trim() ?? string.Empty; 
        Save(); 
        
        // Also store in Preferences for Android boot receiver access
        Preferences.Set("LinkId", Settings.LinkId);
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
        // Android: Use Pictures/AnonWallClient
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(picturesPath, "AnonWallClient");
#elif WINDOWS
        // Windows: Use Pictures/AnonWallClient
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(picturesPath, "AnonWallClient");
#elif IOS || MACCATALYST
        // iOS/macOS: Use Documents/AnonWallClient (more accessible than Pictures on iOS)
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AnonWallClient");
#else
        // Fallback for other platforms
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AnonWallClient");
#endif
    }
}
