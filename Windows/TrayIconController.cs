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

        private DispatcherQueueTimer? _refreshTimer;
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
            menu.Items.Add(new PopupMenuItem("אירוע חדש", (_, _) => RunOnUI(ShowNewEvent)));
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

            // DispatcherQueueTimer ticks directly on the UI thread, so the
            // refresh path no longer goes through TryEnqueue and can't be
            // rejected by a transient dispatcher state (sleep/wake, lock screen).
            _refreshTimer = _uiDispatcher.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromMinutes(1);
            _refreshTimer.IsRepeating = true;
            _refreshTimer.Tick += (_, _) =>
            {
                try { UpdateIcon(); }
                catch (Exception ex) { SettingsManager.LogError("Tray refresh tick", ex); }
            };
            _refreshTimer.Start();
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

            if (TryEnqueueAction(action)) return;

            // TryEnqueue rejected the action - almost always a transient state
            // (sleep/wake, lock screen, brief window teardown). Retry once after
            // a short delay so the action isn't silently dropped, and log only
            // if the second attempt also fails (a genuinely stuck dispatcher).
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(250).ConfigureAwait(false);
                if (!TryEnqueueAction(action))
                {
                    SettingsManager.LogError(
                        "Tray dispatch",
                        new InvalidOperationException(
                            "UI dispatcher rejected the action twice in 250ms; dropping it"));
                }
            });
        }

        private bool TryEnqueueAction(Action action)
        {
            return _uiDispatcher.TryEnqueue(() =>
            {
                try { action(); }
                catch (Exception ex) { SettingsManager.LogError("Tray dispatched", ex); }
            });
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
                WindowHelpers.BringToForeground(_popup);
                return;
            }
            _popup = new CalendarPopup();
            _popup.Closed += (_, _) => _popup = null;
            _popup.Activate();
            WindowHelpers.BringToForeground(_popup);
        }

        public void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Activate();
                WindowHelpers.BringToForeground(_mainWindow);
                return;
            }
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (_, _) => _mainWindow = null;
            _mainWindow.Activate();
            WindowHelpers.BringToForeground(_mainWindow);
        }

        private void ShowSettings()
        {
            WindowManager.Show(typeof(SettingsWindow), () => new SettingsWindow(), onClosed: OnSettingsClosed);
        }

        private void ShowConverter()
        {
            WindowManager.Show(typeof(ConverterWindow), () => new ConverterWindow());
        }

        private void ShowZmanimWindow()
        {
            WindowManager.Show(typeof(ZmanimWindow), () => new ZmanimWindow());
        }

        // Tray right-click shortcut: open the shared "new event" draft, anchored to today.
        private void ShowNewEvent()
        {
            EventEditorWindow.OpenForNew(DateTime.Today);
        }

        private void ShowAbout()
        {
            // Shares the single settings window; if it's already open we just focus it.
            WindowManager.Show(typeof(SettingsWindow), () => new SettingsWindow(focusAbout: true), onClosed: OnSettingsClosed);
        }

        private void OnSettingsClosed()
        {
            _lastDateStr = ""; // force a redraw in case settings changed the icon text
            UpdateIcon();
            _popup?.Refresh();
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

                // Geresh/gershayim are correct typography for the tooltip but read as
                // visual noise at 32px; strip them so the glyph can be drawn larger.
                var text = settings.ShowHebrewDateInTray
                    ? today.DayStr.Replace("\"", "").Replace("'", "")
                        .Replace("׳", "").Replace("״", "")
                    : DateTime.Today.Day.ToString();

                // afterSunset and the style are part of the cache key so the icon
                // redraws when crossing sunset or when the user picks a new style.
                var cacheKey = $"{text}|{afterSunset}|{settings.TrayIconStyle}";
                if (cacheKey == _lastDateStr) return;
                _lastDateStr = cacheKey;

                if (_currentIcon != null)
                {
                    var oldHandle = _currentIcon.Handle;
                    _currentIcon.Dispose();
                    _currentIcon = null;
                    try { TrayIconRenderer.DestroyIcon(oldHandle); } catch { }
                }

                _currentIcon = TrayIconRenderer.Render(
                    text, ThemeHelper.IsSystemDarkMode(), afterSunset, settings.TrayIconStyle);

                _trayIcon.UpdateIcon(_currentIcon.Handle);

                var hebFull = HebrewDateFormatter.Full(today.Day, today.MonthName, today.Year, settings.DateFormat);
                var omer = OmerHelper.FormatOmer(today.Month, today.Day);
                var tooltip = $"{hebFull} ({DateTime.Today:dd/MM/yyyy})";
                if (!string.IsNullOrEmpty(omer)) tooltip += $"\n{omer}";
                if (afterSunset) tooltip += "\n(לאחר השקיעה)";
                if (settings.ShowSecondTempleTimer)
                {
                    var temple = SecondTempleTimer.Compute();
                    if (temple != null) tooltip += $"\n{SecondTempleTimer.FormatCompact(temple)}";
                }
                _trayIcon.UpdateToolTip(tooltip);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("TrayIconController.UpdateIcon", ex);
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
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
            WindowManager.CloseAll();

            GC.SuppressFinalize(this);
        }
    }
}
