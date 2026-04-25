namespace ItimHebrewCalendar.Services
{
    public static class OmerHelper
    {
        public static int? GetOmerDay(int hebMonth, int hebDay)
        {
            if (hebMonth == 1 && hebDay >= 16 && hebDay <= 30) return hebDay - 15;
            if (hebMonth == 2 && hebDay >= 1 && hebDay <= 29) return 15 + hebDay;
            if (hebMonth == 3 && hebDay >= 1 && hebDay <= 5) return 44 + hebDay;
            return null;
        }

        public static string FormatOmer(int hebMonth, int hebDay)
        {
            var day = GetOmerDay(hebMonth, hebDay);
            if (day == null) return "";
            return $"{HebrewNumberFormatter.FormatDay(day.Value)} בעומר";
        }
    }
}
