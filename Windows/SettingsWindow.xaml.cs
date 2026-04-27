using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public sealed partial class SettingsWindow : Window
    {
        private BackdropHandles? _backdrop;
        private readonly AppSettings _workingCopy;

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
            CloseTrayPopupOnFocusLoss = original.CloseTrayPopupOnFocusLoss,
            ShowModernHolidays = original.ShowModernHolidays,
            UseSunsetDateTransition = original.UseSunsetDateTransition,
            ZmanimSource = original.ZmanimSource,
            ShowSecondTempleTimer = original.ShowSecondTempleTimer,
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

            try
            {
                var asm = typeof(SettingsWindow).Assembly;
                var version = asm.GetName().Version;
                AboutVersionText.Text = version != null
                    ? $"גרסה {version.Major}.{version.Minor}.{version.Build}"
                    : "גרסה 1.2.0";
            }
            catch
            {
                AboutVersionText.Text = "גרסה 1.2.0";
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

            SettingsManager.Save(_workingCopy);
            App.Settings = _workingCopy;
            StartupHelper.SetEnabled(_workingCopy.StartWithWindows);

            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

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
    }
}
