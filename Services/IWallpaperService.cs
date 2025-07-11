namespace AnonWallClient.Services; // Changed namespace

public interface IWallpaperService
{
    Task<bool> SetWallpaperAsync(string imageUrl);
}