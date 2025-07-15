using Android.App;
using Android.Graphics;
using AnonWallClient.Services;
using System.IO;

namespace AnonWallClient.Platforms.Android;

public class WallpaperService : IWallpaperService
{
    private readonly HttpClient _httpClient;
    private readonly AppLogService _logger;

    public WallpaperService(IHttpClientFactory httpClientFactory, AppLogService logger)
    {
        _httpClient = httpClientFactory.CreateClient("WalltakerClient");
        _logger = logger;
    }

    public async Task<bool> SetWallpaperAsync(string imagePathOrUrl)
    {
        _logger.Add("Android Service: SetWallpaperAsync called.");
        try
        {
            var wallpaperManager = WallpaperManager.GetInstance(global::Android.App.Application.Context);
            if (wallpaperManager == null)
            {
                _logger.Add("Android Service ERROR: WallpaperManager instance is null.");
                return false;
            }
            _logger.Add("Android Service: Got WallpaperManager instance.");

            Stream imageStream;
            // Check if the input is a web URL or a local file path
            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                _logger.Add("Android Service: Path is a URL, downloading...");
                var response = await _httpClient.GetAsync(imagePathOrUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Add($"Failed to download image. Status: {response.StatusCode}");
                    return false;
                }
                imageStream = await response.Content.ReadAsStreamAsync();
            }
            else
            {
                _logger.Add("Android Service: Path is a local file, opening stream...");
                // It's a local file path, open a stream to it
                imageStream = new FileStream(imagePathOrUrl, FileMode.Open, FileAccess.Read);
            }

            _logger.Add("Android Service: Decoding stream to bitmap...");
            using var bitmap = await BitmapFactory.DecodeStreamAsync(imageStream);
            await imageStream.DisposeAsync(); // Clean up the stream

            if (bitmap != null)
            {
                _logger.Add($"Android Service: Bitmap decoded successfully. Size: {bitmap.Width}x{bitmap.Height}");
                _logger.Add("Android Service: Calling native SetBitmap...");

                wallpaperManager.SetBitmap(bitmap);

                _logger.Add("Android Service: Native SetBitmap call completed.");
                return true;
            }
            else
            {
                _logger.Add("Android Service ERROR: Failed to decode image into bitmap.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Android Service FATAL ERROR: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }
}