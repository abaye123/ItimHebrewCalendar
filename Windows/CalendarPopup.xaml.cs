using System;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        private const int OmerExtraHeight = 32;
        private const int AfterSunsetExtraHeight = 28;
        private const int TempleExtraHeight = 40;
        private int _currentHeight = -1;
        private DateTime _halachicTodayDate = DateTime.Today;
        private Brush? _defaultTodayCardBrush;
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush SunsetCardBrush =
            new(global::Windows.UI.Color.FromArgb(255, 0xE0, 0x55, 0x10));

        public CalendarPopup()
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
            WindowHelpers.PositionNearTray(this);

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

                    var temple = SecondTempleTimer.Compute();
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

                    int targetHeight = BaseHeight;
                    if (showOmer) targetHeight += OmerExtraHeight;
                    if (afterSunset) targetHeight += AfterSunsetExtraHeight;
                    if (showTemple) targetHeight += TempleExtraHeight;
                    ApplyHeight(targetHeight);
                }

                var month = HebcalBridge.GetHebrewMonth(_hebYear, _hebMonth,
                    settings.UseIsraeliHolidays, settings.ShowModernHolidays);
                if (month == null || month.Days.Count == 0) return;

                DrawMonthHeader(month);
                DrawDaysGrid(month, settings.ShowGregorianInCalendar);

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
                SettingsManager.LogError("CalendarPopup.Refresh", ex);
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
                Text = day.HebDayStr,
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

            if (hasHoliday || isRoshChodesh)
            {
                sp.Children.Add(new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Margin = new Thickness(0, 1, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Fill = isToday ? CellTheme.TextOnAccent() : CellTheme.AccentBackground(isDark)
                });
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

        private static string BuildDayTooltip(CalendarDay day)
        {
            var parts = new System.Collections.Generic.List<string>
            {
                $"{day.HebDayStr} ב{day.HebMonthName} {HebrewNumberFormatter.FormatYear(day.HebYear)}",
                day.Date.ToString("dddd, d בMMMM yyyy", CultureInfo.GetCultureInfo("he-IL"))
            };
            foreach (var e in day.Events)
            {
                if (!string.IsNullOrEmpty(e.Description))
                    parts.Add($"• {e.Description}");
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
                ZmanimPanel = DetailsZmanimPanel
            });

            CalendarView.Visibility = Visibility.Collapsed;
            DetailsView.Visibility = Visibility.Visible;
        }

        private void OnBackToCalendar(object sender, RoutedEventArgs e)
        {
            DetailsView.Visibility = Visibility.Collapsed;
            CalendarView.Visibility = Visibility.Visible;
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

        private void ApplyHeight(int height)
        {
            if (height == _currentHeight) return;
            _currentHeight = height;
            WindowHelpers.Resize(this, 380, height);
            WindowHelpers.PositionNearTray(this);
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
            new ZmanimWindow().Activate();
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
    }
}
