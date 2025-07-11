using Android.App;
using Android.Graphics;
using AnonWallClient.Services;

namespace AnonWallClient.Platforms.Android;

public class WallpaperService(HttpClient httpClient, AppLogService logger) : IWallpaperService
{
    public async Task<bool> SetWallpaperAsync(string imageUrl)
    {
        logger.Add("Android Service: SetWallpaperAsync called.");
        try
        {
            var wallpaperManager = WallpaperManager.GetInstance(global::Android.App.Application.Context);
            if (wallpaperManager == null)
            {
                logger.Add("Android Service ERROR: WallpaperManager instance is null.");
                return false;
            }
            logger.Add("Android Service: Got WallpaperManager instance.");

            logger.Add("Android Service: Starting image download...");
            using var response = await httpClient.GetAsync(imageUrl);
            logger.Add($"Android Service: HTTP response received with status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                logger.Add("Android Service ERROR: Download failed.");
                return false;
            }

            logger.Add("Android Service: Reading image stream...");
            using var stream = await response.Content.ReadAsStreamAsync();
            logger.Add("Android Service: Decoding stream to bitmap...");
            using var bitmap = await BitmapFactory.DecodeStreamAsync(stream);

            if (bitmap != null)
            {
                logger.Add($"Android Service: Bitmap decoded successfully. Size: {bitmap.Width}x{bitmap.Height}");
                logger.Add("Android Service: Calling native SetBitmap...");

                wallpaperManager.SetBitmap(bitmap);

                logger.Add("Android Service: Native SetBitmap call completed."); // We may not see this if it crashes
                return true;
            }
            else
            {
                logger.Add("Android Service ERROR: Failed to decode image into bitmap.");
                return false;
            }
        }
        catch (Exception ex)
        {
            // This will catch both C# and Java exceptions
            logger.Add($"Android Service FATAL ERROR: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }
}