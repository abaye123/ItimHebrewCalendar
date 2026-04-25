using System.Text;

namespace ItimHebrewCalendar.Services
{
    public static class HebrewNumberFormatter
    {
        // ASCII apostrophe/quote to match hebcal-go's yearToHebrew output exactly,
        // so the tray tooltip and the in-app month picker render identically.
        private const char Geresh = '\'';
        private const char Gershayim = '"';

        public static string FormatYear(int year)
        {
            int n = year >= 5000 ? year - 5000 : year;
            var letters = FormatSubThousand(n);
            if (string.IsNullOrEmpty(letters)) return year.ToString();
            var formatted = InsertPunctuation(letters);
            if (year >= 5000) return "ה'" + formatted;
            return formatted;
        }

        public static string FormatDay(int day)
        {
            if (day < 1 || day > 999) return day.ToString();
            var letters = FormatSubThousand(day);
            if (string.IsNullOrEmpty(letters)) return day.ToString();
            return InsertPunctuation(letters);
        }

        private static string FormatSubThousand(int n)
        {
            if (n < 1 || n > 999) return "";

            var sb = new StringBuilder();

            int hundreds = n / 100;
            while (hundreds >= 4) { sb.Append('ת'); hundreds -= 4; }
            if (hundreds == 3) { sb.Append('ש'); hundreds = 0; }
            if (hundreds == 2) { sb.Append('ר'); hundreds = 0; }
            if (hundreds == 1) { sb.Append('ק'); hundreds = 0; }

            int rem = n % 100;
            // Traditional substitutions to avoid spelling Divine names.
            if (rem == 15) { sb.Append("טו"); return sb.ToString(); }
            if (rem == 16) { sb.Append("טז"); return sb.ToString(); }

            int tens = rem / 10;
            switch (tens)
            {
                case 9: sb.Append('צ'); break;
                case 8: sb.Append('פ'); break;
                case 7: sb.Append('ע'); break;
                case 6: sb.Append('ס'); break;
                case 5: sb.Append('נ'); break;
                case 4: sb.Append('מ'); break;
                case 3: sb.Append('ל'); break;
                case 2: sb.Append('כ'); break;
                case 1: sb.Append('י'); break;
            }

            int ones = rem % 10;
            switch (ones)
            {
                case 9: sb.Append('ט'); break;
                case 8: sb.Append('ח'); break;
                case 7: sb.Append('ז'); break;
                case 6: sb.Append('ו'); break;
                case 5: sb.Append('ה'); break;
                case 4: sb.Append('ד'); break;
                case 3: sb.Append('ג'); break;
                case 2: sb.Append('ב'); break;
                case 1: sb.Append('א'); break;
            }

            return sb.ToString();
        }

        private static string InsertPunctuation(string letters)
        {
            if (letters.Length == 1) return letters + Geresh;
            return letters[..^1] + Gershayim + letters[^1];
        }
    }
}
