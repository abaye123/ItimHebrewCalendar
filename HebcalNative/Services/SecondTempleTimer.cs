using System;
using System.Collections.Generic;
using System.Linq;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    public record SecondTempleInterval(
        int Years, int Months, int Days,
        int Hours, int Minutes, int Seconds,
        int TotalDays);

    // Time elapsed since the destruction of the Second Temple (9 Av 3830 / 70 CE).
    // Algorithm based on https://github.com/kdroidFilter/SecondTempleTimerLibrary:
    // calendar diff in Hebrew years/months/days, plus hours/minutes/seconds since
    // the most recent sunset in Jerusalem (where the Hebrew day rolls over).
    public static class SecondTempleTimer
    {
        private const int RefYear = 3830;
        private const int RefMonth = 5; // Av
        private const int RefDay = 9;

        private static readonly LocationInfo Jerusalem = new()
        {
            Name = "Jerusalem",
            NameEn = "Jerusalem",
            Latitude = 31.7683,
            Longitude = 35.2137,
            Elevation = 800,
            TimeZone = "Asia/Jerusalem",
            IsInIsrael = true
        };

        private static DateTime? _destructionGregorian;

        private static DateTime DestructionGregorian
        {
            get
            {
                if (_destructionGregorian.HasValue) return _destructionGregorian.Value;
                try
                {
                    var g = HebcalBridge.ConvertFromHebrew(RefYear, RefMonth, RefDay);
                    if (g != null)
                    {
                        _destructionGregorian = new DateTime(g.Year, g.Month, g.Day);
                        return _destructionGregorian.Value;
                    }
                }
                catch { }
                _destructionGregorian = new DateTime(70, 8, 4);
                return _destructionGregorian.Value;
            }
        }

        public static SecondTempleInterval? Compute()
        {
            try
            {
                var now = DateTime.Now;
                var todaySunset = GetJerusalemSunset(now.Date);
                bool sunsetPassed = todaySunset.HasValue && now >= todaySunset.Value;

                var hebDate = sunsetPassed ? now.Date.AddDays(1) : now.Date;
                var heb = HebcalBridge.Convert(hebDate);
                if (heb == null) return null;

                int years = heb.HebYear - RefYear;
                int months = heb.HebMonth - RefMonth;
                int days = heb.HebDay - RefDay;

                if (days < 0)
                {
                    months -= 1;
                    int prevMonth = heb.HebMonth - 1;
                    int prevYear = heb.HebYear;
                    if (prevMonth < 1) { prevMonth = 12; prevYear--; }
                    days += DaysInHebrewMonth(prevYear, prevMonth);
                }

                if (months < 0)
                {
                    years -= 1;
                    months += 12;
                }

                TimeSpan diff;
                if (todaySunset.HasValue && now >= todaySunset.Value)
                {
                    diff = now - todaySunset.Value;
                }
                else if (todaySunset.HasValue)
                {
                    diff = now - todaySunset.Value.AddDays(-1);
                }
                else
                {
                    diff = TimeSpan.Zero;
                }

                int totalDays = (int)(hebDate - DestructionGregorian).TotalDays;

                return new SecondTempleInterval(
                    years, months, days,
                    (int)diff.Hours, diff.Minutes, diff.Seconds,
                    totalDays);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("SecondTempleTimer.Compute", ex);
                return null;
            }
        }

        private static DateTime? GetJerusalemSunset(DateTime date)
        {
            var z = ZmanimService.GetZmanim(date, Jerusalem);
            if (z == null || string.IsNullOrEmpty(z.Sunset)) return null;
            if (!TimeSpan.TryParse(z.Sunset, out var t)) return null;
            return date.Date.Add(t);
        }

        private static int DaysInHebrewMonth(int year, int month)
        {
            var greg30 = HebcalBridge.ConvertFromHebrew(year, month, 30);
            if (greg30 == null) return 29;
            var heb = HebcalBridge.Convert(new DateTime(greg30.Year, greg30.Month, greg30.Day));
            return (heb != null && heb.HebMonth == month) ? 30 : 29;
        }

        // "חלפו 1955 שנים, 9 חודשים ו-27 ימים מחורבן בית המקדש השני"
        public static string FormatCompact(SecondTempleInterval i)
        {
            var parts = new List<string>();
            if (i.Years > 0)  parts.Add(FormatUnit(i.Years,  "שנה אחת",  "שנים"));
            if (i.Months > 0) parts.Add(FormatUnit(i.Months, "חודש אחד", "חודשים"));
            if (i.Days > 0)   parts.Add(FormatUnit(i.Days,   "יום אחד",  "ימים"));
            if (parts.Count == 0) parts.Add("פחות מיממה");
            return $"חלפו {JoinHebrew(parts)} מחורבן בית המקדש השני";
        }

        // Compact + hours/minutes since last Jerusalem sunset.
        public static string FormatWithTime(SecondTempleInterval i)
        {
            var parts = new List<string>();
            if (i.Years > 0)   parts.Add(FormatUnit(i.Years,   "שנה אחת",  "שנים"));
            if (i.Months > 0)  parts.Add(FormatUnit(i.Months,  "חודש אחד", "חודשים"));
            if (i.Days > 0)    parts.Add(FormatUnit(i.Days,    "יום אחד",  "ימים"));
            if (i.Hours > 0)   parts.Add(FormatUnit(i.Hours,   "שעה אחת",  "שעות"));
            if (i.Minutes > 0) parts.Add(FormatUnit(i.Minutes, "דקה אחת",  "דקות"));
            if (parts.Count == 0) parts.Add("פחות מדקה");
            return $"חלפו {JoinHebrew(parts)} מחורבן בית המקדש השני";
        }

        private static string FormatUnit(int n, string singular, string pluralWord) =>
            n == 1 ? singular : $"{n} {pluralWord}";

        private static string JoinHebrew(IList<string> parts)
        {
            if (parts.Count == 1) return parts[0];
            var head = string.Join(", ", parts.Take(parts.Count - 1));
            return $"{head} ו-{parts[^1]}";
        }
    }
}
