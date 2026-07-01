using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ItimHebrewCalendar.Models;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public sealed partial class CalendarPopup : Window
    {
        private int _hebYear;
        private int _hebMonth;
        private BackdropHandles? _backdrop;

        private const int BaseHeight = 640;
        private const int PopupWidth = 380;
        private int _currentHeight = -1;
        private int _monthlyHeight;
        private bool _monthBuilt;
        private bool _loaded;
        private DateTime _halachicTodayDate = DateTime.Today;
        private Brush? _defaultTodayCardBrush;
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush SunsetCardBrush =
            new(global::Windows.UI.Color.FromArgb(255, 0xE0, 0x55, 0x10));

        private readonly HashSet<DateTime> _datesWithUserEvents = new();
        private DateTime _dailyDate = DateTime.Today;
        private bool _viewModeReady;

        public CalendarPopup()
        {
            InitializeComponent();

            WindowHelpers.LoadAppIconInto(TitleBarIcon);
            _defaultTodayCardBrush = TodayCard.Background;
            RootGrid.Loaded += OnRootLoaded;

            SetCurrentHebMonthToToday();
            Title = "עיתים - לוח שנה עברי";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);
            ThemeHelper.Apply(this, App.Settings.Theme, _backdrop.Config);

            ApplyHeight(BaseHeight);
            WindowHelpers.PositionNearTray(this);

            if (App.Settings.DefaultTrayView == CalendarViewMode.Daily)
            {
                DailyViewToggle.IsChecked = true;
                MonthlyViewToggle.IsChecked = false;
            }
            else
            {
                MonthlyViewToggle.IsChecked = true;
                DailyViewToggle.IsChecked = false;
            }
            UpdateSegmentedHighlight();
            _viewModeReady = true;
            ApplyViewMode();

            var appWin = WindowHelpers.GetAppWindow(this);
            if (appWin != null)
            {
                appWin.IsShownInSwitchers = false;

                if (appWin.Presenter is OverlappedPresenter op)
                {
                    op.IsMaximizable = false;
                    op.IsMinimizable = false;
                    op.IsResizable = false;
                }
            }

            Refresh();

            Activated += OnActivated;

            Closed += (_, _) =>
            {
                Activated -= OnActivated;
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        private bool _isClosing;

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (!App.Settings.CloseTrayPopupOnFocusLoss) return;
            if (args.WindowActivationState != WindowActivationState.Deactivated) return;
            if (_isClosing) return;

            _isClosing = true;
            try { Close(); }
            catch (Exception ex) { SettingsManager.LogError("CalendarPopup auto-close", ex); }
        }

        public void Refresh()
        {
            try
            {
                var settings = App.Settings;
                var loc = settings.GetEffectiveLocation();

                var (today, afterSunset) = HebcalBridge.GetHalachicToday(loc, settings.UseSunsetDateTransition);
                _halachicTodayDate = DateTime.Today.AddDays(afterSunset ? 1 : 0);
                if (today != null)
                {
                    TxtTodayHebrew.Text = HebrewDateFormatter.Full(today.Day, today.MonthName, today.Year, App.Settings.DateFormat);
                    TxtTodayGregorian.Text = HebrewDateFormatter.Gregorian(DateTime.Now, App.Settings.DateFormat);
                    AfterSunsetPanel.Visibility = afterSunset ? Visibility.Visible : Visibility.Collapsed;

                    TodayCard.Background = afterSunset ? SunsetCardBrush : _defaultTodayCardBrush;

                    var omer = OmerHelper.FormatOmer(today.Month, today.Day);
                    bool showOmer = !string.IsNullOrEmpty(omer);
                    if (showOmer)
                    {
                        TxtTodayOmer.Text = omer;
                        TxtTodayOmer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TxtTodayOmer.Visibility = Visibility.Collapsed;
                    }

                    var temple = settings.ShowSecondTempleTimer ? SecondTempleTimer.Compute() : null;
                    bool showTemple = temple != null;
                    if (showTemple)
                    {
                        TxtTempleTimer.Text = SecondTempleTimer.FormatCompact(temple!);
                        TxtTempleTimer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TxtTempleTimer.Visibility = Visibility.Collapsed;
                    }

                }

                var month = HebcalBridge.GetHebrewMonth(_hebYear, _hebMonth,
                    settings.UseIsraeliHolidays, settings.ShowModernHolidays);
                if (month == null || month.Days.Count == 0) return;

                BuildUserEventDateIndex(month);
                DrawMonthHeader(month);
                DrawDaysGrid(month, settings.ShowGregorianInCalendar);

                var shabbat = HebcalBridge.GetShabbat(loc);
                if (shabbat != null)
                {
                    TxtParasha.Text = shabbat.Parasha;
                    TxtCandleLighting.Text = string.IsNullOrEmpty(shabbat.CandleLighting) ? "" : $"הדלקת נרות: {shabbat.CandleLighting}";
                    TxtHavdalah.Text = string.IsNullOrEmpty(shabbat.Havdalah) ? "" : $"הבדלה: {shabbat.Havdalah}";
                }

                _monthBuilt = true;
                AdjustHeightForCurrentView();
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("CalendarPopup.Refresh", ex);
            }
        }

        private void DrawMonthHeader(MonthlyCalendar month)
        {
            if (month.Days.Count == 0) return;

            var first = month.Days[0];
            var last = month.Days[^1];

            TxtMonthHeb.Text = HebrewDateFormatter.MonthYear(first.HebMonthName, first.HebYear, App.Settings.DateFormat);

            var ci = CultureInfo.GetCultureInfo("he-IL");
            string gregSpan;
            if (first.GregYear == last.GregYear && first.GregMonth == last.GregMonth)
            {
                gregSpan = $"{ci.DateTimeFormat.GetMonthName(first.GregMonth)} {first.GregYear}";
            }
            else if (first.GregYear == last.GregYear)
            {
                gregSpan = $"{ci.DateTimeFormat.GetMonthName(first.GregMonth)}–" +
                           $"{ci.DateTimeFormat.GetMonthName(last.GregMonth)} {last.GregYear}";
            }
            else
            {
                gregSpan = $"{ci.DateTimeFormat.GetMonthName(first.GregMonth)} {first.GregYear} – " +
                           $"{ci.DateTimeFormat.GetMonthName(last.GregMonth)} {last.GregYear}";
            }
            TxtMonthGreg.Text = gregSpan;
        }

        private void DrawDaysGrid(MonthlyCalendar month, bool showGreg)
        {
            // Children.Clear leaves the column/row definitions intact.
            DaysGrid.Children.Clear();

            if (month.Days.Count == 0) return;

            var firstDay = month.Days[0];
            int startCol = firstDay.DayOfWeek; // 0 = Sunday
            int currentRow = 0;
            int currentCol = startCol;

            foreach (var day in month.Days)
            {
                var cell = BuildDayCell(day, showGreg);
                Grid.SetRow(cell, currentRow);
                Grid.SetColumn(cell, currentCol);
                DaysGrid.Children.Add(cell);

                currentCol++;
                if (currentCol > 6)
                {
                    currentCol = 0;
                    currentRow++;
                }
            }
        }

        private FrameworkElement BuildDayCell(CalendarDay day, bool showGreg)
        {
            var isToday = day.Date.Date == _halachicTodayDate;
            var isShabbat = day.IsShabbat;
            var hasHoliday = day.Events.Any(e => e.IsHoliday || e.IsMajor);
            var isRoshChodesh = day.Events.Any(e => e.IsRoshChodesh);
            bool isDark = ThemeHelper.IsEffectivelyDark(App.Settings.Theme);

            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(1),
                Padding = new Thickness(2),
                Tag = day,
                Background = GetCellBackground(isDark, isToday, hasHoliday, isShabbat)
            };

            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 0
            };

            var tbHeb = new TextBlock
            {
                Text = HebrewDateFormatter.Day(day.HebDay, App.Settings.DateFormat),
                FontSize = 14,
                FontWeight = isToday || isShabbat ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = isToday ? CellTheme.TextOnAccent() : CellTheme.TextPrimary(isDark)
            };
            sp.Children.Add(tbHeb);

            if (showGreg)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = day.GregDay.ToString(),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = isToday ? CellTheme.TextOnAccent() : CellTheme.TextSecondary(isDark),
                    Opacity = 0.85
                });
            }

            bool hasUserEvent = _datesWithUserEvents.Contains(day.Date.Date);
            if (hasHoliday || isRoshChodesh || hasUserEvent)
            {
                var dotsRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                if (hasHoliday || isRoshChodesh)
                {
                    dotsRow.Children.Add(new Ellipse
                    {
                        Width = 4, Height = 4,
                        Fill = isToday ? CellTheme.TextOnAccent() : CellTheme.AccentBackground(isDark)
                    });
                }
                if (hasUserEvent)
                {
                    var userDot = new Ellipse
                    {
                        Width = 4, Height = 4,
                        Fill = isToday ? CellTheme.TextOnAccent() : CellTheme.AccentText(isDark)
                    };
                    ToolTipService.SetToolTip(userDot, "אירוע אישי");
                    dotsRow.Children.Add(userDot);
                }
                sp.Children.Add(dotsRow);
            }

            border.Child = sp;

            var tt = BuildDayTooltip(day);
            if (!string.IsNullOrEmpty(tt))
            {
                ToolTipService.SetToolTip(border, tt);
            }

            border.PointerReleased += (_, _) => ShowDayDetails(day);
            border.PointerEntered += (s, _) =>
            {
                if (s is Border b && !isToday)
                    b.Background = CellTheme.HoverBackground(isDark);
            };
            border.PointerExited += (s, _) =>
            {
                if (s is Border b && !isToday)
                    b.Background = GetCellBackground(isDark, isToday, hasHoliday, isShabbat);
            };

            return border;
        }

        private static Brush GetCellBackground(bool isDark, bool isToday, bool hasHoliday, bool isShabbat)
        {
            if (isToday) return CellTheme.AccentBackground(isDark);
            if (hasHoliday) return CellTheme.HolidayBackground(isDark);
            if (isShabbat) return CellTheme.ShabbatBackground(isDark);
            return CellTheme.NormalBackground();
        }

        private string BuildDayTooltip(CalendarDay day)
        {
            var parts = new System.Collections.Generic.List<string>
            {
                HebrewDateFormatter.Full(day.HebDay, day.HebMonthName, day.HebYear, App.Settings.DateFormat),
                HebrewDateFormatter.Gregorian(day.Date, App.Settings.DateFormat)
            };
            foreach (var e in day.Events)
            {
                if (!string.IsNullOrEmpty(e.Description))
                    parts.Add($"• {e.Description}");
            }
            foreach (var occ in DayDetailsRenderer.GetUserEventsForDate(day.Date))
            {
                parts.Add($"• {occ.Event.Title} (אישי)");
            }
            return string.Join("\n", parts);
        }

        private void ShowDayDetails(CalendarDay day)
        {
            DayDetailsRenderer.Render(day, new DayDetailsRenderer.Targets
            {
                HebrewLabel = DetailsHebrew,
                GregorianLabel = DetailsGregorian,
                DayOfWeekLabel = DetailsDayOfWeek,
                EventsSection = DetailsEventsSection,
                EventsPanel = DetailsEventsPanel,
                ZmanimSection = DetailsZmanimSection,
                ZmanimPanel = DetailsZmanimPanel,
                OnEditUserEvent = ev =>
                {
                    EventEditorWindow.OpenForEdit(ev, day.Date, () => ShowDayDetails(day));
                }
            });

            CalendarView.Visibility = Visibility.Collapsed;
            DetailsView.Visibility = Visibility.Visible;
            BottomBar.Visibility = Visibility.Collapsed;
        }

        private void BuildUserEventDateIndex(MonthlyCalendar? month)
        {
            _datesWithUserEvents.Clear();
            try
            {
                if (month == null || month.Days.Count == 0) return;
                var from = month.Days[0].Date.Date;
                var to   = month.Days[^1].Date.Date;
                foreach (var ev in EventsRepository.All)
                    foreach (var occ in EventOccurrenceExpander.Expand(ev, from, to))
                        _datesWithUserEvents.Add(occ.Date);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("CalendarPopup.BuildUserEventDateIndex", ex);
            }
        }

        private void OnBackToCalendar(object sender, RoutedEventArgs e)
        {
            DetailsView.Visibility = Visibility.Collapsed;
            CalendarView.Visibility = Visibility.Visible;
            BottomBar.Visibility = Visibility.Visible;
        }

        private void OnPrevMonth(object sender, RoutedEventArgs e)
        {
            StepHebMonth(-1);
            Refresh();
        }

        private void OnNextMonth(object sender, RoutedEventArgs e)
        {
            StepHebMonth(+1);
            Refresh();
        }

        private void OnTodayClick(object sender, RoutedEventArgs e)
        {
            SetCurrentHebMonthToToday();
            Refresh();
        }

        private void OnMonthYearFlyoutOpening(object sender, object e)
        {
            MonthYearPicker.SetCurrent(_hebYear, _hebMonth);
        }

        private void OnMonthYearConfirmed(object sender, (int Year, int Month, int? Day) value)
        {
            _hebYear = value.Year;
            _hebMonth = value.Month;
            MonthYearButton.Flyout?.Hide();
            Refresh();

            // If the user typed a specific day, open Day Details for it so the
            // "go to date" flow lands on the exact day rather than just the month.
            if (value.Day.HasValue)
            {
                var greg = HebcalBridge.ConvertFromHebrew(value.Year, value.Month, value.Day.Value);
                if (greg != null && greg.Year > 0)
                {
                    var date = new DateTime(greg.Year, greg.Month, greg.Day);
                    var settings = App.Settings;
                    var monthData = HebcalBridge.GetMonth(date.Year, date.Month,
                        settings.UseIsraeliHolidays, settings.ShowModernHolidays);
                    var dayData = monthData?.Days.FirstOrDefault(d =>
                        d.GregYear == date.Year && d.GregMonth == date.Month && d.GregDay == date.Day);
                    if (dayData != null) ShowDayDetails(dayData);
                }
            }
        }

        private void OnMonthYearTodayRequested(object sender, EventArgs e)
        {
            SetCurrentHebMonthToToday();
            MonthYearButton.Flyout?.Hide();
            Refresh();
        }

        private void ApplyHeight(int height)
        {
            if (height == _currentHeight) return;
            _currentHeight = height;
            WindowHelpers.Resize(this, PopupWidth, height);
            WindowHelpers.PositionNearTray(this);
        }

        // The monthly view has no internal scroll, so the window must be tall enough
        // to show all of its content - otherwise the bottom toolbar is clipped. This
        // happens for users with Windows text-scaling > 100% or longer date strings.
        // Measuring the real content height (instead of a fixed constant) keeps the
        // toolbar visible regardless of scaling, font, or wrapping. The daily/details
        // views scroll internally, so they keep a fixed height.
        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            _loaded = true;
            // Defer one tick so the first real layout pass (with final fonts/scaling)
            // has completed before we measure.
            DispatcherQueue.TryEnqueue(AdjustHeightForCurrentView);
        }

        private void AdjustHeightForCurrentView()
        {
            // Before the content is loaded a measurement would be inaccurate; keep the
            // base height until OnRootLoaded re-runs this with a laid-out tree.
            if (!_loaded)
            {
                ApplyHeight(BaseHeight);
                return;
            }

            if (CalendarView.Visibility == Visibility.Visible)
            {
                _monthlyHeight = MeasureMonthlyHeight();
                ApplyHeight(_monthlyHeight);
            }
            else
            {
                // Daily/details views keep the monthly view's height so toggling
                // between views never resizes the window.
                ApplyHeight(_monthlyHeight > 0 ? _monthlyHeight : BaseHeight);
            }
        }

        private int MeasureMonthlyHeight()
        {
            try
            {
                RootGrid.Measure(new global::Windows.Foundation.Size(PopupWidth, double.PositiveInfinity));
                int desired = (int)Math.Ceiling(RootGrid.DesiredSize.Height) + 2;

                // Never grow past the screen work area.
                double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
                var appWin = WindowHelpers.GetAppWindow(this);
                if (appWin != null && scale > 0)
                {
                    var area = DisplayArea.GetFromWindowId(appWin.Id, DisplayAreaFallback.Primary);
                    if (area != null)
                    {
                        int maxDip = (int)(area.WorkArea.Height / scale) - 24;
                        if (maxDip > 0 && desired > maxDip) desired = maxDip;
                    }
                }
                return desired > 0 ? desired : BaseHeight;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("CalendarPopup.MeasureMonthlyHeight", ex);
                return BaseHeight;
            }
        }

        private void SetCurrentHebMonthToToday()
        {
            var tz = App.Settings.GetEffectiveLocation().TimeZone;
            var today = HebcalBridge.GetToday(tz);
            if (today != null)
            {
                _hebYear = today.Year;
                _hebMonth = today.Month;
                return;
            }
            var hd = HebcalBridge.Convert(DateTime.Today);
            _hebYear = hd?.HebYear ?? 5786;
            _hebMonth = hd?.HebMonth ?? 1;
        }

        // Step one Hebrew month by jumping to a gregorian date safely outside the month
        // and converting back. This handles leap years (Adar I/II) and year rollover.
        private void StepHebMonth(int direction)
        {
            var g1 = HebcalBridge.ConvertFromHebrew(_hebYear, _hebMonth, 1);
            if (g1 == null) return;

            var anchor = new DateTime(g1.Year, g1.Month, g1.Day);
            var probe = direction > 0 ? anchor.AddDays(32) : anchor.AddDays(-1);
            var hd = HebcalBridge.Convert(probe);
            if (hd == null) return;

            _hebYear = hd.HebYear;
            _hebMonth = hd.HebMonth;
        }

        private void OnOpenMainWindow(object sender, RoutedEventArgs e)
        {
            App.Tray?.ShowMainWindow();
            Close();
        }

        private void OnZmanimClick(object sender, RoutedEventArgs e)
        {
            WindowManager.Show(typeof(ZmanimWindow), () => new ZmanimWindow());
        }

        private void OnConverterClick(object sender, RoutedEventArgs e)
        {
            WindowManager.Show(typeof(ConverterWindow), () => new ConverterWindow());
        }

        private void OnAllEventsClick(object sender, RoutedEventArgs e)
        {
            WindowManager.Show(typeof(EventsListWindow), () => new EventsListWindow());
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            WindowManager.Show(typeof(SettingsWindow), () => new SettingsWindow(), onClosed: () =>
            {
                ThemeHelper.Apply(this, App.Settings.Theme, _backdrop?.Config);
                Refresh();
                App.Tray?.UpdateIcon();
            });
        }

        // ─── Daily view ────────────────────────────────────────────────────────────

        private void OnSegToggleClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clicked) return;
            if (clicked == DailyViewToggle)
            {
                DailyViewToggle.IsChecked = true;
                MonthlyViewToggle.IsChecked = false;
            }
            else
            {
                MonthlyViewToggle.IsChecked = true;
                DailyViewToggle.IsChecked = false;
            }
            UpdateSegmentedHighlight();
            if (_viewModeReady) ApplyViewMode();
        }

        private void UpdateSegmentedHighlight()
        {
            var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            var onAccent = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
            var fg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

            bool dailyOn = DailyViewToggle.IsChecked == true;
            DailyViewToggle.Background   = dailyOn ? accent : new SolidColorBrush(global::Windows.UI.Color.FromArgb(0,0,0,0));
            DailyViewToggle.Foreground   = dailyOn ? onAccent : fg;
            MonthlyViewToggle.Background = !dailyOn ? accent : new SolidColorBrush(global::Windows.UI.Color.FromArgb(0,0,0,0));
            MonthlyViewToggle.Foreground = !dailyOn ? onAccent : fg;
        }

        private void ApplyViewMode()
        {
            bool daily = DailyViewToggle.IsChecked == true;
            CalendarView.Visibility = daily ? Visibility.Collapsed : Visibility.Visible;
            DetailsView.Visibility  = Visibility.Collapsed;
            DailyView.Visibility    = daily ? Visibility.Visible : Visibility.Collapsed;
            BottomBar.Visibility    = Visibility.Visible;
            if (daily)
            {
                _dailyDate = _halachicTodayDate;
                AdjustHeightForCurrentView();
                RefreshDaily();
            }
            else if (_monthBuilt)
            {
                AdjustHeightForCurrentView();
            }
        }

        private void OnAddEventClick(object sender, RoutedEventArgs e)
        {
            var defaultDate = (DailyViewToggle.IsChecked == true) ? _dailyDate : _halachicTodayDate;
            EventEditorWindow.OpenForNew(defaultDate, () =>
            {
                if (DailyViewToggle.IsChecked == true) RefreshDaily();
                else Refresh();
            });
        }

        private void OnDailyPrev(object sender, RoutedEventArgs e)
        {
            _dailyDate = _dailyDate.AddDays(-1);
            RefreshDaily();
        }

        private void OnDailyNext(object sender, RoutedEventArgs e)
        {
            _dailyDate = _dailyDate.AddDays(+1);
            RefreshDaily();
        }

        private void OnDailyToday(object sender, RoutedEventArgs e)
        {
            _dailyDate = _halachicTodayDate;
            RefreshDaily();
        }

        private void RefreshDaily()
        {
            try
            {
                var ci = CultureInfo.GetCultureInfo("he-IL");
                var hd = HebcalBridge.Convert(_dailyDate);
                if (hd == null) return;

                var headerHeb  = HebrewDateFormatter.Full(hd.HebDay, hd.MonthName, hd.HebYear, App.Settings.DateFormat);
                var headerGreg = HebrewDateFormatter.GregorianNoWeekday(_dailyDate, App.Settings.DateFormat);
                DailyHeaderHeb.Text  = headerHeb;
                DailyHeaderGreg.Text = $"{_dailyDate.ToString("dddd", ci)} · {headerGreg}";

                var settings = App.Settings;
                var monthData = HebcalBridge.GetMonth(_dailyDate.Year, _dailyDate.Month,
                    settings.UseIsraeliHolidays, settings.ShowModernHolidays);
                CalendarDay? dayData = null;
                if (monthData != null)
                {
                    dayData = monthData.Days.FirstOrDefault(d =>
                        d.GregYear == _dailyDate.Year && d.GregMonth == _dailyDate.Month && d.GregDay == _dailyDate.Day);
                }
                dayData ??= new CalendarDay
                {
                    GregYear = _dailyDate.Year, GregMonth = _dailyDate.Month, GregDay = _dailyDate.Day,
                    HebYear = hd.HebYear, HebMonth = hd.HebMonth, HebDay = hd.HebDay,
                    HebDayStr = HebrewNumberFormatter.FormatDay(hd.HebDay),
                    HebMonthName = hd.MonthName,
                    DayOfWeek = (int)_dailyDate.DayOfWeek,
                    Events = new System.Collections.Generic.List<CalendarEvent>()
                };

                DayDetailsRenderer.Render(dayData, new DayDetailsRenderer.Targets
                {
                    EventsSection = DailyEventsSection,
                    EventsPanel = DailyEventsPanel,
                    ZmanimSection = DailyZmanimSection,
                    ZmanimPanel = DailyZmanimPanel,
                    OnEditUserEvent = ev =>
                    {
                        EventEditorWindow.OpenForEdit(ev, _dailyDate, RefreshDaily);
                    }
                });
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("CalendarPopup.RefreshDaily", ex);
            }
        }
    }
}
