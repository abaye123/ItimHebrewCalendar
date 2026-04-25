using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using WinRT.Interop;

namespace ItimHebrewCalendar.Windows
{
    internal static class WindowHelpers
    {
        public static AppWindow? GetAppWindow(Window window)
        {
            var hWnd = WindowNative.GetWindowHandle(window);
            return AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hWnd));
        }

        public static void SetAppIcon(Window window)
        {
            try
            {
                var appWindow = GetAppWindow(window);
                if (appWindow == null) return;

                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
            }
            catch { }
        }

        // In unpackaged WinUI 3 a relative <Image Source="Assets/..."/> is unreliable;
        // load via an absolute file:// URI instead.
        public static void LoadAppIconInto(Image? image)
        {
            if (image == null) return;
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png");
                if (File.Exists(iconPath))
                {
                    image.Source = new BitmapImage(new Uri(iconPath));
                }
            }
            catch { }
        }

        public static void SetupCustomTitleBar(Window window, UIElement dragArea)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return;

            SetAppIcon(window);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var tb = appWindow.TitleBar;
                tb.ExtendsContentIntoTitleBar = true;
                tb.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                tb.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                window.SetTitleBar(dragArea);
            }
            else
            {
                window.ExtendsContentIntoTitleBar = true;
                window.SetTitleBar(dragArea);
            }
        }

        public static BackdropHandles TrySetBackdrop(Window window)
        {
            var handles = new BackdropHandles();

            if (MicaController.IsSupported())
            {
                handles.Config = new SystemBackdropConfiguration
                {
                    Theme = SystemBackdropTheme.Default,
                    IsInputActive = true
                };
                handles.Mica = new MicaController
                {
                    Kind = MicaKind.Base
                };
                handles.Mica.AddSystemBackdropTarget(
                    WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(window));
                handles.Mica.SetSystemBackdropConfiguration(handles.Config);
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                handles.Config = new SystemBackdropConfiguration
                {
                    Theme = SystemBackdropTheme.Default,
                    IsInputActive = true
                };
                handles.Acrylic = new DesktopAcrylicController();
                handles.Acrylic.AddSystemBackdropTarget(
                    WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(window));
                handles.Acrylic.SetSystemBackdropConfiguration(handles.Config);
            }

            return handles;
        }

        public static void Resize(Window window, int width, int height)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return;
            var hWnd = WindowNative.GetWindowHandle(window);
            var dpi = GetDpiForWindow(hWnd);
            var scale = dpi / 96.0;
            appWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
        }

        public static void CenterOnScreen(Window window)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return;
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (area == null) return;
            appWindow.Move(new PointInt32(
                area.WorkArea.X + (area.WorkArea.Width - appWindow.Size.Width) / 2,
                area.WorkArea.Y + (area.WorkArea.Height - appWindow.Size.Height) / 2));
        }

        public static void PositionNearCursor(Window window)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return;

            if (!GetCursorPos(out var pt))
            {
                CenterOnScreen(window);
                return;
            }

            var area = DisplayArea.GetFromPoint(new PointInt32(pt.X, pt.Y), DisplayAreaFallback.Primary);
            if (area == null)
            {
                CenterOnScreen(window);
                return;
            }

            int x = pt.X + 15;
            int y = pt.Y + 15;
            if (x + appWindow.Size.Width > area.WorkArea.X + area.WorkArea.Width)
                x = area.WorkArea.X + area.WorkArea.Width - appWindow.Size.Width - 10;
            if (y + appWindow.Size.Height > area.WorkArea.Y + area.WorkArea.Height)
                y = area.WorkArea.Y + area.WorkArea.Height - appWindow.Size.Height - 10;

            appWindow.Move(new PointInt32(x, y));
        }

        public static void PositionNearTray(Window window)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return;

            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (area == null) { CenterOnScreen(window); return; }

            const int margin = 8;
            int x = area.WorkArea.X + area.WorkArea.Width - appWindow.Size.Width - margin;
            int y = area.WorkArea.Y + area.WorkArea.Height - appWindow.Size.Height - margin;
            appWindow.Move(new PointInt32(x, y));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);
    }

    internal class BackdropHandles : IDisposable
    {
        public MicaController? Mica { get; set; }
        public DesktopAcrylicController? Acrylic { get; set; }
        public SystemBackdropConfiguration? Config { get; set; }

        public void Dispose()
        {
            Mica?.Dispose();
            Acrylic?.Dispose();
            Mica = null;
            Acrylic = null;
            Config = null;
        }
    }
}
