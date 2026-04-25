using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ItimHebrewCalendar.Models;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public sealed partial class ZmanimWindow : Window
    {
        private BackdropHandles? _backdrop;
        private bool _initializing = true;

        private record ZmanimRow(ZmanimDisplay Flag, string Name, Func<ZmanimInfo, string> Get, bool IsPrimary = false);

        private static readonly List<ZmanimRow> AllZmanim = new()
        {
            new(ZmanimDisplay.AlotHaShachar, "עלות השחר (72 ד')", z => z.AlotHaShachar),
            new(ZmanimDisplay.Misheyakir, "משיכיר", z => z.Misheyakir),
            new(ZmanimDisplay.Sunrise, "הנץ החמה", z => z.Sunrise, IsPrimary: true),
            new(ZmanimDisplay.SofZmanShmaMGA, "סוף זמן ק\"ש (מג\"א)", z => z.SofZmanShmaMGA),
            new(ZmanimDisplay.SofZmanShma, "סוף זמן ק\"ש (גר\"א)", z => z.SofZmanShma, IsPrimary: true),
            new(ZmanimDisplay.SofZmanTfillaMGA, "סוף זמן תפילה (מג\"א)", z => z.SofZmanTfillaMGA),
            new(ZmanimDisplay.SofZmanTfilla, "סוף זמן תפילה (גר\"א)", z => z.SofZmanTfilla),
            new(ZmanimDisplay.Chatzot, "חצות", z => z.Chatzot, IsPrimary: true),
            new(ZmanimDisplay.MinchaGedola, "מנחה גדולה", z => z.MinchaGedola, IsPrimary: true),
            new(ZmanimDisplay.MinchaKetana, "מנחה קטנה", z => z.MinchaKetana),
            new(ZmanimDisplay.PlagHaMincha, "פלג המנחה", z => z.PlagHaMincha),
            new(ZmanimDisplay.Sunset, "שקיעה", z => z.Sunset, IsPrimary: true),
            new(ZmanimDisplay.Tzeit, "צאת הכוכבים", z => z.Tzeit, IsPrimary: true),
            new(ZmanimDisplay.Tzeit72, "צאת הכוכבים (ר\"ת)", z => z.Tzeit72),
        };

        public ZmanimWindow()
        {
            InitializeComponent();

            WindowHelpers.LoadAppIconInto(TitleBarIcon);

            Title = "זמני היום - עיתים";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);
            ThemeHelper.Apply(this, App.Settings.Theme, _backdrop.Config);

            WindowHelpers.Resize(this, 560, 760);
            WindowHelpers.CenterOnScreen(this);

            PopulateControls();
            _initializing = false;
            Refresh();

            Closed += (_, _) =>
            {
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        private void PopulateControls()
        {
            // ברירת מחדל לתאריך - היום ההלכתי (אחרי שקיעה: מחר לועזי) אם ההגדרה מופעלת
            DatePicker.Date = HebcalBridge.GetHalachicGregorianDate(
                App.Settings.GetEffectiveLocation(),
                App.Settings.UseSunsetDateTransition);

            LocationCombo.Items.Clear();
            foreach (var c in CitiesDatabase.Cities)
            {
                LocationCombo.Items.Add(new ComboBoxItem { Content = c.Name, Tag = c.Name });
            }
            for (int i = 0; i < LocationCombo.Items.Count; i++)
            {
                if (LocationCombo.Items[i] is ComboBoxItem item && (string)item.Tag! == App.Settings.CityName)
                {
                    LocationCombo.SelectedIndex = i;
                    break;
                }
            }
            if (LocationCombo.SelectedIndex < 0) LocationCombo.SelectedIndex = 0;
        }

        private void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (_initializing) return;
            Refresh();
        }

        private void OnLocationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                if (DatePicker.Date is not DateTimeOffset dto) return;
                if (LocationCombo.SelectedItem is not ComboBoxItem cityItem) return;
                if (cityItem.Tag is not string cityName) return;

                var date = dto.DateTime;
                var city = CitiesDatabase.FindByName(cityName) ?? CitiesDatabase.Default;

                var loc = new LocationInfo
                {
                    Name = city.Name,
                    NameEn = city.NameEn,
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Elevation = city.Elevation,
                    TimeZone = city.TimeZone,
                    IsInIsrael = city.IsInIsrael,
                    CandleLightingMinutes = city.CandleLightingMinutes
                };

                var heb = HebcalBridge.Convert(date);
                var hebStr = heb != null ? heb.Render : "";
                TxtSubtitle.Text = $"{hebStr}  ·  {date.ToString("dddd, d בMMMM yyyy", CultureInfo.GetCultureInfo("he-IL"))}  ·  {city.Name}";

                var zmanim = ZmanimService.GetZmanim(date, loc);
                if (zmanim == null)
                {
                    ResultsPanel.Children.Clear();
                    return;
                }

                ResultsPanel.Children.Clear();

                bool isFriday = date.DayOfWeek == DayOfWeek.Friday;
                var monthCal = HebcalBridge.GetMonth(date.Year, date.Month, loc.IsInIsrael, App.Settings.ShowModernHolidays);
                var dayInfo = monthCal?.Days.FirstOrDefault(d => d.GregDay == date.Day);
                bool isErevChag = dayInfo?.Events.Any(e => e.IsCandleLighting && !isFriday) ?? false;

                if (isFriday || isErevChag)
                {
                    var specialCard = new Border
                    {
                        Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(14, 10, 14, 10)
                    };
                    var specialStack = new StackPanel { Spacing = 4 };

                    var candleEv = dayInfo?.Events.FirstOrDefault(e => e.IsCandleLighting);
                    if (candleEv != null)
                    {
                        specialStack.Children.Add(MakeSpecialRow("🕯️ הדלקת נרות", candleEv.Description));
                    }

                    if (isFriday)
                    {
                        var shabbat = HebcalBridge.GetShabbat(loc);
                        if (shabbat != null)
                        {
                            if (!string.IsNullOrEmpty(shabbat.Havdalah))
                                specialStack.Children.Add(MakeSpecialRow("✨ הבדלה", shabbat.Havdalah));
                            if (!string.IsNullOrEmpty(shabbat.Parasha))
                                specialStack.Children.Add(MakeSpecialRow("📖 פרשת השבוע", shabbat.Parasha));
                        }
                    }

                    specialCard.Child = specialStack;
                    ResultsPanel.Children.Add(specialCard);
                }

                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 4, 14, 4)
                };
                var stack = new StackPanel();

                bool anyShown = false;
                foreach (var row in AllZmanim)
                {
                    if ((App.Settings.ZmanimToShow & row.Flag) == 0) continue;
                    var time = row.Get(zmanim);
                    if (string.IsNullOrEmpty(time)) continue;
                    stack.Children.Add(MakeZmanimRow(row.Name, time, row.IsPrimary));
                    anyShown = true;
                }

                if (!anyShown)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "לא נבחרו זמנים להצגה. עבור להגדרות → \"זמני היום להצגה\" כדי לבחור.",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 20),
                        TextAlignment = TextAlignment.Center
                    });
                }

                card.Child = stack;
                ResultsPanel.Children.Add(card);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ZmanimWindow.Refresh", ex);
            }
        }

        private UIElement MakeSpecialRow(string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblTb = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
            };
            Grid.SetColumn(lblTb, 0);

            var valTb = new TextBlock
            {
                Text = value,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
                FlowDirection = FlowDirection.LeftToRight
            };
            Grid.SetColumn(valTb, 1);

            grid.Children.Add(lblTb);
            grid.Children.Add(valTb);
            return grid;
        }

        private UIElement MakeZmanimRow(string name, string time, bool isPrimary)
        {
            var grid = new Grid
            {
                Padding = new Thickness(0, 10, 0, 10),
                BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblTb = new TextBlock
            {
                Text = name,
                FontSize = isPrimary ? 15 : 14,
                FontWeight = isPrimary ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblTb, 0);

            var valTb = new TextBlock
            {
                Text = time,
                FontSize = isPrimary ? 16 : 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };
            Grid.SetColumn(valTb, 1);

            grid.Children.Add(lblTb);
            grid.Children.Add(valTb);
            return grid;
        }
    }
}
