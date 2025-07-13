using System.Runtime.InteropServices;
using AnonWallClient.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AnonWallClient.Platforms.Windows;

public partial class WallpaperService : IWallpaperService
{
    private readonly HttpClient _httpClient;
    private readonly AppLogService _logger;

    public WallpaperService(IHttpClientFactory httpClientFactory, AppLogService logger)
    {
        _httpClient = httpClientFactory.CreateClient("WalltakerClient");
        _logger = logger;
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    public async Task<bool> SetWallpaperAsync(string imagePathOrUrl)
    {
        _logger.Add("Attempting to set Windows wallpaper...");
        try
        {
            string tempPath;
            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                using var response = await _httpClient.GetAsync(imagePathOrUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Add($"Failed to download image. Status: {response.StatusCode}");
                    return false;
                }

                tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.jpg");
                await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);
                await fs.DisposeAsync();
            }
            else
            {
                tempPath = imagePathOrUrl;
            }

            int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            if (result != 0)
            {
                _logger.Add("Wallpaper set successfully.");
                return true;
            }
            else
            {
                _logger.Add("System call failed to set wallpaper.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"ERROR setting Windows wallpaper: {ex.Message}");
            return false;
        }
    }
}