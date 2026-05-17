using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ItimHebrewCalendar.Models;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{

    public sealed partial class MainWindow : Window
    {
        private int _hebYear;
        private int _hebMonth;
        private CalendarDay? _selectedDay;
        private BackdropHandles? _backdrop;

        private const int BaseHeight = 730;
        private const int OmerExtraHeight = 32;
        private const int AfterSunsetExtraHeight = 32;
        private const int TempleExtraHeight = 32;
        private int _currentHeight = -1;
        private DateTime _halachicTodayDate = DateTime.Today;
        private Brush? _defaultTodayCardBrush;
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush SunsetCardBrush =
            new(global::Windows.UI.Color.FromArgb(255, 0xE0, 0x55, 0x10));

        private readonly HashSet<DateTime> _datesWithUserEvents = new();
        private DateTime _dailyDate = DateTime.Today;
        private bool _viewModeReady;

        public MainWindow()
        {
            InitializeComponent();

            WindowHelpers.LoadAppIconInto(TitleBarIcon);
            _defaultTodayCardBrush = TodayCard.Background;

            SetCurrentHebMonthToToday();
            Title = "עיתים - לוח שנה עברי";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);
            ThemeHelper.Apply(this, App.Settings.Theme, _backdrop.Config);

            ApplyHeight(BaseHeight);
            WindowHelpers.CenterOnScreen(this);

            // Honor default view from settings without triggering the change handler twice.
            if (App.Settings.DefaultMainView == CalendarViewMode.Daily)
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

            Refresh();

            EventsRepository.Changed += OnEventsChanged;

            Closed += (_, _) =>
            {
                EventsRepository.Changed -= OnEventsChanged;
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        private void OnEventsChanged()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    Refresh();
                    if (DailyViewToggle?.IsChecked == true) RefreshDaily();
                });
            }
            catch { }
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
                    TxtTodayHebrew.Text = $"{today.DayStr} ב{today.MonthName} {today.YearStr}";
                    TxtTodayGregorian.Text = DateTime.Now.ToString("dddd, d בMMMM yyyy",
                        CultureInfo.GetCultureInfo("he-IL"));
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
                        TxtTempleTimer.Text = SecondTempleTimer.FormatWithTime(temple!);
                        TxtTempleTimer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TxtTempleTimer.Visibility = Visibility.Collapsed;
                    }

                    int targetHeight = BaseHeight;
                    if (showOmer) targetHeight += OmerExtraHeight;
                    if (afterSunset) targetHeight += AfterSunsetExtraHeight;
                    if (showTemple) targetHeight += TempleExtraHeight;
                    ApplyHeight(targetHeight);
                }

                var month = HebcalBridge.GetHebrewMonth(_hebYear, _hebMonth,
                    settings.UseIsraeliHolidays, settings.ShowModernHolidays);
                if (month == null || month.Days.Count == 0) return;

                BuildUserEventDateIndex(month);
                DrawMonthHeader(month);
                DrawDaysGrid(month, settings.ShowGregorianInCalendar);

                var preferredDay = month.Days.FirstOrDefault(d => d.Date.Date == _halachicTodayDate) ?? month.Days[0];
                SelectDay(preferredDay);

                var shabbat = HebcalBridge.GetShabbat(loc);
                if (shabbat != null)
                {
                    TxtParasha.Text = shabbat.Parasha;
                    TxtCandleLighting.Text = string.IsNullOrEmpty(shabbat.CandleLighting) ? "" : $"הדלקת נרות: {shabbat.CandleLighting}";
                    TxtHavdalah.Text = string.IsNullOrEmpty(shabbat.Havdalah) ? "" : $"הבדלה: {shabbat.Havdalah}";
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("MainWindow.Refresh", ex);
            }
        }

        private void DrawMonthHeader(MonthlyCalendar month)
        {
            if (month.Days.Count == 0) return;

            var first = month.Days[0];
            var last = month.Days[^1];

            TxtMonthHeb.Text = $"{first.HebMonthName} {HebrewNumberFormatter.FormatYear(first.HebYear)}";

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
            DaysGrid.Children.Clear();
            if (month.Days.Count == 0) return;

            int startCol = month.Days[0].DayOfWeek;
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
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(2),
                Padding = new Thickness(8),
                Tag = day,
                BorderThickness = new Thickness(1),
                BorderBrush = CellTheme.Border(isDark),
                Background = GetCellBackground(isDark, isToday, hasHoliday, isShabbat)
            };

            var sp = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                Spacing = 2
            };

            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition());
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tbHeb = new TextBlock
            {
                Text = day.HebDayStr,
                FontSize = 18,
                FontWeight = isToday || isShabbat ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = isToday ? CellTheme.TextOnAccent() : CellTheme.TextPrimary(isDark)
            };
            Grid.SetColumn(tbHeb, 0);
            top.Children.Add(tbHeb);

            if (showGreg)
            {
                var tbGreg = new TextBlock
                {
                    Text = day.GregDay.ToString(),
                    FontSize = 11,
                    Foreground = isToday ? CellTheme.TextOnAccent() : CellTheme.TextSecondary(isDark),
                    Opacity = 0.85,
                    VerticalAlignment = VerticalAlignment.Center,
                    FlowDirection = FlowDirection.LeftToRight
                };
                Grid.SetColumn(tbGreg, 1);
                top.Children.Add(tbGreg);
            }
            sp.Children.Add(top);

            var firstEvent = day.Events.FirstOrDefault(e => !string.IsNullOrEmpty(e.Description));
            if (firstEvent != null)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = firstEvent.Description,
                    FontSize = 10,
                    TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    Foreground = isToday ? CellTheme.TextOnAccent() : CellTheme.AccentText(isDark)
                });
            }
            else if (isRoshChodesh)
            {
                sp.Children.Add(new Ellipse
                {
                    Width = 5, Height = 5,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Fill = isToday ? CellTheme.TextOnAccent() : CellTheme.AccentBackground(isDark)
                });
            }

            if (_datesWithUserEvents.Contains(day.Date.Date))
            {
                var dot = new Ellipse
                {
                    Width = 6, Height = 6,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 2, 0),
                    Fill = isToday ? CellTheme.TextOnAccent() : CellTheme.AccentBackground(isDark)
                };
                ToolTipService.SetToolTip(dot, "אירוע אישי ביום זה");
                sp.Children.Add(dot);
            }

            border.Child = sp;
            border.PointerReleased += (_, _) => SelectDay(day);
            border.PointerEntered += (s, _) =>
            {
                if (s is Border b && !isToday && (_selectedDay != day))
                    b.Background = CellTheme.HoverBackground(isDark);
            };
            border.PointerExited += (s, _) =>
            {
                if (s is Border b && !isToday && (_selectedDay != day))
                    b.Background = GetCellBackground(isDark, isToday: false, hasHoliday, isShabbat);
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

        private void SelectDay(CalendarDay day)
        {
            _selectedDay = day;
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
                    var editor = new EventEditorWindow(ev, day.Date, () =>
                    {
                        BuildUserEventDateIndex(null);
                        SelectDay(day);
                    });
                    editor.Activate();
                }
            });
        }

        private void BuildUserEventDateIndex(MonthlyCalendar? month)
        {
            _datesWithUserEvents.Clear();
            try
            {
                DateTime from, to;
                if (month != null && month.Days.Count > 0)
                {
                    from = month.Days[0].Date.Date;
                    to   = month.Days[^1].Date.Date;
                }
                else
                {
                    from = DateTime.Today.AddDays(-31);
                    to   = DateTime.Today.AddDays(62);
                }
                foreach (var ev in EventsRepository.All)
                    foreach (var occ in EventOccurrenceExpander.Expand(ev, from, to))
                        _datesWithUserEvents.Add(occ.Date);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("MainWindow.BuildUserEventDateIndex", ex);
            }
        }

        private void ApplyHeight(int height)
        {
            if (height == _currentHeight) return;
            _currentHeight = height;
            WindowHelpers.Resize(this, 980, height);
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

        private void OnPrevMonth(object sender, RoutedEventArgs e) { StepHebMonth(-1); Refresh(); }
        private void OnNextMonth(object sender, RoutedEventArgs e) { StepHebMonth(+1); Refresh(); }
        private void OnTodayClick(object sender, RoutedEventArgs e) { SetCurrentHebMonthToToday(); Refresh(); }

        private void OnMonthYearFlyoutOpening(object sender, object e)
        {
            MonthYearPicker.SetCurrent(_hebYear, _hebMonth);
        }

        private void OnMonthYearConfirmed(object sender, (int Year, int Month, int? Day) value)
        {
            _hebYear = value.Year;
            _hebMonth = value.Month;
            MonthYearButton.Flyout?.Hide();

            // When a specific day was typed, jump straight into Daily view at that
            // gregorian date — that's the cleanest way to "go to" an exact day.
            if (value.Day.HasValue)
            {
                var greg = HebcalBridge.ConvertFromHebrew(value.Year, value.Month, value.Day.Value);
                if (greg != null && greg.Year > 0)
                {
                    _dailyDate = new DateTime(greg.Year, greg.Month, greg.Day);
                    if (DailyViewToggle.IsChecked != true)
                    {
                        // Flip the toggle state without going through ApplyViewMode(),
                        // which would otherwise reset _dailyDate back to today.
                        DailyViewToggle.IsChecked = true;
                        MonthlyViewToggle.IsChecked = false;
                        UpdateSegmentedHighlight();
                        DailyContentGrid.Visibility = Visibility.Visible;
                        MonthlyContentGrid.Visibility = Visibility.Collapsed;
                    }
                    RefreshDaily();
                    return;
                }
            }

            Refresh();
        }

        private void OnMonthYearTodayRequested(object sender, EventArgs e)
        {
            SetCurrentHebMonthToToday();
            MonthYearButton.Flyout?.Hide();
            Refresh();
        }

        private void OnConverterClick(object sender, RoutedEventArgs e)
        {
            new ConverterWindow().Activate();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Closed += (_, _) =>
            {
                ThemeHelper.Apply(this, App.Settings.Theme, _backdrop?.Config);
                Refresh();
                App.Tray?.UpdateIcon();
            };
            win.Activate();
        }

        // ─── Daily view ────────────────────────────────────────────────────────────

        private void OnSegToggleClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clicked) return;

            // Enforce mutual exclusion. If the user clicked the already-active one,
            // re-check it (don't allow neither-selected).
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
            DailyContentGrid.Visibility   = daily ? Visibility.Visible : Visibility.Collapsed;
            MonthlyContentGrid.Visibility = daily ? Visibility.Collapsed : Visibility.Visible;
            if (daily)
            {
                _dailyDate = _halachicTodayDate;
                RefreshDaily();
            }
        }

        private void OnAddEventClick(object sender, RoutedEventArgs e)
        {
            var defaultDate = (DailyViewToggle.IsChecked == true)
                ? _dailyDate
                : (_selectedDay?.Date ?? DateTime.Today);
            var editor = new EventEditorWindow(null, defaultDate, () =>
            {
                if (DailyViewToggle.IsChecked == true) RefreshDaily();
                else Refresh();
            });
            editor.Activate();
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

                var headerHeb = $"{HebrewNumberFormatter.FormatDay(hd.HebDay)} ב{hd.MonthName} {HebrewNumberFormatter.FormatYear(hd.HebYear)}";
                var headerGreg = _dailyDate.ToString("d בMMMM yyyy", ci);
                DailyHeaderHeb.Text = headerHeb;
                DailyHeaderGreg.Text = headerGreg;
                DailyDow.Text  = _dailyDate.ToString("dddd", ci);

                // Build a CalendarDay-like wrapper using hebcal's monthly data so events are populated.
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
                    Events = new List<CalendarEvent>()
                };

                DayDetailsRenderer.Render(dayData, new DayDetailsRenderer.Targets
                {
                    EventsSection = DailyEventsSection,
                    EventsPanel = DailyEventsPanel,
                    ZmanimSection = DailyZmanimSection,
                    ZmanimPanel = DailyZmanimPanel,
                    OnEditUserEvent = ev =>
                    {
                        var editor = new EventEditorWindow(ev, _dailyDate, RefreshDaily);
                        editor.Activate();
                    }
                });
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("MainWindow.RefreshDaily", ex);
            }
        }
    }
}
