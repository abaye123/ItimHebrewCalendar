using System;
using System.IO;
using Microsoft.Win32;

namespace ItimHebrewCalendar.Services
{
    public static class StartupHelper
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppValueName = "ItimHebrewCalendar";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(AppValueName) is string;
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null) return;

                if (enable)
                {
                    var exe = Path.Combine(AppContext.BaseDirectory, "ItimHebrewCalendar.exe");
                    key.SetValue(AppValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
                }
                else
                {
                    if (key.GetValue(AppValueName) != null)
                        key.DeleteValue(AppValueName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("StartupHelper.SetEnabled", ex);
            }
        }
    }
}
