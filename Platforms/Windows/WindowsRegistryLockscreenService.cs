using Microsoft.Win32;
using AnonWallClient.Services;

namespace AnonWallClient.Platforms.Windows;

public class WindowsRegistryLockscreenService
{
    private readonly AppLogService _logger;
    private readonly SettingsService _settingsService;
    
    // Registry paths for Windows lockscreen
    private const string PERSONALIZATION_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP";
    private const string LOCK_SCREEN_IMAGE_PATH = "LockScreenImagePath";
    private const string LOCK_SCREEN_IMAGE_URL = "LockScreenImageUrl";
    private const string LOCK_SCREEN_IMAGE_STATUS = "LockScreenImageStatus";
    
    // Backup storage in app settings
    private const string BACKUP_IMAGE_PATH_KEY = "OriginalLockScreenImagePath";
    private const string BACKUP_IMAGE_URL_KEY = "OriginalLockScreenImageUrl";
    private const string BACKUP_IMAGE_STATUS_KEY = "OriginalLockScreenImageStatus";

    public WindowsRegistryLockscreenService(AppLogService logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async Task<bool> SetLockscreenWallpaperAsync(string imagePath)
    {
        try
        {
            _logger.Add("Windows Registry: Attempting to set lockscreen wallpaper via registry...");

            // First, backup current registry values
            await BackupCurrentLockscreenSettingsAsync();

            // Open registry key with write access
            using var key = Registry.LocalMachine.CreateSubKey(PERSONALIZATION_KEY, true);
            if (key == null)
            {
                _logger.Add("Windows Registry: Failed to open/create personalization registry key.");
                return false;
            }

            // Convert relative path to absolute if needed
            var absolutePath = Path.IsPathRooted(imagePath) ? imagePath : Path.GetFullPath(imagePath);
            
            // Verify image file exists
            if (!File.Exists(absolutePath))
            {
                _logger.Add($"Windows Registry: Image file not found: {absolutePath}");
                return false;
            }

            _logger.Add($"Windows Registry: Setting lockscreen to: {absolutePath}");

            // Set the lockscreen image path
            key.SetValue(LOCK_SCREEN_IMAGE_PATH, absolutePath, RegistryValueKind.String);
            key.SetValue(LOCK_SCREEN_IMAGE_STATUS, 1, RegistryValueKind.DWord); // 1 = enabled
            
            // Clear URL if it was previously set
            try
            {
                key.DeleteValue(LOCK_SCREEN_IMAGE_URL, false);
            }
            catch
            {
                // Ignore if value doesn't exist
            }

            _logger.Add("Windows Registry: Lockscreen wallpaper registry values updated successfully.");

            // Force Windows to refresh the lockscreen
            await ForceWindowsLockscreenRefreshAsync();

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.Add("Windows Registry: Access denied. Application may need to run as administrator to modify lockscreen settings.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Registry: Error setting lockscreen wallpaper: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RestoreOriginalLockscreenAsync()
    {
        try
        {
            _logger.Add("Windows Registry: Restoring original lockscreen settings...");

            // Get backed up values from settings
            var originalPath = Preferences.Get(BACKUP_IMAGE_PATH_KEY, string.Empty);
            var originalUrl = Preferences.Get(BACKUP_IMAGE_URL_KEY, string.Empty);
            var originalStatus = Preferences.Get(BACKUP_IMAGE_STATUS_KEY, -1);

            if (string.IsNullOrEmpty(originalPath) && string.IsNullOrEmpty(originalUrl) && originalStatus == -1)
            {
                _logger.Add("Windows Registry: No backup found. Clearing lockscreen settings instead.");
                return await ClearCustomLockscreenAsync();
            }

            using var key = Registry.LocalMachine.CreateSubKey(PERSONALIZATION_KEY, true);
            if (key == null)
            {
                _logger.Add("Windows Registry: Failed to open personalization registry key for restore.");
                return false;
            }

            // Restore original values
            if (!string.IsNullOrEmpty(originalPath))
            {
                key.SetValue(LOCK_SCREEN_IMAGE_PATH, originalPath, RegistryValueKind.String);
                _logger.Add($"Windows Registry: Restored original image path: {originalPath}");
            }
            else
            {
                try { key.DeleteValue(LOCK_SCREEN_IMAGE_PATH, false); } catch { }
            }

            if (!string.IsNullOrEmpty(originalUrl))
            {
                key.SetValue(LOCK_SCREEN_IMAGE_URL, originalUrl, RegistryValueKind.String);
                _logger.Add($"Windows Registry: Restored original image URL: {originalUrl}");
            }
            else
            {
                try { key.DeleteValue(LOCK_SCREEN_IMAGE_URL, false); } catch { }
            }

            if (originalStatus != -1)
            {
                key.SetValue(LOCK_SCREEN_IMAGE_STATUS, originalStatus, RegistryValueKind.DWord);
                _logger.Add($"Windows Registry: Restored original status: {originalStatus}");
            }
            else
            {
                try { key.DeleteValue(LOCK_SCREEN_IMAGE_STATUS, false); } catch { }
            }

            // Clear backup values
            Preferences.Remove(BACKUP_IMAGE_PATH_KEY);
            Preferences.Remove(BACKUP_IMAGE_URL_KEY);
            Preferences.Remove(BACKUP_IMAGE_STATUS_KEY);

            await ForceWindowsLockscreenRefreshAsync();

            _logger.Add("Windows Registry: Original lockscreen settings restored successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Registry: Error restoring lockscreen: {ex.Message}");
            return false;
        }
    }

    private async Task BackupCurrentLockscreenSettingsAsync()
    {
        try
        {
            _logger.Add("Windows Registry: Backing up current lockscreen settings...");

            using var key = Registry.LocalMachine.OpenSubKey(PERSONALIZATION_KEY, false);
            if (key == null)
            {
                _logger.Add("Windows Registry: No existing personalization key found - creating new backup with defaults.");
                return;
            }

            // Backup current values
            var currentPath = key.GetValue(LOCK_SCREEN_IMAGE_PATH)?.ToString() ?? string.Empty;
            var currentUrl = key.GetValue(LOCK_SCREEN_IMAGE_URL)?.ToString() ?? string.Empty;
            var currentStatus = key.GetValue(LOCK_SCREEN_IMAGE_STATUS) is int status ? status : -1;

            // Store backup in Preferences (survives app restarts)
            Preferences.Set(BACKUP_IMAGE_PATH_KEY, currentPath);
            Preferences.Set(BACKUP_IMAGE_URL_KEY, currentUrl);
            Preferences.Set(BACKUP_IMAGE_STATUS_KEY, currentStatus);

            _logger.Add($"Windows Registry: Backed up - Path: '{currentPath}', URL: '{currentUrl}', Status: {currentStatus}");
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Registry: Warning - Could not backup current settings: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }

    private async Task<bool> ClearCustomLockscreenAsync()
    {
        try
        {
            _logger.Add("Windows Registry: Clearing custom lockscreen settings...");

            using var key = Registry.LocalMachine.CreateSubKey(PERSONALIZATION_KEY, true);
            if (key == null) return false;

            // Remove custom lockscreen settings to revert to Windows default
            try { key.DeleteValue(LOCK_SCREEN_IMAGE_PATH, false); } catch { }
            try { key.DeleteValue(LOCK_SCREEN_IMAGE_URL, false); } catch { }
            try { key.DeleteValue(LOCK_SCREEN_IMAGE_STATUS, false); } catch { }

            await ForceWindowsLockscreenRefreshAsync();

            _logger.Add("Windows Registry: Custom lockscreen settings cleared - reverted to Windows default.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Registry: Error clearing lockscreen: {ex.Message}");
            return false;
        }
    }

    private async Task ForceWindowsLockscreenRefreshAsync()
    {
        try
        {
            _logger.Add("Windows Registry: Forcing Windows to refresh lockscreen...");

            // Method 1: Trigger Group Policy refresh
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "gpupdate",
                Arguments = "/force",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    _logger.Add("Windows Registry: Group Policy refresh completed.");
                }
            }
            catch
            {
                _logger.Add("Windows Registry: Group Policy refresh failed, but lockscreen should still update on next lock.");
            }

            // Method 2: Try to trigger personalization refresh via PowerShell
            try
            {
                var psInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"& {Get-Process explorer | Stop-Process -Force; Start-Process explorer}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas" // May prompt for admin rights
                };

                // This is optional and might fail without admin rights
                using var psProcess = System.Diagnostics.Process.Start(psInfo);
                if (psProcess != null)
                {
                    await psProcess.WaitForExitAsync();
                    _logger.Add("Windows Registry: Explorer restart completed.");
                }
            }
            catch
            {
                _logger.Add("Windows Registry: Explorer restart failed (may require admin rights). Lockscreen will update on next lock.");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Registry: Error during refresh: {ex.Message}");
        }
    }

    public bool HasBackupStored()
    {
        var hasPath = !string.IsNullOrEmpty(Preferences.Get(BACKUP_IMAGE_PATH_KEY, string.Empty));
        var hasUrl = !string.IsNullOrEmpty(Preferences.Get(BACKUP_IMAGE_URL_KEY, string.Empty));
        var hasStatus = Preferences.Get(BACKUP_IMAGE_STATUS_KEY, -1) != -1;
        
        return hasPath || hasUrl || hasStatus;
    }

    public async Task<bool> IsRegistryMethodAvailableAsync()
    {
        try
        {
            // Test if we can access the registry key
            using var key = Registry.LocalMachine.OpenSubKey(PERSONALIZATION_KEY, false);
            return true; // If we can open it for reading, method is available
        }
        catch
        {
            return false; // Registry access not available
        }
    }
}
