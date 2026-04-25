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
    // Shared day-detail renderer used by both the inline tray view and the
    // MainWindow side panel: header text, holiday cards, halachic times.
    internal static class DayDetailsRenderer
    {
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

        public class Targets
        {
            public TextBlock? HebrewLabel { get; set; }
            public TextBlock? GregorianLabel { get; set; }
            public TextBlock? DayOfWeekLabel { get; set; }
            public FrameworkElement? EventsSection { get; set; }
            public StackPanel? EventsPanel { get; set; }
            public FrameworkElement? ZmanimSection { get; set; }
            public StackPanel? ZmanimPanel { get; set; }
        }

        public static void Render(CalendarDay day, Targets t)
        {
            var ci = CultureInfo.GetCultureInfo("he-IL");

            if (t.HebrewLabel != null)
                t.HebrewLabel.Text = $"{day.HebDayStr} ב{day.HebMonthName} {HebrewNumberFormatter.FormatYear(day.HebYear)}";
            if (t.GregorianLabel != null)
                t.GregorianLabel.Text = day.Date.ToString("d בMMMM yyyy", ci);
            if (t.DayOfWeekLabel != null)
                t.DayOfWeekLabel.Text = day.Date.ToString("dddd", ci);

            RenderEvents(day, t);
            RenderZmanim(day, t);
        }

        private static void RenderEvents(CalendarDay day, Targets t)
        {
            if (t.EventsPanel == null) return;
            t.EventsPanel.Children.Clear();

            if (day.Events.Count == 0)
            {
                if (t.EventsSection != null) t.EventsSection.Visibility = Visibility.Collapsed;
                return;
            }

            bool any = false;
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
                sp.Children.Add(new TextBlock
                {
                    Text = ev.Description,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14
                });

                card.Child = sp;
                t.EventsPanel.Children.Add(card);
                any = true;
            }

            if (t.EventsSection != null)
                t.EventsSection.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void RenderZmanim(CalendarDay day, Targets t)
        {
            if (t.ZmanimPanel == null) return;
            t.ZmanimPanel.Children.Clear();

            try
            {
                var loc = App.Settings.GetEffectiveLocation();
                var zmanim = ZmanimService.GetZmanim(day.Date, loc);
                if (zmanim == null)
                {
                    if (t.ZmanimSection != null) t.ZmanimSection.Visibility = Visibility.Collapsed;
                    return;
                }

                t.ZmanimPanel.Children.Add(new InfoBar
                {
                    IsOpen = true,
                    IsClosable = false,
                    Severity = InfoBarSeverity.Warning,
                    Title = "הזמנים למידע בלבד",
                    Message = "ייתכנו סטיות של מספר דקות. אין לסמוך להלכה למעשה",
                    Margin = new Thickness(0, 0, 0, 6)
                });

                bool any = false;
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

                    t.ZmanimPanel.Children.Add(g);
                    any = true;
                }

                if (t.ZmanimSection != null)
                    t.ZmanimSection.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("DayDetailsRenderer.Zmanim", ex);
                if (t.ZmanimSection != null) t.ZmanimSection.Visibility = Visibility.Collapsed;
            }
        }
    }
}
