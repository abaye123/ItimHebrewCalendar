using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    public sealed partial class ConverterWindow : Window
    {
        private BackdropHandles? _backdrop;
        private bool _initializing = true;

        // Index 0 is unused; months are 1-based to match hebcal-go's HMonth values.
        private static readonly string[] HebMonthNames = new[]
        {
            "", "ניסן", "אייר", "סיון", "תמוז", "אב", "אלול",
            "תשרי", "חשוון", "כסלו", "טבת", "שבט", "אדר", "אדר ב'"
        };

        public ConverterWindow()
        {
            InitializeComponent();

            WindowHelpers.LoadAppIconInto(TitleBarIcon);

            Title = "ממיר תאריכים - עיתים";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;

            ThemeHelper.EnableRtlCaptionButtons(this);
            WindowHelpers.SetupCustomTitleBar(this, AppTitleBar);
            _backdrop = WindowHelpers.TrySetBackdrop(this);
            ThemeHelper.Apply(this, App.Settings.Theme, _backdrop.Config);

            WindowHelpers.Resize(this, 540, 600);
            WindowHelpers.CenterOnScreen(this);

            GregDatePicker.Date = DateTime.Today;

            HebMonthCombo.Items.Clear();
            for (int i = 1; i <= 13; i++)
            {
                HebMonthCombo.Items.Add(new ComboBoxItem
                {
                    Content = HebMonthNames[i],
                    Tag = i
                });
            }

            try
            {
                var today = HebcalBridge.Convert(DateTime.Today);
                if (today != null)
                {
                    HebDayBox.Text = HebrewNumberFormatter.FormatDay(today.HebDay);
                    HebYearBox.Text = HebrewNumberFormatter.FormatYear(today.HebYear);
                    for (int i = 0; i < HebMonthCombo.Items.Count; i++)
                    {
                        if (HebMonthCombo.Items[i] is ComboBoxItem item
                            && item.Tag is int m && m == today.HebMonth)
                        {
                            HebMonthCombo.SelectedIndex = i;
                            break;
                        }
                    }
                    if (HebMonthCombo.SelectedIndex < 0) HebMonthCombo.SelectedIndex = 6;
                }
                else
                {
                    HebMonthCombo.SelectedIndex = 6;
                }
            }
            catch
            {
                HebMonthCombo.SelectedIndex = 6;
            }

            _initializing = false;
            ConvertGregToHeb();

            Closed += (_, _) =>
            {
                _backdrop?.Dispose();
                _backdrop = null;
            };
        }

        private void Direction_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            if (RadioGregToHeb.IsChecked == true)
            {
                GregInputPanel.Visibility = Visibility.Visible;
                HebInputPanel.Visibility = Visibility.Collapsed;
                ConvertGregToHeb();
            }
            else
            {
                GregInputPanel.Visibility = Visibility.Collapsed;
                HebInputPanel.Visibility = Visibility.Visible;
                ConvertHebToGreg();
            }
        }

        private void GregDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (_initializing) return;
            ConvertGregToHeb();
        }

        private void HebInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_initializing) return;
            ConvertHebToGreg();
        }

        private void HebInput_ChangedCombo(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            ConvertHebToGreg();
        }

        private void ConvertGregToHeb()
        {
            try
            {
                ErrorBar.IsOpen = false;
                if (GregDatePicker.Date is not DateTimeOffset dto) return;

                var date = dto.DateTime;
                var heb = HebcalBridge.Convert(date);
                if (heb == null)
                {
                    ShowError("ההמרה נכשלה");
                    return;
                }

                TxtResultPrimary.Text = heb.Render;
                TxtResultSecondary.Text = date.ToString("dddd, d בMMMM yyyy",
                    CultureInfo.GetCultureInfo("he-IL"));
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ConvertHebToGreg()
        {
            try
            {
                ErrorBar.IsOpen = false;

                var dayParsed = HebrewNumberParser.Parse(HebDayBox.Text);
                if (dayParsed == null) return; // stay silent while the user is typing
                int day = dayParsed.Value;
                var yearParsed = HebrewNumberParser.ParseYear(HebYearBox.Text);
                if (yearParsed == null) return;
                int year = yearParsed.Value;
                int month = 0;
                if (HebMonthCombo.SelectedItem is ComboBoxItem item && item.Tag is int m)
                    month = m;

                if (month < 1 || day < 1 || day > 30 || year < 5000)
                {
                    ShowError("ערכים לא תקינים");
                    return;
                }

                var greg = HebcalBridge.ConvertFromHebrew(year, month, day);
                if (greg == null)
                {
                    ShowError("ההמרה נכשלה");
                    return;
                }

                var date = new DateTime(greg.Year, greg.Month, greg.Day);
                TxtResultPrimary.Text = date.ToString("d בMMMM yyyy",
                    CultureInfo.GetCultureInfo("he-IL"));
                TxtResultSecondary.Text = date.ToString("dddd", CultureInfo.GetCultureInfo("he-IL"));
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ShowError(string msg)
        {
            ErrorBar.Message = msg;
            ErrorBar.IsOpen = true;
            TxtResultPrimary.Text = "—";
            TxtResultSecondary.Text = "";
        }
    }
}
