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
        private CalendarDay _day;

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

            WindowHelpers.Resize(this, 440, 640);
            WindowHelpers.PositionNearCursor(this);

            _day = day;
            Populate(day);

            Closed += (_, _) =>
            {
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        private void Populate(CalendarDay day)
        {
            _day = day;

            TxtHebrew.Text = HebrewDateFormatter.Full(day.HebDay, day.HebMonthName, day.HebYear, App.Settings.DateFormat);
            TxtGregorian.Text = HebrewDateFormatter.GregorianNoWeekday(day.Date, App.Settings.DateFormat);
            TxtDayOfWeek.Text = day.Date.ToString("dddd",
                CultureInfo.GetCultureInfo("he-IL"));

            EventsPanel.Children.Clear();

            // User events (clickable to edit) + hebcal events
            var userOccurrences = DayDetailsRenderer.GetUserEventsForDate(day.Date);
            bool any = false;
            foreach (var occ in userOccurrences)
            {
                EventsPanel.Children.Add(BuildUserEventCard(occ.Event));
                any = true;
            }
            foreach (var ev in day.Events)
            {
                if (string.IsNullOrEmpty(ev.Description)) continue;
                EventsPanel.Children.Add(BuildHebcalCard(ev));
                any = true;
            }
            EventsSection.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            NoEventsText.Visibility  = any ? Visibility.Collapsed : Visibility.Visible;

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

                    if (!anyShown) ZmanimSection.Visibility = Visibility.Collapsed;
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

        private Border BuildHebcalCard(CalendarEvent ev)
        {
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
                Text = string.IsNullOrEmpty(ev.Emoji) ? ev.Description : $"{ev.Emoji}  {ev.Description}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14
            });
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
            return card;
        }

        private FrameworkElement BuildUserEventCard(UserEvent ev)
        {
            var sp = new StackPanel { Spacing = 2 };

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            titleRow.Children.Add(new FontIcon
            {
                Glyph = "",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            titleRow.Children.Add(new TextBlock
            {
                Text = ev.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(titleRow);

            if (ev.StartTime.HasValue)
            {
                var sub = ev.StartTime.Value.ToString(@"hh\:mm");
                if (ev.Duration.HasValue)
                    sub += " · " + ev.Duration.Value.TotalMinutes.ToString("0") + " דק'";
                sp.Children.Add(new TextBlock
                {
                    Text = sub,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            if (ev.Reminders.Count > 0)
            {
                var rt = $"{ev.Reminders.Count} תזכורות";
                if (ev.Reminders.Count == 1) rt = "תזכורת אחת";
                sp.Children.Add(new TextBlock
                {
                    Text = "🔔 " + rt,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            if (!string.IsNullOrEmpty(ev.Description))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = ev.Description,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            var card = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Background = (Brush)Application.Current.Resources["AccentFillColorTertiaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Child = sp
            };

            var btn = new Button
            {
                Content = card,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
                BorderThickness = new Thickness(0)
            };
            btn.Click += (_, _) =>
            {
                var editor = new EventEditorWindow(ev, _day.Date, () => Populate(_day));
                editor.Activate();
            };
            return btn;
        }

        private void OnAddEvent(object sender, RoutedEventArgs e)
        {
            var editor = new EventEditorWindow(null, _day.Date, () => Populate(_day));
            editor.Activate();
        }
    }
}
