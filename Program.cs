using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace ItimHebrewCalendar
{
    // Custom entry point. Required for unpackaged WinUI 3:
    //   1. Bootstrapper must be initialized before App is constructed.
    //   2. STA apartment is required for WinUI.
    //   3. We enforce single-instance via a named mutex.
    public static class Program
    {
        // Must match the Microsoft.WindowsAppSDK NuGet major.minor in the csproj.
        private static readonly uint RequiredMajorMinor = 0x00010007; // 1.7

        [STAThread]
        public static int Main(string[] args)
        {
            using var mutex = new Mutex(true,
                "ItimHebrewCalendar_SingleInstance_Mutex_{B8F3A1C2-4E7D-4A3F-9C8D-2E1F0A9B7C6D}",
                out bool isNewInstance);
            if (!isNewInstance)
            {
                return 0;
            }

            try
            {
                Bootstrap.Initialize(RequiredMajorMinor);
            }
            catch (Exception ex)
            {
                MessageBoxW(IntPtr.Zero,
                    "חסר Windows App Runtime במערכת.\n\n" +
                    "אנא התקן את Microsoft Windows App Runtime 1.7 מהכתובת:\n" +
                    "https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe\n\n" +
                    $"פרטי שגיאה:\n{ex.Message}",
                    "ItimHebrewCalendar - שגיאת התקנה",
                    0x10);
                return 1;
            }

            try
            {
                Application.Start((p) =>
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });

                return 0;
            }
            finally
            {
                Bootstrap.Shutdown();
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    }
}
