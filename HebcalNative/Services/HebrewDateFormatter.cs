using System;
using System.Globalization;

namespace ItimHebrewCalendar.Services
{
    // Single place that turns numeric Hebrew/Gregorian dates into display strings
    // according to the user's chosen format (HebrewDateFormatOptions). Every window
    // that shows a date routes through here so a setting change is reflected app-wide.
    public static class HebrewDateFormatter
    {
        private static readonly CultureInfo He = CultureInfo.GetCultureInfo("he-IL");

        public static string Day(int day, HebrewDateFormatOptions o)
            => HebrewNumberFormatter.FormatDay(day, o.UseGershayim);

        public static string Year(int year, HebrewDateFormatOptions o) => o.YearStyle switch
        {
            HebYearStyle.Numeric => year.ToString(),
            HebYearStyle.WithoutMillennium => HebrewNumberFormatter.FormatYear(year, o.UseGershayim, millennium: false),
            _ => HebrewNumberFormatter.FormatYear(year, o.UseGershayim, millennium: true),
        };

        // Transforms the canonical month name (as produced by hebcal, e.g. "חשוון")
        // to the requested spelling. Only Cheshvan actually varies.
        public static string Month(string baseName, HebrewDateFormatOptions o)
        {
            if (baseName is "חשוון" or "חשון" or "מרחשוון")
            {
                return o.MonthSpelling switch
                {
                    HebMonthSpelling.Haser => "חשון",
                    HebMonthSpelling.Marcheshvan => "מרחשוון",
                    _ => "חשוון",
                };
            }
            return baseName;
        }

        // Full Hebrew date, e.g. "כ\"ג בחשוון תשפ\"ו".
        public static string Full(int day, string baseMonthName, int year, HebrewDateFormatOptions o)
        {
            var prefix = o.UseBetPrefix ? "ב" : "";
            var comma = o.UseComma ? "," : "";
            return $"{Day(day, o)} {prefix}{Month(baseMonthName, o)}{comma} {Year(year, o)}";
        }

        // Month + year only, for calendar headers, e.g. "חשוון תשפ\"ו".
        public static string MonthYear(string baseMonthName, int year, HebrewDateFormatOptions o)
            => $"{Month(baseMonthName, o)} {Year(year, o)}";

        // Gregorian date in the requested style. May include the weekday.
        public static string Gregorian(DateTime d, HebrewDateFormatOptions o)
            => Gregorian(d, o.GregStyle);

        // Same, but never includes the weekday — for contexts that already show a
        // separate day-of-week label and would otherwise duplicate it.
        public static string GregorianNoWeekday(DateTime d, HebrewDateFormatOptions o)
            => Gregorian(d, o.GregStyle == GregDateStyle.LongWithWeekday ? GregDateStyle.Long : o.GregStyle);

        private static string Gregorian(DateTime d, GregDateStyle style) => style switch
        {
            GregDateStyle.LongWithWeekday => d.ToString("dddd, d בMMMM yyyy", He),
            GregDateStyle.Numeric => d.ToString("dd/MM/yyyy", He),
            GregDateStyle.Iso => d.ToString("yyyy-MM-dd", He),
            _ => d.ToString("d בMMMM yyyy", He),
        };
    }
}
