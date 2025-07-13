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
        // The real InitializeComponent() is auto-generated, so we don't need a manual one.
        // This call will now work correctly.
        this.InitializeComponent();
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
            // Use the simpler 'Icon' property with a direct path to your .ico file
            Icon = "Images/appicon.ico"
        };

        // Use the library's command system with fully qualified names to avoid ambiguity
        trayIcon.LeftClickCommand = new Microsoft.Maui.Controls.Command(ShowWindow);

        // The correct property is ContextMenu, and it expects a native WinUI MenuFlyout
        trayIcon.ContextMenu = new MenuFlyout
        {
            Items =
            {
                new MenuFlyoutItem { Text = "Open", Command = new Microsoft.Maui.Controls.Command(ShowWindow) },
                new MenuFlyoutItem { Text = "Panic", Command = new Microsoft.Maui.Controls.Command(PanicAndExit) },
                new MenuFlyoutSeparator(),
                new MenuFlyoutItem { Text = "Exit", Command = new Microsoft.Maui.Controls.Command(ExitApplication) }
            }
        };

        trayIcon.ForceCreate();

        var windowId = Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow));
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Closing += (sender, e) =>
        {
            e.Cancel = true;
            sender.Hide();
        };
    }

    private void ShowWindow()
    {
        var mauiWindow = global::AnonWallClient.App.Current?.Windows.FirstOrDefault();
        mauiWindow?.Show();
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

    private void ExitApplication()
    {
        global::AnonWallClient.App.Current?.Quit();
    }
}