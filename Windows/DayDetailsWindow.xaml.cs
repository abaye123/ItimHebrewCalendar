using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ItimHebrewCalendar.Models;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public sealed partial class DayDetailsWindow : Window
    {
        private BackdropHandles? _backdrop;

        private record ZmanimRow(ZmanimDisplay Flag, string Name, Func<ZmanimInfo, string> Get);

        private static readonly List<ZmanimRow> AllZmanim = new()
        {
            new(ZmanimDisplay.AlotHaShachar, "עלות השחר", z => z.AlotHaShachar),
            new(ZmanimDisplay.Misheyakir, "משיכיר", z => z.Misheyakir),
            new(ZmanimDisplay.Sunrise, "הנץ החמה", z => z.Sunrise),
            new(ZmanimDisplay.SofZmanShmaMGA, "סוף ק\"ש (מג\"א)", z => z.SofZmanShmaMGA),
            new(ZmanimDisplay.SofZmanShma, "סוף ק\"ש (גר\"א)", z => z.SofZmanShma),
            new(ZmanimDisplay.SofZmanTfillaMGA, "סוף תפילה (מג\"א)", z => z.SofZmanTfillaMGA),
            new(ZmanimDisplay.SofZmanTfilla, "סוף תפילה (גר\"א)", z => z.SofZmanTfilla),
            new(ZmanimDisplay.Chatzot, "חצות", z => z.Chatzot),
            new(ZmanimDisplay.MinchaGedola, "מנחה גדולה", z => z.MinchaGedola),
            new(ZmanimDisplay.MinchaKetana, "מנחה קטנה", z => z.MinchaKetana),
            new(ZmanimDisplay.PlagHaMincha, "פלג המנחה", z => z.PlagHaMincha),
            new(ZmanimDisplay.Sunset, "שקיעה", z => z.Sunset),
            new(ZmanimDisplay.Tzeit, "צאת הכוכבים", z => z.Tzeit),
            new(ZmanimDisplay.Tzeit72, "צאה\"כ ר\"ת", z => z.Tzeit72),
        };

        public DayDetailsWindow(CalendarDay day)
        {
            InitializeComponent();

            Title = "פרטי יום - עיתים";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);
            ThemeHelper.Apply(this, App.Settings.Theme, _backdrop.Config);

            WindowHelpers.Resize(this, 420, 600);
            WindowHelpers.PositionNearCursor(this);

            Populate(day);

            Closed += (_, _) =>
            {
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        private void Populate(CalendarDay day)
        {
            TxtHebrew.Text = $"{day.HebDayStr} ב{day.HebMonthName} {day.HebYear}";
            TxtGregorian.Text = day.Date.ToString("d בMMMM yyyy",
                CultureInfo.GetCultureInfo("he-IL"));
            TxtDayOfWeek.Text = day.Date.ToString("dddd",
                CultureInfo.GetCultureInfo("he-IL"));

            // אירועים
            EventsPanel.Children.Clear();
            if (day.Events.Count > 0)
            {
                EventsSection.Visibility = Visibility.Visible;
                foreach (var ev in day.Events)
                {
                    if (string.IsNullOrEmpty(ev.Description)) continue;

                    var card = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 8, 12, 8),
                        Background = (Brush)Application.Current.Resources[
                            ev.IsHoliday || ev.IsMajor
                                ? "SystemFillColorCautionBackgroundBrush"
                                : "CardBackgroundFillColorDefaultBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(1)
                    };

                    var sp = new StackPanel { Spacing = 2 };
                    var line1 = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(ev.Emoji) ? ev.Description : $"{ev.Emoji}  {ev.Description}",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 14
                    };
                    sp.Children.Add(line1);

                    if (!string.IsNullOrEmpty(ev.DescriptionEn) && ev.DescriptionEn != ev.Description)
                    {
                        sp.Children.Add(new TextBlock
                        {
                            Text = ev.DescriptionEn,
                            FontSize = 11,
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            FlowDirection = FlowDirection.LeftToRight
                        });
                    }

                    card.Child = sp;
                    EventsPanel.Children.Add(card);
                }
            }
            else
            {
                EventsSection.Visibility = Visibility.Collapsed;
            }

            // זמני יום
            try
            {
                var loc = App.Settings.GetEffectiveLocation();
                var zmanim = HebcalBridge.GetZmanim(day.Date, loc);
                if (zmanim != null)
                {
                    ZmanimPanel.Children.Clear();
                    bool anyShown = false;
                    foreach (var row in AllZmanim)
                    {
                        if ((App.Settings.ZmanimToShow & row.Flag) == 0) continue;
                        var time = row.Get(zmanim);
                        if (string.IsNullOrEmpty(time)) continue;

                        var g = new Grid
                        {
                            Padding = new Thickness(0, 8, 0, 8),
                            BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                            BorderThickness = new Thickness(0, 0, 0, 1)
                        };
                        g.ColumnDefinitions.Add(new ColumnDefinition());
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var nameTb = new TextBlock { Text = row.Name, FontSize = 13 };
                        Grid.SetColumn(nameTb, 0);
                        g.Children.Add(nameTb);

                        var valTb = new TextBlock
                        {
                            Text = time,
                            FontSize = 13,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                            FlowDirection = FlowDirection.LeftToRight
                        };
                        Grid.SetColumn(valTb, 1);
                        g.Children.Add(valTb);

                        ZmanimPanel.Children.Add(g);
                        anyShown = true;
                    }

                    if (!anyShown)
                    {
                        ZmanimSection.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    ZmanimSection.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("DayDetailsWindow.Zmanim", ex);
                ZmanimSection.Visibility = Visibility.Collapsed;
            }
        }
    }
}
