using System;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        private const int BaseHeight = 680;
        private const int OmerExtraHeight = 32;
        private const int AfterSunsetExtraHeight = 32;
        private const int TempleExtraHeight = 32;
        private int _currentHeight = -1;
        private DateTime _halachicTodayDate = DateTime.Today;
        private Brush? _defaultTodayCardBrush;
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush SunsetCardBrush =
            new(global::Windows.UI.Color.FromArgb(255, 0xE0, 0x55, 0x10));

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

            Refresh();

            Closed += (_, _) =>
            {
                _backdrop?.Dispose();
                _backdrop = null;
            };
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
                ZmanimPanel = DetailsZmanimPanel
            });
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
