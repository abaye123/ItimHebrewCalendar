using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows.Controls
{
    public sealed partial class HebrewMonthYearPicker : UserControl
    {
        public sealed class MonthItem
        {
            public int Value { get; init; }
            public string Name { get; init; } = "";
        }

        // ToString() drives both the AutoSuggestBox dropdown rendering and the
        // text shown after a selection (UpdateTextOnSelect).
        public sealed class YearItem
        {
            public int Value { get; init; }
            public string Name { get; init; } = "";
            // Same as Name but stripped of geresh/gershayim and the "ה'" prefix so
            // user input like "תשפו" or "תשפ\"ו" both match a single entry.
            public string SearchKey { get; init; } = "";
            public override string ToString() => Name;
        }

        // 100 years back, 30 forward - covers any realistic recall while keeping
        // the dropdown list manageable.
        private const int YearSpanBack = 100;
        private const int YearSpanForward = 30;

        public event EventHandler<(int Year, int Month, int? Day)>? Confirmed;
        public event EventHandler? TodayRequested;

        private List<YearItem> _allYears = new();

        public HebrewMonthYearPicker()
        {
            InitializeComponent();
        }

        public void SetCurrent(int hebYear, int hebMonth)
        {
            BuildYears(hebYear);
            SelectYear(hebYear);
            BuildMonths(hebYear, hebMonth);
            NbDay.Text = ""; // day is intentionally cleared on every open
        }

        private void BuildYears(int center)
        {
            var items = new List<YearItem>(YearSpanBack + YearSpanForward + 1);
            for (int y = center - YearSpanBack; y <= center + YearSpanForward; y++)
            {
                var name = HebrewNumberFormatter.FormatYear(y);
                items.Add(new YearItem
                {
                    Value = y,
                    Name = name,
                    SearchKey = NormalizeHebrew(name),
                });
            }
            // Most recent years near the cursor - show in reverse chronological order
            // so the current year sits at the top of the dropdown.
            items.Reverse();
            _allYears = items;
            TxtYear.ItemsSource = _allYears;
        }

        private void SelectYear(int year)
        {
            var match = _allYears.FirstOrDefault(y => y.Value == year);
            if (match != null) TxtYear.Text = match.Name;
        }

        // Calendar-order months: Tishrei -> Elul. hebcal numbering uses
        // 1=Nisan ... 6=Elul, 7=Tishrei ... 12=Adar (Adar I in leap), 13=Adar II.
        private void BuildMonths(int hebYear, int selectMonth)
        {
            bool leap = IsHebrewLeapYear(hebYear);
            var items = new List<MonthItem>(13);

            AddMonth(items, 7,  "תשרי");
            AddMonth(items, 8,  "חשוון");
            AddMonth(items, 9,  "כסלו");
            AddMonth(items, 10, "טבת");
            AddMonth(items, 11, "שבט");
            if (leap)
            {
                AddMonth(items, 12, "אדר א'");
                AddMonth(items, 13, "אדר ב'");
            }
            else
            {
                AddMonth(items, 12, "אדר");
            }
            AddMonth(items, 1, "ניסן");
            AddMonth(items, 2, "אייר");
            AddMonth(items, 3, "סיון");
            AddMonth(items, 4, "תמוז");
            AddMonth(items, 5, "אב");
            AddMonth(items, 6, "אלול");

            CmbMonth.ItemsSource = items;

            // If switching to a non-leap year while Adar II was selected, fall
            // back to regular Adar so the selection stays inside the new list.
            int target = !leap && selectMonth == 13 ? 12 : selectMonth;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Value == target)
                {
                    CmbMonth.SelectedIndex = i;
                    return;
                }
            }
            if (items.Count > 0) CmbMonth.SelectedIndex = 0;
        }

        private static void AddMonth(List<MonthItem> items, int value, string name) =>
            items.Add(new MonthItem { Value = value, Name = name });

        private static bool IsHebrewLeapYear(int year) => (7 * year + 1) % 19 < 7;

        // Strips geresh/gershayim and the "ה'" millennium prefix so user input
        // can be compared against year names without worrying about punctuation.
        private static string NormalizeHebrew(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var chars = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (c == '\'' || c == '"' || c == '׳' || c == '״') continue;
                chars.Append(c);
            }
            var stripped = chars.ToString();
            // "ה'תשפ"ו" -> "התשפו" after punctuation removal; drop the leading
            // millennium ה so users can search for just "תשפו".
            if (stripped.StartsWith('ה') && stripped.Length > 1) stripped = stripped[1..];
            return stripped;
        }

        private void OnYearTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            var query = sender.Text?.Trim() ?? "";
            if (query.Length == 0)
            {
                sender.ItemsSource = _allYears;
                return;
            }

            // Numeric input matches the absolute year (e.g. "5786" or "786").
            if (int.TryParse(query, out var numeric))
            {
                var byNumber = _allYears
                    .Where(y => y.Value.ToString().Contains(query))
                    .ToList();
                if (byNumber.Count > 0)
                {
                    sender.ItemsSource = byNumber;
                    return;
                }
            }

            var normalized = NormalizeHebrew(query);
            sender.ItemsSource = _allYears
                .Where(y => y.SearchKey.Contains(normalized))
                .ToList();
        }

        private void OnYearSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is YearItem y) sender.Text = y.Name;
        }

        private void OnYearQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // Pressing Enter inside the year box should also commit the whole picker.
            OnGoClick(sender, new RoutedEventArgs());
        }

        private void OnYearGotFocus(object sender, RoutedEventArgs e)
        {
            // Show the full list when the user clicks/tabs into the box, even if
            // the text matches an existing entry exactly (which would otherwise
            // suppress suggestions).
            if (sender is AutoSuggestBox box) box.IsSuggestionListOpen = true;
        }

        // Resolves whatever sits in TxtYear.Text - selected item, raw numeric,
        // or any Hebrew gematria string - to a concrete Hebrew year integer.
        // Falls back to ParseHebrewYear so historical years that aren't in the
        // ±100 dropdown (e.g. "דתתנו" / 4856) can still be navigated to.
        private int? ResolveYear()
        {
            var text = TxtYear.Text?.Trim() ?? "";
            if (text.Length == 0) return null;

            // Exact match against the dropdown - fastest path for the common case.
            var match = _allYears.FirstOrDefault(y => string.Equals(y.Name, text, StringComparison.Ordinal));
            if (match != null) return match.Value;

            // Raw integer like "5786" or "4856".
            if (int.TryParse(text, out var n) && n >= 1 && n <= 9999) return n;

            // Fuzzy match against punctuation-stripped names already in the list.
            var normalized = NormalizeHebrew(text);
            var fuzzy = _allYears.FirstOrDefault(y => y.SearchKey == normalized);
            if (fuzzy != null) return fuzzy.Value;

            // Last resort: parse arbitrary Hebrew gematria so historical years
            // outside the dropdown range still work.
            return HebrewNumberFormatter.ParseHebrewYear(text);
        }

        private int? ResolveDay()
        {
            var raw = NbDay.Text?.Trim() ?? "";
            if (raw.Length == 0) return null;
            if (!int.TryParse(raw, out var d)) return null;
            if (d < 1 || d > 30) return null;
            return d;
        }

        private void OnGoClick(object sender, RoutedEventArgs e)
        {
            if (CmbMonth.SelectedItem is not MonthItem m) return;
            var year = ResolveYear();
            if (!year.HasValue) return;
            Confirmed?.Invoke(this, (year.Value, m.Value, ResolveDay()));
        }

        private void OnTodayClick(object sender, RoutedEventArgs e)
        {
            TodayRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
