namespace ItimHebrewCalendar.Services
{
    public static class HebrewNumberParser
    {
        public static int? Parse(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var trimmed = input.Trim();

            if (int.TryParse(trimmed, out var n)) return n > 0 ? n : null;

            int total = 0;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                int val = LetterValue(c);
                if (val == 0) continue;

                // A geresh after a letter signals thousands ONLY when more letters follow.
                // Trailing "א'" is an ordinal marker (= 1), not 1000.
                if (i + 1 < trimmed.Length
                    && IsGeresh(trimmed[i + 1])
                    && HasMoreLetters(trimmed, i + 2))
                {
                    total += val * 1000;
                    i++;
                }
                else
                {
                    total += val;
                }
            }

            return total > 0 ? total : null;
        }

        private static bool HasMoreLetters(string s, int from)
        {
            for (int j = from; j < s.Length; j++)
            {
                if (LetterValue(s[j]) > 0) return true;
            }
            return false;
        }

        // Short-form years (under 1000) get the implicit fifth millennium added.
        public static int? ParseYear(string? input)
        {
            var n = Parse(input);
            if (n == null) return null;
            if (n < 1000) return n + 5000;
            return n;
        }

        private static bool IsGeresh(char c) =>
            c == '\'' || c == '׳' || c == '‘' || c == '’';

        private static int LetterValue(char c) => c switch
        {
            'א' => 1, 'ב' => 2, 'ג' => 3, 'ד' => 4, 'ה' => 5,
            'ו' => 6, 'ז' => 7, 'ח' => 8, 'ט' => 9,
            'י' => 10,
            'כ' or 'ך' => 20,
            'ל' => 30,
            'מ' or 'ם' => 40,
            'נ' or 'ן' => 50,
            'ס' => 60,
            'ע' => 70,
            'פ' or 'ף' => 80,
            'צ' or 'ץ' => 90,
            'ק' => 100,
            'ר' => 200,
            'ש' => 300,
            'ת' => 400,
            _ => 0
        };
    }
}
