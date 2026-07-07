using System;
using System.Globalization;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    // Derives the Shabbat-specific entries shown inside the daily zmanim list:
    // candle lighting on Erev Shabbat (Friday) and Motzaei Shabbat (Saturday).
    // Both are computed from the day's own zmanim so they always match whichever
    // ZmanimSource (hebcal / KosherJava) produced the rest of the list.
    public static class ShabbatZmanim
    {
        public const string CandleLightingLabel = "🕯️ הדלקת נרות";
        public const string TzeitShabbatLabel = "✨ צאת השבת";

        // Erev Shabbat candle lighting = sunset minus the city's configured offset.
        // Returns "" when the day is not Friday or sunset is unavailable.
        public static string CandleLighting(DateTime date, ZmanimInfo z, LocationInfo loc)
        {
            if (date.DayOfWeek != DayOfWeek.Friday) return "";
            if (z == null || string.IsNullOrEmpty(z.Sunset)) return "";
            if (!TimeSpan.TryParse(z.Sunset, CultureInfo.InvariantCulture, out var sunset)) return "";
            var t = sunset - TimeSpan.FromMinutes(loc.CandleLightingMinutes);
            return t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        // Motzaei Shabbat = tzeit hakochavim (the same value used for havdalah).
        // Returns "" when the day is not Saturday or tzeit is unavailable.
        public static string TzeitShabbat(DateTime date, ZmanimInfo z)
        {
            if (date.DayOfWeek != DayOfWeek.Saturday) return "";
            return z?.Tzeit ?? "";
        }
    }
}
