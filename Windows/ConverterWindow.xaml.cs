using System;
using System.Collections.Generic;
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

            PopulateConverterDayCombo(allow30: true);

            try
            {
                var today = HebcalBridge.Convert(DateTime.Today);
                if (today != null)
                {
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

                    RefreshConverterDayCombo();
                    SelectDayCombo(today.HebDay);
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
            // The year text changed - the (year, month) pair may have flipped between
            // a 30-day and a 29-day month (Cheshvan / Kislev / Adar), so re-filter days.
            RefreshConverterDayCombo();
            ConvertHebToGreg();
        }

        private void HebInput_ChangedCombo(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            if (ReferenceEquals(sender, HebMonthCombo)) RefreshConverterDayCombo();
            ConvertHebToGreg();
        }

        private void PopulateConverterDayCombo(bool allow30)
        {
            int? prev = SelectedDayTag();
            int max = allow30 ? 30 : 29;

            // Save/restore so calls during the constructor don't release the
            // re-entrancy guard prematurely.
            bool wasInitializing = _initializing;
            _initializing = true;
            try
            {
                HebDayCombo.Items.Clear();
                for (int d = 1; d <= max; d++)
                    HebDayCombo.Items.Add(new ComboBoxItem { Content = HebrewNumberFormatter.FormatDay(d), Tag = d });

                if (prev.HasValue && prev.Value >= 1 && prev.Value <= max)
                    SelectDayCombo(prev.Value);
            }
            finally
            {
                _initializing = wasInitializing;
            }
        }

        private void RefreshConverterDayCombo()
        {
            int month = 0;
            if (HebMonthCombo.SelectedItem is ComboBoxItem item && item.Tag is int m) month = m;
            int? year = HebrewNumberParser.ParseYear(HebYearBox.Text);

            // No usable year yet - default to allowing all 30 days; conversion will validate.
            if (month < 1 || !year.HasValue)
            {
                PopulateConverterDayCombo(allow30: true);
                return;
            }
            PopulateConverterDayCombo(allow30: MonthHasDay30(year.Value, month));
        }

        private int? SelectedDayTag()
        {
            if (HebDayCombo.SelectedItem is ComboBoxItem ci && ci.Tag is int d) return d;
            return null;
        }

        private void SelectDayCombo(int day)
        {
            for (int i = 0; i < HebDayCombo.Items.Count; i++)
            {
                if (HebDayCombo.Items[i] is ComboBoxItem ci && ci.Tag is int d && d == day)
                {
                    HebDayCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static bool MonthHasDay30(int year, int month) =>
            EventEditorWindow.MonthHasDay30(year, month);

        private void ConvertGregToHeb()
        {
            try
            {
                ErrorBar.IsOpen = false;
                CopyStatus.Text = "";
                if (GregDatePicker.Date is not DateTimeOffset dto) return;

                var date = dto.DateTime;
                var heb = HebcalBridge.Convert(date);
                if (heb == null)
                {
                    ShowError("ההמרה נכשלה");
                    return;
                }

                var fmt = App.Settings.DateFormat;
                TxtResultPrimary.Text = HebrewDateFormatter.Full(heb.HebDay, heb.MonthName, heb.HebYear, fmt);
                TxtResultSecondary.Text = HebrewDateFormatter.Gregorian(date, fmt);
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
                CopyStatus.Text = "";

                int? dayTag = SelectedDayTag();
                if (dayTag == null) return; // user hasn't picked a day yet
                int day = dayTag.Value;
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
                TxtResultPrimary.Text = HebrewDateFormatter.GregorianNoWeekday(date, App.Settings.DateFormat);
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
            TxtResultPrimary.Text = "-";
            TxtResultSecondary.Text = "";
        }

        private void OnCopyPrimary(object sender, RoutedEventArgs e) => CopyToClipboard(TxtResultPrimary.Text);

        private void OnCopySecondary(object sender, RoutedEventArgs e) => CopyToClipboard(TxtResultSecondary.Text);

        private void OnCopyBoth(object sender, RoutedEventArgs e)
        {
            var both = $"{TxtResultPrimary.Text}\n{TxtResultSecondary.Text}".Trim();
            CopyToClipboard(both);
        }

        private void CopyToClipboard(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "-") return;
            try
            {
                var dp = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(text);
                global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                CopyStatus.Text = "הועתק ✓";
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ConverterWindow.Copy", ex);
            }
        }
    }
}
