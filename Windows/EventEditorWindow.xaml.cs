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
    public sealed partial class EventEditorWindow : Window
    {
        private static readonly string[] HebrewMonthNames =
        {
            "", "ניסן", "אייר", "סיון", "תמוז", "אב", "אלול",
            "תשרי", "חשון", "כסלו", "טבת", "שבט", "אדר", "אדר ב'"
        };

        private BackdropHandles? _backdrop;
        private UserEvent _event;
        private readonly bool _isNew;
        private List<ReminderRule> _reminders = new();
        private Action? _onSavedOrDeleted;

        // Key for the single shared "new event" draft window. A new event has no
        // stable id yet, so all "add event" entry points share one draft window.
        private static readonly object NewEventKey = new();

        // Opens (or re-focuses) the editor for an existing event. If a window is already
        // open for this event, it's brought to the foreground instead of creating another.
        public static EventEditorWindow OpenForEdit(UserEvent existing, DateTime defaultDate, Action? onSavedOrDeleted = null)
            => WindowManager.Show(existing.Id, () => new EventEditorWindow(existing, defaultDate, onSavedOrDeleted));

        // Opens (or re-focuses) the shared draft window for creating a new event.
        public static EventEditorWindow OpenForNew(DateTime defaultDate, Action? onSavedOrDeleted = null)
            => WindowManager.Show(NewEventKey, () => new EventEditorWindow(null, defaultDate, onSavedOrDeleted));

        public EventEditorWindow(UserEvent? existing, DateTime defaultDate, Action? onSavedOrDeleted = null)
        {
            InitializeComponent();
            _onSavedOrDeleted = onSavedOrDeleted;

            _isNew = existing == null;
            _event = existing ?? CreateNewWithDefaults(defaultDate);

            Title = _isNew ? "אירוע חדש - עיתים" : "עריכת אירוע - עיתים";
            HeaderText.Text = _isNew ? "אירוע חדש" : "עריכת אירוע";
            TitleBarText.Text = HeaderText.Text;
            DeleteButton.Visibility = _isNew ? Visibility.Collapsed : Visibility.Visible;

            RootGrid.FlowDirection = FlowDirection.RightToLeft;
            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);
            ThemeHelper.Apply(this, App.Settings.Theme, _backdrop.Config);
            WindowHelpers.Resize(this, 460, 760);
            WindowHelpers.PositionNearCursor(this);

            PopulateHebrewControls();
            LoadFromEvent(_event);
            RebuildRemindersUi();

            Closed += (_, _) =>
            {
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        // ─── Populate ──────────────────────────────────────────────────────────────

        private const int YearLookaheadCount = 10;

        // Cache so flipping months/years doesn't re-cross the marshal boundary every time.
        private static readonly Dictionary<(int year, int month), bool> Day30Cache = new();

        private void PopulateHebrewControls()
        {
            HebMonthCombo.Items.Clear();
            for (int m = 1; m <= 13; m++)
                HebMonthCombo.Items.Add(new ComboBoxItem { Content = HebrewMonthNames[m], Tag = m });

            PopulateYearCombo();
            PopulateDayCombo(allow30: true);
        }

        private void PopulateYearCombo()
        {
            int current = GetCurrentHebrewYear();
            int eventYear = _event.StartHebrew?.Year ?? current;
            int start = Math.Min(current, eventYear);
            int end = Math.Max(current + YearLookaheadCount, eventYear);

            HebYearCombo.Items.Clear();
            for (int y = start; y <= end; y++)
                HebYearCombo.Items.Add(new ComboBoxItem { Content = FormatYearShort(y), Tag = y });
        }

        private static int GetCurrentHebrewYear()
        {
            try
            {
                var today = HebcalBridge.Convert(DateTime.Today);
                if (today != null) return today.HebYear;
            }
            catch (Exception ex) { SettingsManager.LogError("GetCurrentHebrewYear", ex); }
            return 5786;
        }

        private void PopulateDayCombo(bool allow30)
        {
            int? prev = TagAsInt(HebDayCombo);
            int max = allow30 ? 30 : 29;

            HebDayCombo.Items.Clear();
            for (int d = 1; d <= max; d++)
                HebDayCombo.Items.Add(new ComboBoxItem { Content = HebrewNumberFormatter.FormatDay(d), Tag = d });

            if (prev.HasValue && prev.Value >= 1 && prev.Value <= max)
                SelectByTag(HebDayCombo, prev.Value);
        }

        private void RefreshDayCombo()
        {
            int? month = TagAsInt(HebMonthCombo);
            int? year  = TagAsInt(HebYearCombo);
            if (!month.HasValue || !year.HasValue) return;
            PopulateDayCombo(allow30: MonthHasDay30(year.Value, month.Value));
        }

        // Computed without poking the Hebcal DLL with an out-of-range day - some
        // native paths panic instead of returning an empty result, which kills the
        // whole process with no managed exception to log.
        public static bool MonthHasDay30(int year, int month)
        {
            if (Day30Cache.TryGetValue((year, month), out var cached)) return cached;

            bool has = month switch
            {
                1 or 3 or 5 or 7 or 11 => true,        // Nisan, Sivan, Av, Tishrei, Shvat - always 30
                2 or 4 or 6 or 10 or 13 => false,      // Iyar, Tammuz, Elul, Tevet, Adar II - always 29
                12 => IsHebrewLeapYear(year),          // Adar = 29 in regular, Adar I = 30 in leap
                8 or 9 => CheshvanOrKislevHas30(year, month),
                _ => false,
            };
            Day30Cache[(year, month)] = has;
            return has;
        }

        private static bool IsHebrewLeapYear(int year)
        {
            // Standard 19-year metonic cycle: leap years are positions 3, 6, 8, 11, 14, 17, 19.
            return ((7 * year) + 1) % 19 < 7;
        }

        // Cheshvan (8) and Kislev (9) are the variable-length months. Cheshvan is 30
        // only in "complete" (shleimah) years; Kislev is 30 in regular or complete.
        // We probe the year length by measuring days between successive Tishrei 1's -
        // both calls are guaranteed-valid Hebrew dates, so they can't trip a native panic.
        private static bool CheshvanOrKislevHas30(int year, int month)
        {
            try
            {
                var g1 = HebcalBridge.ConvertFromHebrew(year, 7, 1);
                var g2 = HebcalBridge.ConvertFromHebrew(year + 1, 7, 1);
                if (g1 == null || g2 == null || g1.Year == 0 || g2.Year == 0) return true;

                int length = (new DateTime(g2.Year, g2.Month, g2.Day)
                            - new DateTime(g1.Year, g1.Month, g1.Day)).Days;

                // Possible Hebrew year lengths: 353/383 (deficient), 354/384 (regular), 355/385 (complete).
                if (month == 8) return length == 355 || length == 385;                  // Cheshvan
                return length == 354 || length == 355 || length == 384 || length == 385; // Kislev
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("CheshvanOrKislevHas30", ex);
                return true; // fail open
            }
        }

        // Short-form Hebrew year (e.g. 5786 → "תשפ"ו") - same gershayim convention
        // as the rest of the app, but without the leading "ה'" thousand prefix.
        private static string FormatYearShort(int year)
        {
            int n = year >= 5000 ? year - 5000 : year;
            return HebrewNumberFormatter.FormatDay(n);
        }

        private static UserEvent CreateNewWithDefaults(DateTime defaultDate)
        {
            var ev = new UserEvent { StartGregorian = defaultDate.Date };
            try
            {
                var hd = HebcalBridge.Convert(defaultDate.Date);
                if (hd != null)
                {
                    ev.StartHebrew = new HebrewDateRef
                    {
                        Year  = hd.HebYear,
                        Month = hd.HebMonth,
                        Day   = hd.HebDay
                    };
                }
            }
            catch (Exception ex) { SettingsManager.LogError("EventEditorWindow.NewDefaults", ex); }
            return ev;
        }

        private void LoadFromEvent(UserEvent ev)
        {
            TitleBox.Text = ev.Title ?? "";
            DescriptionBox.Text = ev.Description ?? "";

            // Default to Hebrew (index 0 after the XAML reorder)
            DateModeCombo.SelectedIndex = 0;
            if (ev.StartGregorian.HasValue)
                GregDatePicker.Date = ev.StartGregorian.Value;
            if (ev.StartHebrew != null)
            {
                SelectByTag(HebMonthCombo, ev.StartHebrew.Month);
                SelectByTag(HebYearCombo, ev.StartHebrew.Year);
                // Month/year both set: refresh day list so ל' shows only if valid, then select.
                RefreshDayCombo();
                SelectByTag(HebDayCombo, ev.StartHebrew.Day);
            }

            if (ev.StartTime.HasValue)
            {
                AllDaySwitch.IsOn = false;
                StartTimePicker.Time = ev.StartTime.Value;
            }
            else
            {
                AllDaySwitch.IsOn = true;
            }
            DurationMinutesBox.Value = ev.Duration?.TotalMinutes ?? double.NaN;

            ApplyAllDay(AllDaySwitch.IsOn);

            // Recurrence
            var rec = ev.Recurrence;
            string tag = (rec?.Kind ?? RecurrenceKind.None).ToString();
            SelectByTag(RecurrenceCombo, tag);
            if (rec != null)
            {
                IntervalBox.Value = rec.Interval;
                if (rec.Until.HasValue) UntilPicker.Date = rec.Until.Value;
                if (rec.Weekdays.HasValue) ApplyWeekdayMask(rec.Weekdays.Value);
            }

            _reminders = ev.Reminders.Select(CloneRule).ToList();
        }

        private static ReminderRule CloneRule(ReminderRule r) => new()
        {
            Id = r.Id,
            AnchorKind = r.AnchorKind,
            ZmanAnchors = r.ZmanAnchors.Select(a => new ZmanimAnchor { Zman = a.Zman }).ToList(),
            ZmanCombination = r.ZmanCombination,
            OffsetMinutes = r.OffsetMinutes,
            AbsoluteWhen = r.AbsoluteWhen,
            Enabled = r.Enabled
        };

        // ─── Event handlers ────────────────────────────────────────────────────────

        private void DateModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool greg = (DateModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Greg";
            GregDatePicker.Visibility = greg ? Visibility.Visible : Visibility.Collapsed;
            HebDatePanel.Visibility   = greg ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AllDaySwitch_Toggled(object sender, RoutedEventArgs e) => ApplyAllDay(AllDaySwitch.IsOn);

        private void ApplyAllDay(bool allDay)
        {
            StartTimePicker.IsEnabled = !allDay;
            DurationMinutesBox.IsEnabled = !allDay;
            TimePanel.Opacity = allDay ? 0.5 : 1.0;
        }

        private void RecurrenceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tag = (RecurrenceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool none = tag == "None" || string.IsNullOrEmpty(tag);
            RecurrenceDetailsPanel.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
            WeekdaysPanel.Visibility = tag == "WeeklyGregorian" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HebMonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshDayCombo();
        private void HebYearCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshDayCombo();

        private void OnAddReminder(object sender, RoutedEventArgs e)
        {
            _reminders.Add(new ReminderRule
            {
                AnchorKind = ReminderAnchorKind.FixedOffset,
                OffsetMinutes = -10
            });
            RebuildRemindersUi();
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (_isNew) { Close(); return; }
            EventsRepository.Delete(_event.Id);
            _onSavedOrDeleted?.Invoke();
            Close();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (!Validate(out var error))
            {
                ValidationBar.Title = "לא ניתן לשמור";
                ValidationBar.Message = error;
                ValidationBar.IsOpen = true;
                return;
            }

            ApplyToEvent(_event);
            EventsRepository.AddOrUpdate(_event);
            _onSavedOrDeleted?.Invoke();
            Close();
        }

        // ─── Validation & extraction ───────────────────────────────────────────────

        private bool Validate(out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            { error = "יש להזין כותרת לאירוע."; return false; }

            var startDate = ResolveStartDate();
            if (startDate == null)
            { error = "יש לבחור תאריך תקין."; return false; }

            if (!AllDaySwitch.IsOn && StartTimePicker.Time == TimeSpan.Zero)
            {
                // Allow midnight, but if not all-day we expect *something*. Treat 0 as valid.
            }

            // Block reminders in the past relative to "now"
            var now = DateTimeOffset.Now;
            var loc = App.Settings.GetEffectiveLocation();
            var startTime = AllDaySwitch.IsOn ? (TimeSpan?)null : StartTimePicker.Time;
            var probeStart = startDate.Value.Date.Add(startTime ?? TimeSpan.Zero);

            foreach (var r in _reminders.Where(x => x.Enabled))
            {
                if (r.AnchorKind == ReminderAnchorKind.FixedOffset)
                {
                    var fire = probeStart.AddMinutes(r.OffsetMinutes);
                    if (new DateTimeOffset(fire) < now)
                    {
                        error = "אחת התזכורות נופלת בעבר. שנה את זמן האירוע או את ההיסט.";
                        return false;
                    }
                }
                else if (r.AnchorKind == ReminderAnchorKind.AbsoluteDateTime)
                {
                    if (!r.AbsoluteWhen.HasValue)
                    { error = "יש לבחור תאריך ושעה לתזכורת."; return false; }
                    if (new DateTimeOffset(r.AbsoluteWhen.Value) < now)
                    {
                        error = "אחת התזכורות נופלת בעבר. שנה את תאריך/שעת התזכורת.";
                        return false;
                    }
                }
                else
                {
                    if (r.ZmanAnchors.Count == 0)
                    { error = "לכל תזכורת מסוג זמן הלכתי יש לבחור לפחות עוגן אחד."; return false; }

                    foreach (var anchor in r.ZmanAnchors)
                    {
                        var t = ReminderScheduler.ResolveZmanInstant(startDate.Value, anchor.Zman, loc);
                        if (t.HasValue && t.Value.AddMinutes(r.OffsetMinutes) < now &&
                            r.ZmanCombination == ZmanCombination.All)
                        {
                            error = "אחת התזכורות נופלת בעבר. שנה את זמן האירוע או את ההיסט.";
                            return false;
                        }
                    }
                    // For Earliest, only check the earliest:
                    if (r.ZmanCombination == ZmanCombination.Earliest)
                    {
                        var fires = r.ZmanAnchors
                            .Select(a => ReminderScheduler.ResolveZmanInstant(startDate.Value, a.Zman, loc))
                            .Where(x => x.HasValue)
                            .Select(x => x!.Value.AddMinutes(r.OffsetMinutes))
                            .ToList();
                        if (fires.Count > 0 && fires.Min() < now)
                        {
                            error = "אחת התזכורות נופלת בעבר. שנה את זמן האירוע או את ההיסט.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private DateTime? ResolveStartDate()
        {
            bool greg = (DateModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Greg";
            if (greg)
            {
                if (!GregDatePicker.Date.HasValue) return null;
                return GregDatePicker.Date.Value.DateTime.Date;
            }
            else
            {
                int? day   = TagAsInt(HebDayCombo);
                int? month = TagAsInt(HebMonthCombo);
                int? year  = TagAsInt(HebYearCombo);
                if (!day.HasValue || !month.HasValue || !year.HasValue) return null;
                var g = HebcalBridge.ConvertFromHebrew(year.Value, month.Value, day.Value);
                if (g == null || g.Year == 0) return null;
                return new DateTime(g.Year, g.Month, g.Day);
            }
        }

        private void ApplyToEvent(UserEvent ev)
        {
            ev.Title = TitleBox.Text.Trim();
            ev.Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();

            var startDate = ResolveStartDate()!.Value;
            ev.StartGregorian = startDate;
            // EventsRepository.FillMissingDate will fill StartHebrew on save.
            ev.StartHebrew = null;

            ev.StartTime = AllDaySwitch.IsOn ? null : StartTimePicker.Time;
            ev.Duration = (!AllDaySwitch.IsOn && !double.IsNaN(DurationMinutesBox.Value) && DurationMinutesBox.Value > 0)
                ? TimeSpan.FromMinutes(DurationMinutesBox.Value)
                : null;

            var recTag = (RecurrenceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
            if (recTag == "None")
            {
                ev.Recurrence = null;
            }
            else
            {
                var kind = Enum.Parse<RecurrenceKind>(recTag);
                ev.Recurrence = new RecurrenceRule
                {
                    Kind = kind,
                    Interval = (int)Math.Max(1, IntervalBox.Value),
                    Until = UntilPicker.Date?.DateTime,
                    Weekdays = kind == RecurrenceKind.WeeklyGregorian ? CollectWeekdayMask() : null
                };
            }

            ev.Reminders = _reminders.Select(CloneRule).ToList();
        }

        // ─── Reminders dynamic UI ──────────────────────────────────────────────────

        private void RebuildRemindersUi()
        {
            RemindersPanel.Children.Clear();
            for (int i = 0; i < _reminders.Count; i++)
                RemindersPanel.Children.Add(BuildReminderCard(_reminders[i], i));
        }

        private Border BuildReminderCard(ReminderRule rule, int index)
        {
            var sp = new StackPanel { Spacing = 6 };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var enabledCheck = new CheckBox
            {
                Content = $"תזכורת {index + 1}",
                IsChecked = rule.Enabled
            };
            enabledCheck.Checked   += (_, _) => rule.Enabled = true;
            enabledCheck.Unchecked += (_, _) => rule.Enabled = false;
            Grid.SetColumn(enabledCheck, 0);
            headerGrid.Children.Add(enabledCheck);

            var removeBtn = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(8, 4, 8, 4)
            };
            removeBtn.Click += (_, _) =>
            {
                _reminders.RemoveAt(index);
                RebuildRemindersUi();
            };
            Grid.SetColumn(removeBtn, 1);
            headerGrid.Children.Add(removeBtn);
            sp.Children.Add(headerGrid);

            // Anchor type
            var anchorCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Header = "סוג עוגן"
            };
            anchorCombo.Items.Add(new ComboBoxItem { Content = "זמן קבוע (יחסית להתחלה)", Tag = "Fixed" });
            anchorCombo.Items.Add(new ComboBoxItem { Content = "זמן הלכתי", Tag = "Zman" });
            anchorCombo.Items.Add(new ComboBoxItem { Content = "תאריך ושעה מסוימים", Tag = "Absolute" });
            anchorCombo.SelectedIndex = rule.AnchorKind switch
            {
                ReminderAnchorKind.Zman => 1,
                ReminderAnchorKind.AbsoluteDateTime => 2,
                _ => 0
            };
            sp.Children.Add(anchorCombo);

            // Offset (value + minutes/hours unit) - for Fixed & Zman anchors.
            var offsetInput = ReminderUiHelpers.BuildOffsetInput(
                rule.OffsetMinutes, m => rule.OffsetMinutes = m);
            sp.Children.Add(offsetInput);

            // Zman combination - for Zman anchor only.
            var combineCombo = new ComboBox
            {
                Header = "מצרף עוגנים",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            combineCombo.Items.Add(new ComboBoxItem { Content = "המוקדם ביותר", Tag = "Earliest" });
            combineCombo.Items.Add(new ComboBoxItem { Content = "כל אחד בנפרד", Tag = "All" });
            combineCombo.SelectedIndex = rule.ZmanCombination == ZmanCombination.All ? 1 : 0;
            combineCombo.SelectionChanged += (_, _) =>
            {
                var tag = (combineCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                rule.ZmanCombination = tag == "All" ? ZmanCombination.All : ZmanCombination.Earliest;
            };
            sp.Children.Add(combineCombo);

            // Absolute date + time - for the AbsoluteDateTime anchor only.
            var absInit = rule.AbsoluteWhen ?? DateTime.Today.AddDays(1).AddHours(9);
            var absGrid = new Grid();
            absGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            absGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var absDatePicker = new CalendarDatePicker
            {
                Header = "תאריך",
                Date = new DateTimeOffset(absInit.Date),
                DateFormat = "{day.integer}/{month.integer}/{year.full}",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 0,
                Margin = new Thickness(0, 0, 4, 0)
            };
            var absTimePicker = new TimePicker
            {
                Header = "שעה",
                Time = absInit.TimeOfDay,
                ClockIdentifier = "24HourClock",
                Margin = new Thickness(4, 0, 0, 0)
            };
            void UpdateAbsolute()
            {
                var d = absDatePicker.Date?.DateTime.Date ?? DateTime.Today;
                rule.AbsoluteWhen = d.Add(absTimePicker.Time);
            }
            absDatePicker.DateChanged += (_, _) => UpdateAbsolute();
            absTimePicker.TimeChanged += (_, _) => UpdateAbsolute();
            Grid.SetColumn(absDatePicker, 0);
            absGrid.Children.Add(absDatePicker);
            Grid.SetColumn(absTimePicker, 1);
            absGrid.Children.Add(absTimePicker);
            sp.Children.Add(absGrid);

            // Zman anchors panel
            var zmanPanel = new StackPanel { Spacing = 4 };
            void RebuildZmanPanel()
            {
                zmanPanel.Children.Clear();
                for (int i = 0; i < rule.ZmanAnchors.Count; i++)
                {
                    int idx = i;
                    var rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var zCombo = BuildZmanCombo(rule.ZmanAnchors[idx].Zman, k => rule.ZmanAnchors[idx].Zman = k);
                    Grid.SetColumn(zCombo, 0);
                    rowGrid.Children.Add(zCombo);
                    var rmBtn = new Button { Content = new FontIcon { Glyph = "", FontSize = 12 }, Margin = new Thickness(4, 0, 0, 0) };
                    rmBtn.Click += (_, _) => { rule.ZmanAnchors.RemoveAt(idx); RebuildZmanPanel(); };
                    Grid.SetColumn(rmBtn, 1);
                    rowGrid.Children.Add(rmBtn);
                    zmanPanel.Children.Add(rowGrid);
                }
                var addBtn = new Button { Content = "+ הוסף עוגן הלכתי", HorizontalAlignment = HorizontalAlignment.Stretch };
                addBtn.Click += (_, _) =>
                {
                    rule.ZmanAnchors.Add(new ZmanimAnchor { Zman = ZmanimKey.Sunrise });
                    RebuildZmanPanel();
                };
                zmanPanel.Children.Add(addBtn);
            }
            RebuildZmanPanel();
            sp.Children.Add(zmanPanel);

            void ApplyAnchorVisibility()
            {
                bool isZman = rule.AnchorKind == ReminderAnchorKind.Zman;
                bool isAbs = rule.AnchorKind == ReminderAnchorKind.AbsoluteDateTime;
                offsetInput.Visibility = isAbs ? Visibility.Collapsed : Visibility.Visible;
                zmanPanel.Visibility = isZman ? Visibility.Visible : Visibility.Collapsed;
                combineCombo.Visibility = isZman ? Visibility.Visible : Visibility.Collapsed;
                absGrid.Visibility = isAbs ? Visibility.Visible : Visibility.Collapsed;
            }
            anchorCombo.SelectionChanged += (_, _) =>
            {
                var tag = (anchorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                rule.AnchorKind = tag switch
                {
                    "Zman" => ReminderAnchorKind.Zman,
                    "Absolute" => ReminderAnchorKind.AbsoluteDateTime,
                    _ => ReminderAnchorKind.FixedOffset
                };
                if (rule.AnchorKind == ReminderAnchorKind.AbsoluteDateTime && !rule.AbsoluteWhen.HasValue)
                    UpdateAbsolute();
                ApplyAnchorVisibility();
            };
            ApplyAnchorVisibility();

            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Child = sp
            };
        }

        private static ComboBox BuildZmanCombo(ZmanimKey current, Action<ZmanimKey> onChange)
        {
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            int selected = 0;
            int i = 0;
            foreach (ZmanimKey k in Enum.GetValues<ZmanimKey>())
            {
                combo.Items.Add(new ComboBoxItem { Content = ReminderScheduler.GetZmanLabel(k), Tag = k });
                if (k == current) selected = i;
                i++;
            }
            combo.SelectedIndex = selected;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ComboBoxItem item && item.Tag is ZmanimKey k)
                    onChange(k);
            };
            return combo;
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────

        private DaysOfWeek CollectWeekdayMask()
        {
            DaysOfWeek mask = DaysOfWeek.None;
            if (ChkSun.IsChecked == true) mask |= DaysOfWeek.Sunday;
            if (ChkMon.IsChecked == true) mask |= DaysOfWeek.Monday;
            if (ChkTue.IsChecked == true) mask |= DaysOfWeek.Tuesday;
            if (ChkWed.IsChecked == true) mask |= DaysOfWeek.Wednesday;
            if (ChkThu.IsChecked == true) mask |= DaysOfWeek.Thursday;
            if (ChkFri.IsChecked == true) mask |= DaysOfWeek.Friday;
            if (ChkSat.IsChecked == true) mask |= DaysOfWeek.Saturday;
            return mask;
        }

        private void ApplyWeekdayMask(DaysOfWeek mask)
        {
            ChkSun.IsChecked = mask.HasFlag(DaysOfWeek.Sunday);
            ChkMon.IsChecked = mask.HasFlag(DaysOfWeek.Monday);
            ChkTue.IsChecked = mask.HasFlag(DaysOfWeek.Tuesday);
            ChkWed.IsChecked = mask.HasFlag(DaysOfWeek.Wednesday);
            ChkThu.IsChecked = mask.HasFlag(DaysOfWeek.Thursday);
            ChkFri.IsChecked = mask.HasFlag(DaysOfWeek.Friday);
            ChkSat.IsChecked = mask.HasFlag(DaysOfWeek.Saturday);
        }

        private static void SelectByTag(ComboBox combo, object tag)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem ci && Equals(ci.Tag?.ToString(), tag?.ToString()))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static int? TagAsInt(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem ci && ci.Tag is int i) return i;
            return null;
        }
    }
}
