using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using AnonWallClient.Services;
using Microsoft.Extensions.DependencyInjection;

// Add reference to Windows Script Host Object Model (IWshRuntimeLibrary)
using System.Reflection;

namespace AnonWallClient.Platforms.Windows;

public partial class App : MauiWinUIApplication
{
    // Win32 constants and structures
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONUP = 0x0202;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern int AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private IntPtr _trayMenu;
    private IntPtr _hIcon;
    private IntPtr _windowHandle;
    private readonly int _trayMenuOpenId = 1;
    private readonly int _trayMenuPanicId = 2;
    private readonly int _trayMenuExitId = 3;
    private bool _isMinimizedToTray;
    private Icon? _icon;
    private AppLogService? _logger;

    public App()
    {
        this.InitializeComponent();
        EnsureStartMenuShortcut();
    }

    private void EnsureStartMenuShortcut()
    {
        try
        {
            string shortcutName = "AnonWallClient.lnk";
            string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", shortcutName);
            if (!File.Exists(startMenuPath))
            {
                string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    var shortcut = shell.CreateShortcut(startMenuPath);
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                    shortcut.WindowStyle = 1;
                    shortcut.Description = "AnonWallClient";
                    shortcut.Save();
                }
            }
            // Set AppUserModelID using Win32 API
            SetCurrentProcessExplicitAppUserModelID("AnonWallClient");
        }
        catch { /* Ignore errors for shortcut creation */ }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var nativeWindow = Application.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (nativeWindow == null) return;

        _windowHandle = WindowNative.GetWindowHandle(nativeWindow);
        _logger = MauiProgram.Services?.GetService<AppLogService>();

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
            SetupSystemTray(iconPath);

            var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            
            // Subclass the window to intercept messages
            WindowSubclasser.Subclass(_windowHandle, WndProc);

            appWindow.Closing += (sender, e) =>
            {
                e.Cancel = true;
                MinimizeToTray(sender);
            };
        }
        catch (Exception ex)
        {
            _logger?.Add($"Error in OnLaunched: {ex.Message}");
        }
    }

    private void SetupSystemTray(string iconPath)
    {
        try
        {
            _icon = new Icon(iconPath);
            _hIcon = _icon.Handle;
            _trayMenu = CreatePopupMenu();
            
            AppendMenu(_trayMenu, 0, (uint)_trayMenuOpenId, "Open");
            AppendMenu(_trayMenu, 0, (uint)_trayMenuPanicId, "Panic");
            AppendMenu(_trayMenu, 0x800, 0, ""); // Separator
            AppendMenu(_trayMenu, 0, (uint)_trayMenuExitId, "Exit");

            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _windowHandle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "AnonWallClient"
            };
            Shell_NotifyIcon(NIM_ADD, ref data);
        }
        catch (Exception ex)
        {
            _logger?.Add($"Error in SetupSystemTray: {ex.Message}");
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            int eventType = lParam.ToInt32();
            if (eventType == WM_RBUTTONUP)
            {
                SetForegroundWindow(hWnd);
                POINT pt;
                GetCursorPos(out pt);
                int selected = TrackPopupMenu(_trayMenu, 0x0100, pt.X, pt.Y, 0, hWnd, IntPtr.Zero);
                
                if (selected == _trayMenuOpenId)
                {
                    ShowWindow();
                    _isMinimizedToTray = false;
                }
                else if (selected == _trayMenuPanicId)
                {
                    PanicAndExit();
                }
                else if (selected == _trayMenuExitId)
                {
                    ExitApplication();
                }
            }
            else if (eventType == WM_LBUTTONUP)
            {
                ShowWindow();
                _isMinimizedToTray = false;
            }
        }
        return WindowSubclasser.CallOriginalWndProc(hWnd, msg, wParam, lParam);
    }

    private void MinimizeToTray(AppWindow sender)
    {
        _isMinimizedToTray = true;
        sender.Hide();
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
            nativeWindow.Activate();
        }
    }

    private async void PanicAndExit()
    {
        try
        {
            _logger?.Add("Windows Tray: Panic action triggered from system tray.");
            
            if (MauiProgram.Services is not null)
            {
                var panicService = MauiProgram.Services.GetService<PanicService>();
                if (panicService != null)
                {
                    var success = await panicService.ExecutePanicAsync();
                    if (success)
                    {
                        _logger?.Add("Windows Tray: Panic wallpaper set successfully.");
                    }
                    else
                    {
                        _logger?.Add("Windows Tray: Failed to set panic wallpaper.");
                    }
                }
                else
                {
                    // Fallback to old method if service not available
                    var settingsService = MauiProgram.Services.GetService<SettingsService>();
                    if (settingsService != null)
                    {
                        var panicPath = settingsService.GetPanicFilePath();
                        if (string.IsNullOrEmpty(panicPath))
                            panicPath = settingsService.GetPanicUrl();

                        if (!string.IsNullOrEmpty(panicPath))
                        {
                            var wallpaperService = MauiProgram.Services.GetService<IWallpaperService>();
                            if (wallpaperService != null)
                            {
                                // Wait for wallpaper to be set before continuing
                                await wallpaperService.SetWallpaperAsync(panicPath);
                                _logger?.Add("Windows Tray: Fallback panic wallpaper set.");
                            }
                        }
                    }
                }
            }
            
            _logger?.Add("Windows Tray: Panic action completed. App will remain running.");
            // Don't exit the app automatically - let user decide
        }
        catch (Exception ex)
        {
            _logger?.Add($"Windows Tray: Panic action failed: {ex.Message}");
        }
    }

    private void ExitApplication()
    {
        try
        {
            // Clean up resources
            _icon?.Dispose();
            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            // Remove tray icon
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _windowHandle,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
        }
        finally
        {
            global::AnonWallClient.App.Current?.Quit();
        }
    }
}

internal static class WindowSubclasser
{
    private static IntPtr _originalWndProc;
    private static WndProcDelegate? _newWndProc;

    public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;

    public static void Subclass(IntPtr hWnd, WndProcDelegate newProc)
    {
        _newWndProc = newProc;
        _originalWndProc = SetWindowLongPtr(hWnd, GWLP_WNDPROC, newProc);
    }

    public static IntPtr CallOriginalWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }
}
