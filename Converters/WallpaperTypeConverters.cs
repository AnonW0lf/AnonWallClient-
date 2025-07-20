using System.Globalization;
using AnonWallClient.Services;

namespace AnonWallClient.Converters;

public class WallpaperTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WallpaperType wallpaperType)
        {
            return wallpaperType switch
            {
                WallpaperType.Wallpaper => "HOME",
                WallpaperType.Lockscreen => "LOCK",
                _ => "HOME"
            };
        }
        return "HOME";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class WallpaperTypeToBadgeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WallpaperType wallpaperType)
        {
            return wallpaperType switch
            {
                WallpaperType.Wallpaper => Color.FromArgb("#007ACC"), // Blue for wallpaper
                WallpaperType.Lockscreen => Color.FromArgb("#FF6B35"), // Orange for lockscreen
                _ => Color.FromArgb("#007ACC")
            };
        }
        return Color.FromArgb("#007ACC");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
