using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace ItimHebrewCalendar.Windows
{
    // App-wide registry of single-instance windows. Asking to open a window that is
    // already on screen re-focuses the existing one instead of stacking a duplicate.
    //
    // The key identifies the logical window: a Type for utility singletons
    // (settings, converter, all-events, ...), a sentinel for the shared "new event"
    // draft, or an event id for a specific event editor. UI-thread only — every
    // caller runs on the dispatcher — so no locking is needed.
    internal static class WindowManager
    {
        private static readonly Dictionary<object, Window> Open = new();

        public static T Show<T>(object key, Func<T> factory, Action? onClosed = null) where T : Window
        {
            if (Open.TryGetValue(key, out var existing))
            {
                existing.Activate();
                WindowHelpers.BringToForeground(existing);
                return (T)existing;
            }

            var win = factory();
            Open[key] = win;
            win.Closed += (_, _) =>
            {
                Open.Remove(key);
                onClosed?.Invoke();
            };
            win.Activate();
            WindowHelpers.BringToForeground(win);
            return win;
        }

        // Closes every tracked window — used on app exit so utility windows don't
        // linger after the tray shuts down.
        public static void CloseAll()
        {
            foreach (var win in new List<Window>(Open.Values))
            {
                try { win.Close(); } catch { /* already torn down */ }
            }
            Open.Clear();
        }
    }
}
