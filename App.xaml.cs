using System;
using System.IO;
using Microsoft.UI.Xaml;
using ItimHebrewCalendar.Services;
using ItimHebrewCalendar.Windows;

namespace ItimHebrewCalendar
{
    public partial class App : Application
    {
        public static TrayIconController? Tray { get; private set; }

        public static AppSettings Settings { get; internal set; } = new();

        // Invisible anchor window. Required so the UI thread's DispatcherQueue keeps
        // pumping after every visible window is closed - otherwise tray clicks queue
        // up but never run (TryEnqueue succeeds, the action never executes).
        private Window? _anchorWindow;

        public App()
        {
            InitializeComponent();
            UnhandledException += (_, e) =>
            {
                TryLogError(e.Exception);
                // Marking as handled prevents the WinUI pump from tearing down
                // after a window-side exception, which would kill the tray.
                e.Handled = true;
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Settings = SettingsManager.Load();

            try { StartupHelper.SetEnabled(Settings.StartWithWindows); }
            catch (Exception ex) { TryLogError(ex); }

            if (!HebcalBridge.EnsureNativeLoaded(out var err))
            {
                Native.MessageBoxW(IntPtr.Zero, err, "ItimHebrewCalendar - שגיאת טעינה", 0x10);
                Exit();
                return;
            }

            CreateAnchorWindow();

            Tray = new TrayIconController();
            Tray.Initialize();

            var cmdArgs = Environment.GetCommandLineArgs();
            bool silentStart = cmdArgs.Length > 1 && cmdArgs[1].Equals("--tray", StringComparison.OrdinalIgnoreCase);
            if (!silentStart)
            {
                try { Tray.ShowMainWindow(); }
                catch (Exception ex) { TryLogError(ex); }
            }
        }

        private void CreateAnchorWindow()
        {
            try
            {
                _anchorWindow = new Window { Title = "ItimHebrewCalendarAnchor" };

                var appWindow = WindowHelpers.GetAppWindow(_anchorWindow);
                if (appWindow != null)
                {
                    appWindow.IsShownInSwitchers = false;
                    appWindow.Move(new global::Windows.Graphics.PointInt32(-32000, -32000));
                    appWindow.Resize(new global::Windows.Graphics.SizeInt32(1, 1));
                }

                // Activate registers the window with WinUI and keeps the pump alive;
                // we hide it immediately afterwards.
                _anchorWindow.Activate();
                appWindow?.Hide();
            }
            catch (Exception ex)
            {
                TryLogError(ex);
            }
        }

        private static void TryLogError(Exception ex)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ItimHebrewCalendar");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "errors.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }
        }
    }

    internal static class Native
    {
        [System.Runtime.InteropServices.DllImport("user32.dll",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    }
}
