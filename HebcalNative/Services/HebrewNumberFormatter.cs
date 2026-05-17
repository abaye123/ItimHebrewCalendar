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
            if (year < 1 || year > 9999) return year.ToString();

            if (year >= 5000)
            {
                var letters = FormatSubThousand(year - 5000);
                if (string.IsNullOrEmpty(letters)) return year.ToString();
                return "ה'" + InsertPunctuation(letters);
            }

            // Years 1000-4999 (e.g. 4856 = ד'תתנ"ו): single thousands letter,
            // then geresh, then the remainder formatted as a sub-thousand number.
            if (year >= 1000)
            {
                int thousands = year / 1000;
                int rest = year % 1000;
                var thousandsLetter = ThousandsLetter(thousands);
                if (rest == 0) return thousandsLetter + Geresh;
                var restLetters = FormatSubThousand(rest);
                if (string.IsNullOrEmpty(restLetters)) return year.ToString();
                return thousandsLetter + Geresh + InsertPunctuation(restLetters);
            }

            var sub = FormatSubThousand(year);
            if (string.IsNullOrEmpty(sub)) return year.ToString();
            return InsertPunctuation(sub);
        }

        private static string ThousandsLetter(int n) => n switch
        {
            1 => "א",
            2 => "ב",
            3 => "ג",
            4 => "ד",
            _ => n.ToString(),
        };

        // Inverse of FormatYear — parses Hebrew gematria-style strings back into
        // a year number. Accepts forms like:
        //   ה'תשפ"ו, התשפו, תשפ"ו   -> 5786
        //   ד'תתנ"ו, דתתנו           -> 4856
        //   תשפו (no millennium)     -> 786 (caller can disambiguate by range)
        // Returns null when the string contains no valid Hebrew letters or
        // is structured in a way we don't understand.
        public static int? ParseHebrewYear(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // Strip punctuation, whitespace, and the ASCII apostrophe/quote so
            // user input like ה'תשפ"ו and התשפו both collapse to "התשפו".
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (c == Geresh || c == Gershayim || c == '׳' || c == '״') continue;
                if (char.IsWhiteSpace(c)) continue;
                sb.Append(c);
            }
            var clean = sb.ToString();
            if (clean.Length == 0) return null;

            // Leading "ה" before more letters is the standard 5000-millennium marker.
            if (clean.Length > 1 && clean[0] == 'ה')
            {
                var rest = ParseSimpleHebrewNumber(clean[1..]);
                if (rest.HasValue) return 5000 + rest.Value;
            }

            // For pre-5000 years like "דתתנו" (4856): first letter (value 1-9)
            // acts as the thousands digit when followed by more letters whose
            // sum is non-zero. "א" alone stays 1, not 1000.
            if (clean.Length >= 2)
            {
                var firstValue = LetterValue(clean[0]);
                if (firstValue is >= 1 and <= 9)
                {
                    var restValue = ParseSimpleHebrewNumber(clean[1..]);
                    if (restValue.HasValue && restValue.Value > 0)
                        return firstValue * 1000 + restValue.Value;
                }
            }

            return ParseSimpleHebrewNumber(clean);
        }

        // Sums the gematria values of a letter sequence, honoring the
        // traditional טו=15 / טז=16 substitutions so they don't decompose into
        // letter pairs that would imply Divine names.
        private static int? ParseSimpleHebrewNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int total = 0;
            int i = 0;
            while (i < s.Length)
            {
                if (i + 1 < s.Length && s[i] == 'ט')
                {
                    if (s[i + 1] == 'ו') { total += 15; i += 2; continue; }
                    if (s[i + 1] == 'ז') { total += 16; i += 2; continue; }
                }
                var v = LetterValue(s[i]);
                if (v == 0) return null;
                total += v;
                i++;
            }
            return total > 0 ? total : null;
        }

        private static int LetterValue(char c) => c switch
        {
            'א' => 1, 'ב' => 2, 'ג' => 3, 'ד' => 4, 'ה' => 5,
            'ו' => 6, 'ז' => 7, 'ח' => 8, 'ט' => 9,
            'י' => 10, 'כ' => 20, 'ך' => 20, 'ל' => 30,
            'מ' => 40, 'ם' => 40, 'נ' => 50, 'ן' => 50,
            'ס' => 60, 'ע' => 70, 'פ' => 80, 'ף' => 80,
            'צ' => 90, 'ץ' => 90, 'ק' => 100, 'ר' => 200,
            'ש' => 300, 'ת' => 400,
            _ => 0,
        };

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
