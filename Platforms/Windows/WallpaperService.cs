using System.Runtime.InteropServices;
using AnonWallClient.Services;

namespace AnonWallClient.Platforms.Windows;

public partial class WallpaperService(HttpClient httpClient, AppLogService logger) : IWallpaperService
{
    // THE FIX IS HERE: We explicitly name the Windows function "SystemParametersInfoW"
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    public async Task<bool> SetWallpaperAsync(string imageUrl)
    {
        logger.Add("Attempting to set Windows wallpaper...");
        try
        {
            using var response = await httpClient.GetAsync(imageUrl);
            if (!response.IsSuccessStatusCode)
            {
                logger.Add($"Failed to download image. Status: {response.StatusCode}");
                return false;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.jpg");
            await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            await response.Content.CopyToAsync(fs);

            await fs.DisposeAsync();

            int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

            if (result != 0)
            {
                logger.Add("Wallpaper set successfully.");
                return true;
            }
            else
            {
                logger.Add("System call failed to set wallpaper.");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.Add($"ERROR setting Windows wallpaper: {ex.Message}");
            return false;
        }
    }
}