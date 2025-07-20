using System.Security.Cryptography;
using System.Text;

namespace AnonWallClient.Services;

public class ImageCacheService
{
    private readonly string _cacheDirectory;
    private readonly SettingsService _settingsService;
    private readonly AppLogService _logger;

    public ImageCacheService(SettingsService settingsService, AppLogService logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        
        _cacheDirectory = GetCacheDirectory();
        
        // Create cache directory if it doesn't exist
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            
            // Clean up expired cache on startup
            _ = Task.Run(CleanupExpiredCacheAsync);
        }
        catch (Exception ex)
        {
            _logger.Add($"Error initializing image cache: {ex.Message}");
        }
    }

    private string GetCacheDirectory()
    {
#if ANDROID
        // Use internal storage: /Internal storage/AnonClient/cache/
        var externalStorageDir = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
        if (!string.IsNullOrEmpty(externalStorageDir))
        {
            return Path.Combine(externalStorageDir, "AnonClient", "cache");
        }
        // Fallback to app-specific cache directory
        return Path.Combine(FileSystem.CacheDirectory, "images");
#else
        // Other platforms: Documents/AnonClient/cache/
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AnonClient", "cache");
#endif
    }

    public async Task<string?> GetCachedImagePathAsync(string imageUrl)
    {
        if (!_settingsService.GetEnableImageCache())
            return null;

        try
        {
            var cacheKey = GenerateCacheKey(imageUrl);
            var cacheFilePath = Path.Combine(_cacheDirectory, cacheKey);
            
            if (File.Exists(cacheFilePath))
            {
                // Check if cache is still valid
                var fileInfo = new FileInfo(cacheFilePath);
                var expiryDate = fileInfo.CreationTime.AddDays(_settingsService.GetCacheExpiryDays());
                
                if (DateTime.Now <= expiryDate)
                {
                    _logger.Add($"Cache hit for image: {imageUrl}");
                    return cacheFilePath;
                }
                else
                {
                    // Cache expired, delete the file
                    File.Delete(cacheFilePath);
                    _logger.Add($"Cache expired for image: {imageUrl}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error checking cache for {imageUrl}: {ex.Message}");
        }
        
        return null;
    }

    public async Task<string?> CacheImageAsync(string imageUrl, byte[] imageData)
    {
        if (!_settingsService.GetEnableImageCache())
            return null;

        try
        {
            // Check cache size limit before adding
            await EnsureCacheSizeLimitAsync();
            
            var cacheKey = GenerateCacheKey(imageUrl);
            var cacheFilePath = Path.Combine(_cacheDirectory, cacheKey);
            
            await File.WriteAllBytesAsync(cacheFilePath, imageData);
            _logger.Add($"Cached image: {imageUrl}");
            
            return cacheFilePath;
        }
        catch (Exception ex)
        {
            _logger.Add($"Error caching image {imageUrl}: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> DownloadAndCacheImageAsync(string imageUrl, HttpClient httpClient)
    {
        if (!_settingsService.GetEnableImageCache())
            return null;

        try
        {
            // First check if already cached
            var cachedPath = await GetCachedImagePathAsync(imageUrl);
            if (cachedPath != null)
                return cachedPath;

            // Download the image
            _logger.Add($"Downloading image for cache: {imageUrl}");
            var imageData = await httpClient.GetByteArrayAsync(imageUrl);
            
            // Cache the downloaded image
            return await CacheImageAsync(imageUrl, imageData);
        }
        catch (Exception ex)
        {
            _logger.Add($"Error downloading and caching image {imageUrl}: {ex.Message}");
            return null;
        }
    }

    private string GenerateCacheKey(string imageUrl)
    {
        // Generate a safe filename from the URL using SHA256 hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(imageUrl));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        // Try to get file extension from URL
        var extension = Path.GetExtension(new Uri(imageUrl).LocalPath);
        if (string.IsNullOrEmpty(extension) || extension.Length > 5)
        {
            // Default to .jpg if no extension or invalid extension
            extension = ".jpg";
        }
        
        return $"{hash}{extension}";
    }

    private async Task EnsureCacheSizeLimitAsync()
    {
        try
        {
            var maxSizeBytes = _settingsService.GetMaxCacheSizeMB() * 1024 * 1024;
            var directoryInfo = new DirectoryInfo(_cacheDirectory);
            
            if (!directoryInfo.Exists)
                return;

            var files = directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.LastAccessTime)
                .ToArray();

            long totalSize = files.Sum(f => f.Length);

            if (totalSize <= maxSizeBytes)
                return;

            _logger.Add($"Cache size ({totalSize / 1024 / 1024}MB) exceeds limit ({maxSizeBytes / 1024 / 1024}MB), cleaning up...");

            // Remove oldest files until under limit
            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                    totalSize -= file.Length;
                    _logger.Add($"Deleted cached file: {file.Name}");

                    if (totalSize <= maxSizeBytes)
                        break;
                }
                catch (Exception ex)
                {
                    _logger.Add($"Error deleting cache file {file.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error managing cache size: {ex.Message}");
        }
    }

    private async Task CleanupExpiredCacheAsync()
    {
        try
        {
            var directoryInfo = new DirectoryInfo(_cacheDirectory);
            
            if (!directoryInfo.Exists)
                return;

            var expiryThreshold = DateTime.Now.AddDays(-_settingsService.GetCacheExpiryDays());
            var files = directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly);

            var expiredCount = 0;
            foreach (var file in files)
            {
                try
                {
                    if (file.CreationTime < expiryThreshold)
                    {
                        file.Delete();
                        expiredCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Add($"Error deleting expired cache file {file.Name}: {ex.Message}");
                }
            }

            if (expiredCount > 0)
            {
                _logger.Add($"Cleaned up {expiredCount} expired cache files");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Error during cache cleanup: {ex.Message}");
        }
    }

    public async Task ClearCacheAsync()
    {
        try
        {
            var directoryInfo = new DirectoryInfo(_cacheDirectory);
            
            if (!directoryInfo.Exists)
                return;

            var files = directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.Add($"Error deleting cache file {file.Name}: {ex.Message}");
                }
            }

            _logger.Add($"Cleared cache: deleted {deletedCount} files");
        }
        catch (Exception ex)
        {
            _logger.Add($"Error clearing cache: {ex.Message}");
        }
    }

    public long GetCacheSizeBytes()
    {
        try
        {
            var directoryInfo = new DirectoryInfo(_cacheDirectory);
            
            if (!directoryInfo.Exists)
                return 0;

            return directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    public int GetCacheFileCount()
    {
        try
        {
            var directoryInfo = new DirectoryInfo(_cacheDirectory);
            
            if (!directoryInfo.Exists)
                return 0;

            return directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly).Length;
        }
        catch
        {
            return 0;
        }
    }

    public string GetCacheInfo()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                return "Cache: 0MB / 0MB, Files: 0";
            }

            var files = Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories);
            var totalSize = files.Sum(file => new FileInfo(file).Length);
            var totalSizeMB = Math.Round(totalSize / (1024.0 * 1024.0), 1);
            var maxSizeMB = _settingsService.GetMaxCacheSizeMB();

            return $"Cache: {totalSizeMB}MB / {maxSizeMB}MB, Files: {files.Length}";
        }
        catch (Exception ex)
        {
            return $"Cache info error: {ex.Message}";
        }
    }
}
