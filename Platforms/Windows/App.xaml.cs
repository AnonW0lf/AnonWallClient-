using AnonWallClient.Services;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnonWallClient.Platforms.Windows;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var nativeWindow = Application.Windows[0].Handler!.PlatformView as Window;
        if (nativeWindow == null) return;

        var trayIcon = new TaskbarIcon
        {
            ToolTipText = "AnonWallClient",
            Icon = "Images/appicon.ico"
        };

        var flyout = new MenuFlyout();

        var openItem = new MenuFlyoutItem { Text = "Open" };
        openItem.Click += (s, e) => ShowWindow();
        flyout.Items.Add(openItem);

        var panicItem = new MenuFlyoutItem { Text = "Panic" };
        panicItem.Click += (s, e) => PanicAndExit();
        flyout.Items.Add(panicItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (s, e) => ExitApplication();
        flyout.Items.Add(exitItem);

        trayIcon.ContextFlyout = flyout;
        trayIcon.ForceCreate();

        var windowId = Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow));
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Closing += (sender, e) =>
        {
            e.Cancel = true;
            sender.Hide();
        };
    }

    private void ShowWindow() => global::AnonWallClient.App.Current?.Windows.FirstOrDefault()?.Show();
    private void ExitApplication() => global::AnonWallClient.App.Current?.Quit();

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
}