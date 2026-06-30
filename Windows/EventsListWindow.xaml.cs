using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ItimHebrewCalendar.Models;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public sealed partial class EventsListWindow : Window
    {
        private BackdropHandles? _backdrop;
        private string _filter = "";

        public EventsListWindow()
        {
            InitializeComponent();

            WindowHelpers.LoadAppIconInto(TitleBarIcon);

            Title = "כל האירועים - עיתים";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);
            ThemeHelper.Apply(this, App.Settings.Theme, _backdrop.Config);

            WindowHelpers.Resize(this, 520, 680);
            WindowHelpers.CenterOnScreen(this);

            BuildList();

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
            try { DispatcherQueue.TryEnqueue(BuildList); }
            catch { }
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            _filter = SearchBox.Text?.Trim() ?? "";
            BuildList();
        }

        private void OnSortChanged(object sender, SelectionChangedEventArgs e) => BuildList();

        private IEnumerable<UserEvent> ApplySort(IEnumerable<UserEvent> events)
        {
            var tag = (SortCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "DateDesc";
            return tag switch
            {
                "DateAsc" => events.OrderBy(ev => ev.StartGregorian ?? DateTime.MaxValue),
                "Created" => events.OrderByDescending(ev => ev.CreatedUtc),
                "Title"   => events.OrderBy(ev => ev.Title ?? "", StringComparer.CurrentCultureIgnoreCase),
                _         => events.OrderByDescending(ev => ev.StartGregorian ?? DateTime.MinValue),
            };
        }

        private void OnAddEventClick(object sender, RoutedEventArgs e)
        {
            EventEditorWindow.OpenForNew(DateTime.Today, BuildList);
        }

        private void BuildList()
        {
            try
            {
                EventsPanel.Children.Clear();

                var all = ApplySort(EventsRepository.All).ToList();

                var visible = string.IsNullOrEmpty(_filter)
                    ? all
                    : all.Where(ev => Matches(ev, _filter)).ToList();

                CountText.Text = all.Count == visible.Count
                    ? $"{all.Count} אירועים"
                    : $"{visible.Count} מתוך {all.Count} אירועים";

                EmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                EmptyText.Text = all.Count == 0
                    ? "אין אירועים שמורים."
                    : "לא נמצאו אירועים התואמים לחיפוש.";

                foreach (var ev in visible)
                    EventsPanel.Children.Add(BuildEventCard(ev));
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("EventsListWindow.BuildList", ex);
            }
        }

        private static bool Matches(UserEvent ev, string filter)
        {
            return (ev.Title?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (ev.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private FrameworkElement BuildEventCard(UserEvent ev)
        {
            var fmt = App.Settings.DateFormat;
            var sp = new StackPanel { Spacing = 3 };

            // Title row (+ optional color dot).
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (!string.IsNullOrEmpty(ev.ColorTag) && TryParseColor(ev.ColorTag, out var color))
            {
                titleRow.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            titleRow.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(ev.Title) ? "(ללא כותרת)" : ev.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(titleRow);

            // Date line: Hebrew · Gregorian.
            var greg = ev.StartGregorian ?? DateTime.Today;
            string dateLine;
            try
            {
                var heb = HebcalBridge.Convert(greg);
                var hebStr = heb != null
                    ? HebrewDateFormatter.Full(heb.HebDay, heb.MonthName, heb.HebYear, fmt)
                    : "";
                var gregStr = HebrewDateFormatter.GregorianNoWeekday(greg, fmt);
                dateLine = string.IsNullOrEmpty(hebStr) ? gregStr : $"{hebStr}  ·  {gregStr}";
            }
            catch
            {
                dateLine = HebrewDateFormatter.GregorianNoWeekday(greg, fmt);
            }
            sp.Children.Add(new TextBlock
            {
                Text = dateLine,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            });

            // Badges line: time / all-day, recurrence, reminders.
            var badges = new List<string>();
            badges.Add(ev.StartTime.HasValue ? ev.StartTime.Value.ToString(@"hh\:mm") : "כל היום");
            var recur = RecurrenceLabel(ev.Recurrence);
            if (recur != null) badges.Add(recur);
            int reminderCount = ev.Reminders.Count(r => r.Enabled);
            if (reminderCount > 0)
                badges.Add(reminderCount == 1 ? "תזכורת אחת" : $"{reminderCount} תזכורות");
            if (ev.IsImported) badges.Add("מיובא");

            sp.Children.Add(new TextBlock
            {
                Text = string.Join("  ·  ", badges),
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            if (!string.IsNullOrEmpty(ev.Description))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = ev.Description,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis,
                    MaxLines = 2,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
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
            ToolTipService.SetToolTip(btn, "לחץ לעריכה");
            btn.Click += (_, _) =>
            {
                EventEditorWindow.OpenForEdit(ev, greg, BuildList);
            };
            return btn;
        }

        private static string? RecurrenceLabel(RecurrenceRule? r) => r?.Kind switch
        {
            RecurrenceKind.DailyGregorian   => "חוזר · יומי",
            RecurrenceKind.WeeklyGregorian  => "חוזר · שבועי",
            RecurrenceKind.MonthlyGregorian => "חוזר · חודשי (לועזי)",
            RecurrenceKind.YearlyGregorian  => "חוזר · שנתי (לועזי)",
            RecurrenceKind.MonthlyHebrew    => "חוזר · חודשי (עברי)",
            RecurrenceKind.YearlyHebrew     => "חוזר · שנתי (עברי)",
            _ => null
        };

        private static bool TryParseColor(string hex, out global::Windows.UI.Color color)
        {
            color = default;
            try
            {
                var s = hex.TrimStart('#');
                if (s.Length == 6)
                {
                    byte r = System.Convert.ToByte(s.Substring(0, 2), 16);
                    byte g = System.Convert.ToByte(s.Substring(2, 2), 16);
                    byte b = System.Convert.ToByte(s.Substring(4, 2), 16);
                    color = global::Windows.UI.Color.FromArgb(255, r, g, b);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
