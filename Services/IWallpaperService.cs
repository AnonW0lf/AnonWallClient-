namespace AnonWallClient.Services;

public interface IWallpaperService
{
    Task<bool> SetWallpaperAsync(string imageUrl);
    Task<bool> SetWallpaperAsync(string imageUrl, WallpaperFitMode fitMode);
    Task<bool> SetWallpaperAsync(string imageUrl, WallpaperFitMode fitMode, WallpaperType wallpaperType);
}
