using System;
using System.Drawing;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public class TrayIconController : IDisposable
    {
        private TrayIconWithContextMenu? _trayIcon;
        private CalendarPopup? _popup;
        private MainWindow? _mainWindow;
        private SettingsWindow? _settingsWindow;
        private ConverterWindow? _converterWindow;
        private ZmanimWindow? _zmanimWindow;

        private System.Threading.Timer? _refreshTimer;
        private string _lastDateStr = "";
        private Icon? _currentIcon;

        private readonly DispatcherQueue _uiDispatcher;

        public TrayIconController()
        {
            _uiDispatcher = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("TrayIconController must be constructed on the UI thread");
        }

        public void Initialize()
        {
            var menu = new PopupMenu();
            menu.Items.Add(new PopupMenuItem("עיתים", (_, _) => { }) { Enabled = false });
            menu.Items.Add(new PopupMenuSeparator());
            menu.Items.Add(new PopupMenuItem("פתח חלון מלא", (_, _) => RunOnUI(ShowMainWindow)));
            menu.Items.Add(new PopupMenuItem("הצג לוח שנה", (_, _) => RunOnUI(ShowCalendarPopup)));
            menu.Items.Add(new PopupMenuSeparator());
            menu.Items.Add(new PopupMenuItem("ממיר תאריכים", (_, _) => RunOnUI(ShowConverter)));
            menu.Items.Add(new PopupMenuItem("זמני היום", (_, _) => RunOnUI(ShowZmanimWindow)));
            menu.Items.Add(new PopupMenuSeparator());
            menu.Items.Add(new PopupMenuItem("הגדרות", (_, _) => RunOnUI(ShowSettings)));
            menu.Items.Add(new PopupMenuItem("אודות", (_, _) => RunOnUI(ShowAbout)));
            menu.Items.Add(new PopupMenuSeparator());
            menu.Items.Add(new PopupMenuItem("יציאה", (_, _) => RunOnUI(ExitApp)));

            _trayIcon = new TrayIconWithContextMenu
            {
                ToolTip = "ItimHebrewCalendar",
                ContextMenu = menu,
            };

            _trayIcon.MessageWindow.MouseEventReceived += OnTrayMouseEvent;

            UpdateIcon();
            _trayIcon.Create();

            _refreshTimer = new System.Threading.Timer(_ =>
            {
                RunOnUI(UpdateIcon);
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        private void OnTrayMouseEvent(object? sender, H.NotifyIcon.Core.MessageWindow.MouseEventReceivedEventArgs e)
        {
            try
            {
                // Some H.NotifyIcon builds raise IconLeftDoubleClick instead of (or in addition
                // to) IconLeftMouseUp; accepting both keeps the click responsive everywhere.
                if (e.MouseEvent == MouseEvent.IconLeftMouseUp ||
                    e.MouseEvent == MouseEvent.IconLeftDoubleClick)
                {
                    RunOnUI(TogglePopup);
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("Tray mouse event", ex);
            }
        }

        private void ExitApp()
        {
            // Application.Exit() doesn't know about the tray icon - dispose first.
            try { Dispose(); }
            catch (Exception ex) { SettingsManager.LogError("Tray dispose on exit", ex); }

            try
            {
                Microsoft.UI.Xaml.Application.Current.Exit();
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("Tray exit", ex);
            }

            // H.NotifyIcon's MessageWindow can keep the process alive past Application.Exit;
            // force a hard exit so the user sees the app actually close.
            Environment.Exit(0);
        }

        private void RunOnUI(Action action)
        {
            if (_uiDispatcher.HasThreadAccess)
            {
                try { action(); }
                catch (Exception ex) { SettingsManager.LogError("Tray UI action", ex); }
                return;
            }

            var queued = _uiDispatcher.TryEnqueue(() =>
            {
                try { action(); }
                catch (Exception ex) { SettingsManager.LogError("Tray dispatched", ex); }
            });

            if (!queued)
            {
                SettingsManager.LogError("Tray dispatch",
                    new InvalidOperationException("TryEnqueue returned false - dispatcher rejected the action"));
            }
        }

        private void TogglePopup()
        {
            if (_popup != null)
            {
                _popup.Close();
                _popup = null;
                return;
            }
            ShowCalendarPopup();
        }

        public void ShowCalendarPopup()
        {
            if (_popup != null)
            {
                _popup.Activate();
                return;
            }
            _popup = new CalendarPopup();
            _popup.Closed += (_, _) => _popup = null;
            _popup.Activate();
        }

        public void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Activate();
                return;
            }
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (_, _) => _mainWindow = null;
            _mainWindow.Activate();
        }

        private void ShowSettings()
        {
            if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) =>
            {
                _settingsWindow = null;
                _lastDateStr = ""; // force a redraw in case settings changed the icon text
                UpdateIcon();
                _popup?.Refresh();
            };
            _settingsWindow.Activate();
        }

        private void ShowConverter()
        {
            if (_converterWindow != null) { _converterWindow.Activate(); return; }
            _converterWindow = new ConverterWindow();
            _converterWindow.Closed += (_, _) => _converterWindow = null;
            _converterWindow.Activate();
        }

        private void ShowZmanimWindow()
        {
            if (_zmanimWindow != null) { _zmanimWindow.Activate(); return; }
            _zmanimWindow = new ZmanimWindow();
            _zmanimWindow.Closed += (_, _) => _zmanimWindow = null;
            _zmanimWindow.Activate();
        }

        private void ShowAbout()
        {
            if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
            _settingsWindow = new SettingsWindow(focusAbout: true);
            _settingsWindow.Closed += (_, _) =>
            {
                _settingsWindow = null;
                _lastDateStr = "";
                UpdateIcon();
                _popup?.Refresh();
            };
            _settingsWindow.Activate();
        }

        public void UpdateIcon()
        {
            if (_trayIcon == null) return;

            try
            {
                var settings = App.Settings;
                var loc = settings.GetEffectiveLocation();
                var (today, afterSunset) = HebcalBridge.GetHalachicToday(loc, settings.UseSunsetDateTransition);
                if (today == null) return;

                var text = settings.ShowHebrewDateInTray
                    ? today.DayStr
                    : DateTime.Today.Day.ToString();

                // afterSunset is part of the cache key so the icon redraws when crossing sunset.
                var cacheKey = $"{text}|{afterSunset}";
                if (cacheKey == _lastDateStr) return;
                _lastDateStr = cacheKey;

                if (_currentIcon != null)
                {
                    var oldHandle = _currentIcon.Handle;
                    _currentIcon.Dispose();
                    _currentIcon = null;
                    try { TrayIconRenderer.DestroyIcon(oldHandle); } catch { }
                }

                _currentIcon = TrayIconRenderer.Render(text, ThemeHelper.IsSystemDarkMode(), afterSunset);

                _trayIcon.UpdateIcon(_currentIcon.Handle);

                var hebFull = $"{today.DayStr} ב{today.MonthName} {today.YearStr}";
                var omer = OmerHelper.FormatOmer(today.Month, today.Day);
                var tooltip = $"{hebFull} ({DateTime.Today:dd/MM/yyyy})";
                if (!string.IsNullOrEmpty(omer)) tooltip += $"\n{omer}";
                if (afterSunset) tooltip += "\n(לאחר השקיעה)";
                var temple = SecondTempleTimer.Compute();
                if (temple != null) tooltip += $"\n{SecondTempleTimer.FormatCompact(temple)}";
                _trayIcon.UpdateToolTip(tooltip);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("TrayIconController.UpdateIcon", ex);
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;

            _trayIcon?.Dispose();
            _trayIcon = null;

            if (_currentIcon != null)
            {
                var h = _currentIcon.Handle;
                _currentIcon.Dispose();
                _currentIcon = null;
                try { TrayIconRenderer.DestroyIcon(h); } catch { }
            }

            _popup?.Close();
            _mainWindow?.Close();
            _settingsWindow?.Close();
            _converterWindow?.Close();
            _zmanimWindow?.Close();

            GC.SuppressFinalize(this);
        }
    }
}
