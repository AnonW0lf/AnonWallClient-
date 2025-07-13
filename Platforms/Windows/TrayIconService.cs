using AnonWallClient.Services;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;

namespace AnonWallClient.Platforms.Windows;

public class TrayIconService
{
    public TrayIconService() { }

    public void Initialize(Microsoft.UI.Xaml.Window nativeWindow)
    {
        var iconStream = File.OpenRead("Images/appicon.ico");

        var trayIcon = new TaskbarIcon
        {
            ToolTipText = "AnonWallClient",
            Icon = new System.Drawing.Icon(iconStream),
        };

        // Use the fully qualified names for all WinUI controls
        var flyout = new Microsoft.UI.Xaml.Controls.MenuFlyout();

        var openItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Open" };
        openItem.Click += (s, e) => ShowWindow();
        flyout.Items.Add(openItem);

        var panicItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Panic" };
        panicItem.Click += (s, e) => PanicAndExit();
        flyout.Items.Add(panicItem);

        flyout.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());

        var exitItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (s, e) => ExitApplication();
        flyout.Items.Add(exitItem);

        trayIcon.ContextMenu = flyout;
        trayIcon.ForceCreate();

        // Hook into the AppWindow Closing event
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow));
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Closing += (sender, args) =>
        {
            args.Cancel = true;
            sender.Hide();
        };
    }

    private void ShowWindow()
    {
        var window = App.Current?.Windows.FirstOrDefault();
        window?.Show();
    }

    private void PanicAndExit()
    {
        var panicPath = Preferences.Get("panic_file_path", string.Empty);
        if (string.IsNullOrEmpty(panicPath)) panicPath = Preferences.Get("panic_url", string.Empty);

        if (!string.IsNullOrEmpty(panicPath) && MauiProgram.Services is not null)
        {
            var wallpaperService = MauiProgram.Services.GetService<IWallpaperService>();
            _ = wallpaperService?.SetWallpaperAsync(panicPath);
        }
        ExitApplication();
    }

    private void ExitApplication() => App.Current?.Quit();
}