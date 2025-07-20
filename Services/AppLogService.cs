using System.Collections.ObjectModel;
using System.Text;

namespace AnonWallClient.Services;

public class AppLogService
{
    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly int _maxLogLines = 1000; // Keep last 1000 lines in memory
    private readonly int _maxLogFileSize = 10 * 1024 * 1024; // 10MB max log file
    private readonly object _fileLock = new object();

    // A collection that notifies the UI when it's updated
    public ObservableCollection<string> Logs { get; } = new();

    public AppLogService()
    {
        _logDirectory = GetLogDirectory();
        _logFilePath = Path.Combine(_logDirectory, "app.log");
        
        // Ensure log directory exists
        try
        {
            // Request storage permissions on Android first
#if ANDROID
            _ = Task.Run(async () =>
            {
                try
                {
                    // Check if we need external storage permissions
                    if (_logDirectory.Contains("/storage/emulated/0"))
                    {
                        var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                        if (status != PermissionStatus.Granted)
                        {
                            // Don't request here - will be requested by UI
                            System.Diagnostics.Debug.WriteLine("AppLogService: Storage permissions not granted, may fall back to app directory");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking storage permissions: {ex.Message}");
                }
            });
#endif

            Directory.CreateDirectory(_logDirectory);
            
            // Rotate log file if it's too large
            RotateLogFileIfNeeded();
            
            Add("AppLogService initialized with file logging");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing log service: {ex.Message}");
            
            // Try fallback to app directory on Android
#if ANDROID
            try
            {
                _logDirectory = Path.Combine(FileSystem.AppDataDirectory, "logs");
                _logFilePath = Path.Combine(_logDirectory, "app.log");
                Directory.CreateDirectory(_logDirectory);
                Add($"AppLogService initialized with fallback directory: {_logDirectory}");
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback log directory also failed: {fallbackEx.Message}");
                Add($"Warning: File logging may not work - {fallbackEx.Message}");
            }
#else
            Add($"Warning: File logging may not work - {ex.Message}");
#endif
        }
    }

    private string GetLogDirectory()
    {
#if ANDROID
        // Use internal storage: /Internal storage/AnonClient/logs/
        var externalStorageDir = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
        if (!string.IsNullOrEmpty(externalStorageDir))
        {
            return Path.Combine(externalStorageDir, "AnonClient", "logs");
        }
        // Fallback to app-specific external storage
        return Path.Combine(FileSystem.AppDataDirectory, "logs");
#else
        // Other platforms: Documents/AnonClient/logs/
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AnonClient", "logs");
#endif
    }

    public void Add(string message)
    {
        var timestamp = DateTime.Now;
        string entry = $"{timestamp:yyyy-MM-dd HH:mm:ss.fff} - {message}";

        // Write to file first (background thread)
        _ = Task.Run(() => WriteToFile(entry));

        // All UI updates must happen on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Add the new log to the top of the list
                Logs.Insert(0, $"{timestamp:HH:mm:ss} - {message}");

                // Keep the log from growing too large in memory
                while (Logs.Count > _maxLogLines)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating log UI: {ex.Message}");
            }
        });
    }

    private void WriteToFile(string entry)
    {
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, entry + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing to log file: {ex.Message}");
        }
    }

    private void RotateLogFileIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFilePath)) return;

            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Length > _maxLogFileSize)
            {
                // Create backup with timestamp
                var backupPath = Path.Combine(_logDirectory, $"app_backup_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.Move(_logFilePath, backupPath);

                // Clean up old backup files (keep only last 5)
                CleanupOldLogFiles();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rotating log file: {ex.Message}");
        }
    }

    private void CleanupOldLogFiles()
    {
        try
        {
            var logFiles = Directory.GetFiles(_logDirectory, "app_backup_*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(5) // Keep only 5 most recent backup files
                .ToList();

            foreach (var file in logFiles)
            {
                file.Delete();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cleaning up old log files: {ex.Message}");
        }
    }

    public async Task<string> ExportLogsAsync()
    {
        try
        {
            lock (_fileLock)
            {
                if (File.Exists(_logFilePath))
                {
                    return File.ReadAllText(_logFilePath, Encoding.UTF8);
                }
            }
        }
        catch (Exception ex)
        {
            Add($"Error exporting logs: {ex.Message}");
        }
        
        return string.Empty;
    }

    public string GetLogDirectoryPath() => _logDirectory;

    public void ClearLogs()
    {
        try
        {
            // Clear UI logs
            MainThread.BeginInvokeOnMainThread(() => Logs.Clear());

            // Clear log file
            lock (_fileLock)
            {
                if (File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
            }

            Add("Logs cleared");
        }
        catch (Exception ex)
        {
            Add($"Error clearing logs: {ex.Message}");
        }
    }
}
