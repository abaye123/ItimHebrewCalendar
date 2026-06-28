using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ItimHebrewCalendar.Models;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public sealed partial class SettingsWindow : Window
    {
        private BackdropHandles? _backdrop;
        private readonly AppSettings _workingCopy;
        private UpdateChecker.ReleaseInfo? _pendingUpdate;

        private readonly List<(ZmanimDisplay Flag, CheckBox Check)> _zmanimChecks = new();

        private record ZmanimOption(ZmanimDisplay Flag, string Name);

        private static readonly List<ZmanimOption> AllZmanim = new()
        {
            new(ZmanimDisplay.AlotHaShachar, "עלות השחר (72 דקות)"),
            new(ZmanimDisplay.Misheyakir, "משיכיר"),
            new(ZmanimDisplay.Sunrise, "הנץ החמה"),
            new(ZmanimDisplay.SofZmanShmaMGA, "סוף זמן ק\"ש (מג\"א)"),
            new(ZmanimDisplay.SofZmanShma, "סוף זמן ק\"ש (גר\"א)"),
            new(ZmanimDisplay.SofZmanTfillaMGA, "סוף זמן תפילה (מג\"א)"),
            new(ZmanimDisplay.SofZmanTfilla, "סוף זמן תפילה (גר\"א)"),
            new(ZmanimDisplay.Chatzot, "חצות"),
            new(ZmanimDisplay.MinchaGedola, "מנחה גדולה"),
            new(ZmanimDisplay.MinchaKetana, "מנחה קטנה"),
            new(ZmanimDisplay.PlagHaMincha, "פלג המנחה"),
            new(ZmanimDisplay.Sunset, "שקיעה"),
            new(ZmanimDisplay.Tzeit, "צאת הכוכבים"),
            new(ZmanimDisplay.Tzeit72, "צאת הכוכבים (ר\"ת, 72 דקות)"),
        };

        public SettingsWindow(bool focusAbout = false)
        {
            InitializeComponent();

            WindowHelpers.LoadAppIconInto(TitleBarIcon);
            WindowHelpers.LoadAppIconInto(HeaderIcon);
            LoadAbayeLogo();

            // Working copy so Cancel reverts to the saved state.
            _workingCopy = CloneSettings(App.Settings);

            Title = "הגדרות - עיתים";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);

            WindowHelpers.Resize(this, 640, 800);
            WindowHelpers.CenterOnScreen(this);

            if (WindowHelpers.GetAppWindow(this)?.Presenter is OverlappedPresenter op)
            {
                op.IsMaximizable = true;
                op.IsMinimizable = true;
                op.IsResizable = true;
            }

            PopulateControls();
            ApplyCurrentTheme();

            if (focusAbout)
            {
                AppearanceExpander.IsExpanded = false;
                LocationExpander.IsExpanded = false;
                HolidaysExpander.IsExpanded = false;
                AboutExpander.IsExpanded = true;
                AboutExpander.Loaded += (_, _) => AboutExpander.StartBringIntoView();
            }

            Closed += (_, _) =>
            {
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        private static AppSettings CloneSettings(AppSettings original) => new()
        {
            CityName = original.CityName,
            UseIsraeliHolidays = original.UseIsraeliHolidays,
            CandleLightingMinutes = original.CandleLightingMinutes,
            ZmanimToShow = original.ZmanimToShow,
            Theme = original.Theme,
            ShowGregorianInCalendar = original.ShowGregorianInCalendar,
            StartWithWindows = original.StartWithWindows,
            ShowHebrewDateInTray = original.ShowHebrewDateInTray,
            TrayIconStyle = original.TrayIconStyle,
            CloseTrayPopupOnFocusLoss = original.CloseTrayPopupOnFocusLoss,
            ShowModernHolidays = original.ShowModernHolidays,
            UseSunsetDateTransition = original.UseSunsetDateTransition,
            ZmanimSource = original.ZmanimSource,
            ShowSecondTempleTimer = original.ShowSecondTempleTimer,
            DefaultMainView = original.DefaultMainView,
            DefaultTrayView = original.DefaultTrayView,
            StandaloneZmanReminders = original.StandaloneZmanReminders
                .Select(r => new Models.StandaloneZmanReminder
                {
                    Id = r.Id,
                    Label = r.Label,
                    Zman = r.Zman,
                    OffsetMinutes = r.OffsetMinutes,
                    ActiveDays = r.ActiveDays,
                    SkipShabbatYomTov = r.SkipShabbatYomTov,
                    Enabled = r.Enabled
                }).ToList(),
            WindowsCalendarSyncEnabled = original.WindowsCalendarSyncEnabled,
            IcsExportMonthsAhead = original.IcsExportMonthsAhead,
            MissedReminderLookbackHours = original.MissedReminderLookbackHours,
            AutoCheckUpdates = original.AutoCheckUpdates,
            LastUpdateCheckUtc = original.LastUpdateCheckUtc,
            DateFormat = new HebrewDateFormatOptions
            {
                UseGershayim = original.DateFormat.UseGershayim,
                UseBetPrefix = original.DateFormat.UseBetPrefix,
                UseComma = original.DateFormat.UseComma,
                MonthSpelling = original.DateFormat.MonthSpelling,
                YearStyle = original.DateFormat.YearStyle,
                GregStyle = original.DateFormat.GregStyle,
            },
        };

        private void PopulateControls()
        {
            for (int i = 0; i < ThemeCombo.Items.Count; i++)
            {
                if (ThemeCombo.Items[i] is ComboBoxItem c && (string)c.Tag! == _workingCopy.Theme.ToString())
                {
                    ThemeCombo.SelectedIndex = i;
                    break;
                }
            }
            if (ThemeCombo.SelectedIndex < 0) ThemeCombo.SelectedIndex = 0;

            CityCombo.Items.Clear();
            foreach (var city in CitiesDatabase.Cities)
            {
                var item = new ComboBoxItem { Content = city.Name, Tag = city.Name };
                CityCombo.Items.Add(item);
            }
            for (int i = 0; i < CityCombo.Items.Count; i++)
            {
                if (CityCombo.Items[i] is ComboBoxItem c && (string)c.Tag! == _workingCopy.CityName)
                {
                    CityCombo.SelectedIndex = i;
                    break;
                }
            }
            if (CityCombo.SelectedIndex < 0) CityCombo.SelectedIndex = 0;

            IsraelToggle.IsOn = _workingCopy.UseIsraeliHolidays;
            ShowModernToggle.IsOn = _workingCopy.ShowModernHolidays;
            SunsetTransitionToggle.IsOn = _workingCopy.UseSunsetDateTransition;
            CandleMinutesBox.Value = _workingCopy.CandleLightingMinutes;

            for (int i = 0; i < ZmanimSourceCombo.Items.Count; i++)
            {
                if (ZmanimSourceCombo.Items[i] is ComboBoxItem c
                    && (string)c.Tag! == _workingCopy.ZmanimSource.ToString())
                {
                    ZmanimSourceCombo.SelectedIndex = i;
                    break;
                }
            }
            if (ZmanimSourceCombo.SelectedIndex < 0) ZmanimSourceCombo.SelectedIndex = 0;

            ShowGregToggle.IsOn = _workingCopy.ShowGregorianInCalendar;
            ShowHebInTrayToggle.IsOn = _workingCopy.ShowHebrewDateInTray;

            for (int i = 0; i < TrayStyleCombo.Items.Count; i++)
            {
                if (TrayStyleCombo.Items[i] is ComboBoxItem c
                    && (string)c.Tag! == _workingCopy.TrayIconStyle.ToString())
                {
                    TrayStyleCombo.SelectedIndex = i;
                    break;
                }
            }
            if (TrayStyleCombo.SelectedIndex < 0) TrayStyleCombo.SelectedIndex = 0;
            CloseOnFocusLossToggle.IsOn = _workingCopy.CloseTrayPopupOnFocusLoss;
            ShowTempleTimerToggle.IsOn = _workingCopy.ShowSecondTempleTimer;

            // Settings file is the source of truth; the Registry key is reconciled on Save.
            StartupToggle.IsOn = _workingCopy.StartWithWindows;

            ZmanimChecksPanel.Children.Clear();
            _zmanimChecks.Clear();
            foreach (var opt in AllZmanim)
            {
                var cb = new CheckBox
                {
                    Content = opt.Name,
                    IsChecked = (_workingCopy.ZmanimToShow & opt.Flag) != 0
                };
                _zmanimChecks.Add((opt.Flag, cb));
                ZmanimChecksPanel.Children.Add(cb);
            }

            GershayimToggle.IsOn = _workingCopy.DateFormat.UseGershayim;
            BetPrefixToggle.IsOn = _workingCopy.DateFormat.UseBetPrefix;
            CommaToggle.IsOn = _workingCopy.DateFormat.UseComma;
            SelectComboByTag(MonthSpellingCombo, _workingCopy.DateFormat.MonthSpelling.ToString());
            SelectComboByTag(YearStyleCombo, _workingCopy.DateFormat.YearStyle.ToString());
            SelectComboByTag(GregStyleCombo, _workingCopy.DateFormat.GregStyle.ToString());
            RefreshDateFormatPreview();

            SelectComboByTag(DefaultMainViewCombo, _workingCopy.DefaultMainView.ToString());
            SelectComboByTag(DefaultTrayViewCombo, _workingCopy.DefaultTrayView.ToString());
            WindowsCalendarSyncToggle.IsOn = _workingCopy.WindowsCalendarSyncEnabled;
            AutoCheckUpdatesToggle.IsOn = _workingCopy.AutoCheckUpdates;
            RebuildStandaloneRemindersUi();

            try
            {
                var asm = typeof(SettingsWindow).Assembly;
                var version = asm.GetName().Version;
                AboutVersionText.Text = version != null
                    ? $"גרסה {version.Major}.{version.Minor}.{version.Build}"
                    : "גרסה 1.7.0";
            }
            catch
            {
                AboutVersionText.Text = "גרסה 1.7.0";
            }

            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png");
                if (File.Exists(iconPath))
                {
                    AboutIcon.Source = new BitmapImage(new Uri(iconPath));
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SettingsWindow.LoadAboutIcon", ex);
            }

            SettingsPathText.Text = $"קובץ הגדרות: {SettingsManager.GetSettingsPath()}";
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyCurrentTheme();
        }

        private AppTheme GetSelectedTheme()
        {
            if (ThemeCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag
                && Enum.TryParse<AppTheme>(tag, out var theme))
            {
                return theme;
            }
            return AppTheme.System;
        }

        private void ApplyCurrentTheme()
        {
            ThemeHelper.Apply(this, GetSelectedTheme(), _backdrop?.Config);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (CityCombo.SelectedItem is ComboBoxItem cityItem && cityItem.Tag is string cityName)
                _workingCopy.CityName = cityName;

            _workingCopy.UseIsraeliHolidays = IsraelToggle.IsOn;
            _workingCopy.ShowModernHolidays = ShowModernToggle.IsOn;
            _workingCopy.UseSunsetDateTransition = SunsetTransitionToggle.IsOn;
            _workingCopy.CandleLightingMinutes = (int)CandleMinutesBox.Value;

            if (ZmanimSourceCombo.SelectedItem is ComboBoxItem zsi
                && zsi.Tag is string zsTag
                && Enum.TryParse<ZmanimSource>(zsTag, out var zs))
            {
                _workingCopy.ZmanimSource = zs;
            }
            _workingCopy.ShowGregorianInCalendar = ShowGregToggle.IsOn;
            _workingCopy.ShowHebrewDateInTray = ShowHebInTrayToggle.IsOn;

            if (TrayStyleCombo.SelectedItem is ComboBoxItem tsi
                && tsi.Tag is string tsTag
                && Enum.TryParse<TrayIconStyle>(tsTag, out var ts))
            {
                _workingCopy.TrayIconStyle = ts;
            }
            _workingCopy.CloseTrayPopupOnFocusLoss = CloseOnFocusLossToggle.IsOn;
            _workingCopy.ShowSecondTempleTimer = ShowTempleTimerToggle.IsOn;
            _workingCopy.Theme = GetSelectedTheme();
            _workingCopy.StartWithWindows = StartupToggle.IsOn;

            ZmanimDisplay flags = ZmanimDisplay.None;
            foreach (var (flag, cb) in _zmanimChecks)
            {
                if (cb.IsChecked == true) flags |= flag;
            }
            _workingCopy.ZmanimToShow = flags;

            if (DefaultMainViewCombo.SelectedItem is ComboBoxItem mvi
                && mvi.Tag is string mvTag
                && Enum.TryParse<CalendarViewMode>(mvTag, out var mv))
                _workingCopy.DefaultMainView = mv;

            if (DefaultTrayViewCombo.SelectedItem is ComboBoxItem tvi
                && tvi.Tag is string tvTag
                && Enum.TryParse<CalendarViewMode>(tvTag, out var tv))
                _workingCopy.DefaultTrayView = tv;

            _workingCopy.WindowsCalendarSyncEnabled = WindowsCalendarSyncToggle.IsOn;
            _workingCopy.AutoCheckUpdates = AutoCheckUpdatesToggle.IsOn;
            _workingCopy.DateFormat = ReadDateFormatFromControls();

            SettingsManager.Save(_workingCopy);
            App.Settings = _workingCopy;
            ReminderHostService.OnSettingsChanged();
            UpdateCheckScheduler.OnSettingsChanged();
            StartupHelper.SetEnabled(_workingCopy.StartWithWindows);

            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        // ─── Date format ───────────────────────────────────────────────────────────

        private HebrewDateFormatOptions ReadDateFormatFromControls()
        {
            var o = new HebrewDateFormatOptions
            {
                UseGershayim = GershayimToggle.IsOn,
                UseBetPrefix = BetPrefixToggle.IsOn,
                UseComma = CommaToggle.IsOn,
            };
            if (MonthSpellingCombo.SelectedItem is ComboBoxItem mi && mi.Tag is string mt
                && Enum.TryParse<HebMonthSpelling>(mt, out var ms)) o.MonthSpelling = ms;
            if (YearStyleCombo.SelectedItem is ComboBoxItem yi && yi.Tag is string yt
                && Enum.TryParse<HebYearStyle>(yt, out var ys)) o.YearStyle = ys;
            if (GregStyleCombo.SelectedItem is ComboBoxItem gi && gi.Tag is string gt
                && Enum.TryParse<GregDateStyle>(gt, out var gs)) o.GregStyle = gs;
            return o;
        }

        private void OnDateFormatChanged(object sender, RoutedEventArgs e) => RefreshDateFormatPreview();

        private void OnDateFormatComboChanged(object sender, SelectionChangedEventArgs e) => RefreshDateFormatPreview();

        private void RefreshDateFormatPreview()
        {
            // Controls may not all exist yet during initial XAML load.
            if (DateFormatPreview == null || GershayimToggle == null || MonthSpellingCombo == null) return;

            var o = ReadDateFormatFromControls();
            // Illustrative sample: 23 Cheshvan 5786, so the month-spelling option is visible.
            var heb = HebrewDateFormatter.Full(23, "חשוון", 5786, o);
            var greg = HebrewDateFormatter.Gregorian(new DateTime(2026, 11, 14), o);
            DateFormatPreview.Text = $"{heb}\n{greg}";
        }

        private void LoadAbayeLogo()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "abaye.png");
                if (File.Exists(path))
                {
                    AbayeLogo.Source = new BitmapImage(new Uri(path));
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SettingsWindow.LoadAbayeLogo", ex);
            }
        }

        // ─── Standalone zman reminders ─────────────────────────────────────────────

        private void OnAddStandaloneReminder(object sender, RoutedEventArgs e)
        {
            _workingCopy.StandaloneZmanReminders.Add(new StandaloneZmanReminder
            {
                Label = "",
                Zman = ZmanimKey.SofZmanShma,
                OffsetMinutes = -10,
                ActiveDays = DaysOfWeek.All,
                Enabled = true
            });
            RebuildStandaloneRemindersUi();
        }

        private void RebuildStandaloneRemindersUi()
        {
            StandaloneRemindersPanel.Children.Clear();
            for (int i = 0; i < _workingCopy.StandaloneZmanReminders.Count; i++)
            {
                int idx = i;
                StandaloneRemindersPanel.Children.Add(BuildStandaloneCard(_workingCopy.StandaloneZmanReminders[idx], idx));
            }
        }

        private Border BuildStandaloneCard(StandaloneZmanReminder r, int index)
        {
            var sp = new StackPanel { Spacing = 6 };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var enabledCheck = new CheckBox { Content = "פעיל", IsChecked = r.Enabled };
            enabledCheck.Checked   += (_, _) => r.Enabled = true;
            enabledCheck.Unchecked += (_, _) => r.Enabled = false;
            Grid.SetColumn(enabledCheck, 0);
            headerGrid.Children.Add(enabledCheck);
            var rmBtn = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(8, 4, 8, 4)
            };
            rmBtn.Click += (_, _) =>
            {
                _workingCopy.StandaloneZmanReminders.RemoveAt(index);
                RebuildStandaloneRemindersUi();
            };
            Grid.SetColumn(rmBtn, 1);
            headerGrid.Children.Add(rmBtn);
            sp.Children.Add(headerGrid);

            var labelBox = new TextBox
            {
                Header = "תווית (אופציונלי)",
                Text = r.Label,
                PlaceholderText = "למשל: סוף זמן ק\"ש"
            };
            labelBox.TextChanged += (_, _) => r.Label = labelBox.Text;
            sp.Children.Add(labelBox);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var zmanCombo = new ComboBox { Header = "זמן הלכתי", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 4, 0) };
            int sel = 0, i = 0;
            foreach (ZmanimKey k in Enum.GetValues<ZmanimKey>())
            {
                zmanCombo.Items.Add(new ComboBoxItem { Content = ReminderScheduler.GetZmanLabel(k), Tag = k });
                if (k == r.Zman) sel = i;
                i++;
            }
            zmanCombo.SelectedIndex = sel;
            zmanCombo.SelectionChanged += (_, _) =>
            {
                if (zmanCombo.SelectedItem is ComboBoxItem ci && ci.Tag is ZmanimKey k)
                    r.Zman = k;
            };
            Grid.SetColumn(zmanCombo, 0);
            grid.Children.Add(zmanCombo);

            var offsetBox = new NumberBox
            {
                Header = "היסט (דק')",
                Value = r.OffsetMinutes,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Margin = new Thickness(4, 0, 0, 0)
            };
            offsetBox.ValueChanged += (_, ev) =>
            {
                if (!double.IsNaN(ev.NewValue)) r.OffsetMinutes = (int)ev.NewValue;
            };
            Grid.SetColumn(offsetBox, 1);
            grid.Children.Add(offsetBox);
            sp.Children.Add(grid);

            var skipShabbat = new CheckBox { Content = "דלג בשבת ובחגים", IsChecked = r.SkipShabbatYomTov };
            skipShabbat.Checked   += (_, _) => r.SkipShabbatYomTov = true;
            skipShabbat.Unchecked += (_, _) => r.SkipShabbatYomTov = false;
            sp.Children.Add(skipShabbat);

            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Child = sp
            };
        }

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem ci && (ci.Tag as string) == tag)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        // ─── ICS placeholder handlers ──────────────────────────────────────────────

        private async void OnExportIcs(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = await IcsService.PickSaveAsync(this);
                if (string.IsNullOrEmpty(path)) return;
                var count = IcsService.Export(path, _workingCopy.IcsExportMonthsAhead, _workingCopy.GetEffectiveLocation());
                IcsStatusText.Text = $"יוצאו {count} אירועים אל {path}";
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SettingsWindow.OnExportIcs", ex);
                IcsStatusText.Text = "שגיאה בייצוא: " + ex.Message;
            }
        }

        private async void OnImportIcs(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = await IcsService.PickOpenAsync(this);
                if (string.IsNullOrEmpty(path)) return;
                var count = IcsService.Import(path);
                IcsStatusText.Text = $"יובאו {count} אירועים חדשים מ-{path}";
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SettingsWindow.OnImportIcs", ex);
                IcsStatusText.Text = "שגיאה ביבוא: " + ex.Message;
            }
        }

        private async void DetectLocationButton_Click(object sender, RoutedEventArgs e)
        {
            DetectLocationButton.IsEnabled = false;
            DetectLocationStatus.Text = "מזהה מיקום...";
            try
            {
                var result = await GeoDetection.DetectAsync();
                if (result == null)
                {
                    DetectLocationStatus.Text = "זיהוי נכשל - ודא ששירותי המיקום מופעלים ב-Windows ושההרשאה ניתנה לאפליקציה";
                    return;
                }

                for (int i = 0; i < CityCombo.Items.Count; i++)
                {
                    if (CityCombo.Items[i] is ComboBoxItem item
                        && item.Tag is string tag
                        && tag == result.ClosestMatch.Name)
                    {
                        CityCombo.SelectedIndex = i;
                        break;
                    }
                }

                DetectLocationStatus.Text = $"נבחרה {result.ClosestMatch.Name}";
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SettingsWindow.DetectLocation", ex);
                DetectLocationStatus.Text = "שגיאה בזיהוי";
            }
            finally
            {
                DetectLocationButton.IsEnabled = true;
            }
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            _pendingUpdate = null;
            UpdateStatusText.Text = "בודק עדכונים...";

            try
            {
                var release = await UpdateChecker.GetLatestReleaseAsync();
                var current = UpdateChecker.GetCurrentVersion();

                // Treat the manual click as the latest check, so the background
                // scheduler doesn't immediately re-check after the user just did.
                App.Settings.LastUpdateCheckUtc = DateTime.UtcNow;
                SettingsManager.Save(App.Settings);

                if (release.ParsedVersion == null)
                {
                    UpdateStatusText.Text = $"לא ניתן לפרש את גרסת ה-Release ({release.TagName}).";
                    return;
                }

                if (string.IsNullOrEmpty(release.AssetUrl))
                {
                    UpdateStatusText.Text = $"גרסה {release.ParsedVersion} זמינה, אך לא נמצא קובץ התקנה ב-Release.";
                    return;
                }

                if (UpdateChecker.IsNewer(current, release.ParsedVersion))
                {
                    var sizeMb = release.AssetSize / (1024.0 * 1024.0);
                    UpdateStatusText.Text =
                        $"זמינה גרסה חדשה: {release.ParsedVersion} (הנוכחית: {current}). " +
                        $"קובץ ההתקנה: {release.AssetName} ({sizeMb:0.0} MB).";
                    InstallUpdateButton.Visibility = Visibility.Visible;
                    _pendingUpdate = release;
                }
                else
                {
                    UpdateStatusText.Text = $"אתה משתמש בגרסה העדכנית ביותר ({current}).";
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SettingsWindow.CheckUpdates", ex);
                UpdateStatusText.Text = $"שגיאה בבדיקת עדכונים: {ex.Message}";
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
            }
        }

        private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            var confirm = new ContentDialog
            {
                Title = "התקנת עדכון",
                Content = $"גרסה {_pendingUpdate.ParsedVersion} תורד ותותקן. " +
                          "התוכנה תיסגר אוטומטית לפני ההתקנה. להמשיך?",
                PrimaryButtonText = "המשך",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            CheckUpdatesButton.IsEnabled = false;
            InstallUpdateButton.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;
            UpdateStatusText.Text = "מוריד את קובץ ההתקנה...";

            try
            {
                var progress = new Progress<double>(p =>
                {
                    UpdateProgressBar.Value = p;
                    UpdateStatusText.Text = $"מוריד... {p * 100:0}%";
                });

                var installerPath = await UpdateChecker.DownloadAssetAsync(
                    _pendingUpdate.AssetUrl, progress);

                UpdateStatusText.Text = "מפעיל את ההתקנה...";
                UpdateChecker.RunInstaller(installerPath, SilentUpdateToggle.IsOn);

                // Hard-exit so the installer can replace locked files AND so the
                // post-install relaunch (runasoriginaluser in the .iss [Run]
                // section) doesn't bounce off our single-instance mutex —
                // Application.Current.Exit() alone leaves the tray / event
                // listener / dispatcher keeping the process alive.
                try { App.Tray?.Dispose(); } catch { }
                try { Application.Current.Exit(); } catch { }
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SettingsWindow.InstallUpdate", ex);
                UpdateStatusText.Text = $"שגיאה: {ex.Message}";
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                CheckUpdatesButton.IsEnabled = true;
                InstallUpdateButton.IsEnabled = true;
            }
        }
    }
}
