using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;
using WinRT.Interop;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    internal static class ThemeHelper
    {
        public static ElementTheme ToElementTheme(AppTheme t) => t switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        public static SystemBackdropTheme ToBackdropTheme(AppTheme t) => t switch
        {
            AppTheme.Light => SystemBackdropTheme.Light,
            AppTheme.Dark => SystemBackdropTheme.Dark,
            _ => SystemBackdropTheme.Default
        };

        public static void Apply(Window window, AppTheme theme, SystemBackdropConfiguration? backdropConfig)
        {
            if (window.Content is FrameworkElement root)
            {
                root.RequestedTheme = ToElementTheme(theme);
            }

            if (backdropConfig != null)
            {
                backdropConfig.Theme = ToBackdropTheme(theme);
            }

            ApplyTitleBarButtonColors(window, theme);
        }

        private static void ApplyTitleBarButtonColors(Window window, AppTheme theme)
        {
            try
            {
                var hWnd = WindowNative.GetWindowHandle(window);
                var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hWnd));
                if (appWindow == null || !AppWindowTitleBar.IsCustomizationSupported()) return;

                bool isDark = IsEffectivelyDark(theme);
                var tb = appWindow.TitleBar;

                tb.ButtonBackgroundColor = Colors.Transparent;
                tb.ButtonInactiveBackgroundColor = Colors.Transparent;

                Color fg = isDark ? Colors.White : Colors.Black;
                Color inactiveFg = isDark ? Color.FromArgb(255, 155, 155, 155) : Color.FromArgb(255, 120, 120, 120);
                Color hoverBg = isDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0);
                Color pressedBg = isDark ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(32, 0, 0, 0);

                tb.ButtonForegroundColor = fg;
                tb.ButtonInactiveForegroundColor = inactiveFg;
                tb.ButtonHoverForegroundColor = fg;
                tb.ButtonHoverBackgroundColor = hoverBg;
                tb.ButtonPressedForegroundColor = fg;
                tb.ButtonPressedBackgroundColor = pressedBg;
            }
            catch { }
        }

        public static bool IsEffectivelyDark(AppTheme theme) => theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDarkMode()
        };

        public static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int lightTheme)
                    return lightTheme == 0;
            }
            catch { }
            return false;
        }

        // Adds WS_EX_LAYOUTRTL to the HWND so the system caption buttons mirror to the
        // left. WS_EX_NOINHERITLAYOUT keeps the XAML island itself unmirrored.
        public static void EnableRtlCaptionButtons(Window window)
        {
            try
            {
                var hWnd = WindowNative.GetWindowHandle(window);
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_LAYOUTRTL) == 0)
                {
                    SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYOUTRTL | WS_EX_NOINHERITLAYOUT);
                }
            }
            catch { }
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYOUTRTL = 0x00400000;
        private const int WS_EX_NOINHERITLAYOUT = 0x00100000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
