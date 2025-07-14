using AnonWallClient.Services;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
// Use the MAUI Controls namespace
using Microsoft.Maui.Controls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using WinRT.Interop;

namespace AnonWallClient.Platforms.Windows;

public partial class App : MauiWinUIApplication
{
    private TaskbarIcon? trayIcon;

    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var nativeWindow = Application.Windows[0].Handler!.PlatformView as Microsoft.UI.Xaml.Window;
        if (nativeWindow == null) return;

        //var iconPath = Path.Combine(AppContext.BaseDirectory, "Images/appicon.ico");
        var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");

        // --- The Definitive Solution: Use MAUI Controls ---

        // 1. Create a .NET MAUI MenuFlyout.
        var flyout = new Microsoft.Maui.Controls.MenuFlyout();

        // 2. Create MAUI MenuFlyoutItems and assign their Clicked event.
        var openItem = new Microsoft.Maui.Controls.MenuFlyoutItem { Text = "Open" };
        openItem.Clicked += (s, e) => ShowWindow();
        flyout.Add(openItem);

        var panicItem = new Microsoft.Maui.Controls.MenuFlyoutItem { Text = "Panic" };
        panicItem.Clicked += (s, e) => PanicAndExit();
        flyout.Add(panicItem);

        flyout.Add(new Microsoft.Maui.Controls.MenuFlyoutSeparator());

        var exitItem = new Microsoft.Maui.Controls.MenuFlyoutItem { Text = "Exit" };
        exitItem.Clicked += (s, e) => ExitApplication();
        flyout.Add(exitItem);

        // 3. Initialize the tray icon.
        trayIcon = new TaskbarIcon
        {
            ToolTipText = "AnonWallClient",
            Icon = new System.Drawing.Icon(iconPath),
        };

        // 4. Attach the MAUI flyout using the standard MAUI attached property.
        FlyoutBase.SetContextFlyout(trayIcon, flyout);

        // 5. Assign a command for a standard left-click action.
        trayIcon.LeftClickCommand = new Microsoft.Maui.Controls.Command(ShowWindow);

        trayIcon.ForceCreate();

        var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Closing += (sender, e) =>
        {
            e.Cancel = true;
            sender.Hide();
        };
    }

    private void ShowWindow()
    {
        var mauiWindow = global::AnonWallClient.App.Current?.Windows[0];
        if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Show();
        }
    }

    private void PanicAndExit()
    {
        var panicPath = Preferences.Get("panic_file_path", string.Empty);
        if (string.IsNullOrEmpty(panicPath))
        {
            panicPath = Preferences.Get("panic_url", string.Empty);
        }

        if (!string.IsNullOrEmpty(panicPath) && MauiProgram.Services is not null)
        {
            var wallpaperService = MauiProgram.Services.GetService<IWallpaperService>();
            _ = wallpaperService?.SetWallpaperAsync(panicPath);
        }
        ExitApplication();
    }

    private void ExitApplication()
    {
        trayIcon?.Dispose();
        global::AnonWallClient.App.Current?.Quit();
    }
}