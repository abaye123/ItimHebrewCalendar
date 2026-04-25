using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    // Bridge to HebcalNative.dll (built from hebcal-go).
    // Every export returns a JSON string we deserialize to POCOs.
    // FreeString MUST be called on every returned pointer to avoid leaking Go heap memory.
    public static class HebcalBridge
    {
        private const string DllName = "HebcalNative.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GregorianToHebrew(int year, int month, int day);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr HebrewToGregorian(int hebYear, int hebMonth, int hebDay);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetMonthlyCalendar(int gregYear, int gregMonth, int useIsrael, int noModern);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetZmanim(int gregYear, int gregMonth, int gregDay,
            double lat, double lon, double elev, [MarshalAs(UnmanagedType.LPStr)] string tz);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetTodayHebrewDate([MarshalAs(UnmanagedType.LPStr)] string tz);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetUpcomingShabbat(double lat, double lon, double elev,
            [MarshalAs(UnmanagedType.LPStr)] string tz, int useIsrael, int candleLightingMinutes);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeString(IntPtr s);

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static string MarshalAndFree(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "{}";
            try
            {
                return Marshal.PtrToStringUTF8(ptr) ?? "{}";
            }
            finally
            {
                FreeString(ptr);
            }
        }

        public static bool EnsureNativeLoaded(out string error)
        {
            error = "";
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var dll = Path.Combine(exeDir, "HebcalNative.dll");
                if (!File.Exists(dll))
                {
                    error = $"HebcalNative.dll לא נמצא בתיקייה:\n{exeDir}\n\n" +
                            "יש לבנות אותו באמצעות build-hebcal-dll.bat (דורש Go + MinGW).";
                    return false;
                }
                _ = GetToday("Asia/Jerusalem");
                return true;
            }
            catch (Exception ex)
            {
                error = $"טעינת HebcalNative.dll נכשלה:\n{ex.Message}";
                return false;
            }
        }

        public static HebrewDateInfo? Convert(DateTime gregorian)
        {
            var json = MarshalAndFree(GregorianToHebrew(gregorian.Year, gregorian.Month, gregorian.Day));
            return JsonSerializer.Deserialize<HebrewDateInfo>(json, JsonOpts);
        }

        public static GregorianDateInfo? ConvertFromHebrew(int hebYear, int hebMonth, int hebDay)
        {
            var json = MarshalAndFree(HebrewToGregorian(hebYear, hebMonth, hebDay));
            return JsonSerializer.Deserialize<GregorianDateInfo>(json, JsonOpts);
        }

        public static MonthlyCalendar? GetMonth(int year, int month, bool israel, bool showModern = false)
        {
            var json = MarshalAndFree(GetMonthlyCalendar(
                year, month, israel ? 1 : 0, showModern ? 0 : 1));
            return JsonSerializer.Deserialize<MonthlyCalendar>(json, JsonOpts);
        }

        // Builds a full Hebrew month by pulling one or two adjacent gregorian months
        // and filtering for days that belong to the requested Hebrew month.
        public static MonthlyCalendar? GetHebrewMonth(int hebYear, int hebMonth, bool israel, bool showModern = false)
        {
            var g1 = ConvertFromHebrew(hebYear, hebMonth, 1);
            if (g1 == null) return null;

            var days = new List<CalendarDay>();

            var firstG = GetMonth(g1.Year, g1.Month, israel, showModern);
            if (firstG == null) return null;
            days.AddRange(firstG.Days.Where(d => d.HebYear == hebYear && d.HebMonth == hebMonth));

            if (firstG.Days.Count > 0)
            {
                var last = firstG.Days[^1];
                if (last.HebYear == hebYear && last.HebMonth == hebMonth)
                {
                    var nextG = new DateTime(g1.Year, g1.Month, 1).AddMonths(1);
                    var secondG = GetMonth(nextG.Year, nextG.Month, israel, showModern);
                    if (secondG != null)
                    {
                        days.AddRange(secondG.Days.Where(d => d.HebYear == hebYear && d.HebMonth == hebMonth));
                    }
                }
            }

            return new MonthlyCalendar
            {
                Year = hebYear,
                Month = hebMonth,
                Days = days
            };
        }

        public static ZmanimInfo? GetZmanim(DateTime date, LocationInfo loc)
        {
            var json = MarshalAndFree(GetZmanim(date.Year, date.Month, date.Day,
                loc.Latitude, loc.Longitude, loc.Elevation, loc.TimeZone));
            return JsonSerializer.Deserialize<ZmanimInfo>(json, JsonOpts);
        }

        public static TodayHebrewDate? GetToday(string timeZone)
        {
            var json = MarshalAndFree(GetTodayHebrewDate(timeZone));
            return JsonSerializer.Deserialize<TodayHebrewDate>(json, JsonOpts);
        }

        public static DateTime GetHalachicGregorianDate(LocationInfo loc, bool enableSunsetTransition)
        {
            if (!enableSunsetTransition) return DateTime.Today;
            try
            {
                var now = DateTime.Now;
                var zmanim = ZmanimService.GetZmanim(now, loc);
                if (zmanim == null || string.IsNullOrEmpty(zmanim.Sunset)) return DateTime.Today;
                if (TimeSpan.TryParse(zmanim.Sunset, out var sunset) && now.TimeOfDay >= sunset)
                    return DateTime.Today.AddDays(1);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("GetHalachicGregorianDate", ex);
            }
            return DateTime.Today;
        }

        public static (TodayHebrewDate? today, bool afterSunset) GetHalachicToday(
            LocationInfo loc, bool enableSunsetTransition)
        {
            var civil = GetToday(loc.TimeZone);
            if (!enableSunsetTransition || civil == null) return (civil, false);

            try
            {
                var now = DateTime.Now;
                var zmanim = ZmanimService.GetZmanim(now, loc);
                if (zmanim == null || string.IsNullOrEmpty(zmanim.Sunset)) return (civil, false);
                if (!TimeSpan.TryParse(zmanim.Sunset, out var sunset)) return (civil, false);
                if (now.TimeOfDay < sunset) return (civil, false);

                var heb = Convert(DateTime.Today.AddDays(1));
                if (heb == null) return (civil, false);

                var shifted = new TodayHebrewDate
                {
                    Day = heb.HebDay,
                    DayStr = HebrewNumberFormatter.FormatDay(heb.HebDay),
                    Month = heb.HebMonth,
                    MonthName = heb.MonthName,
                    Year = heb.HebYear,
                    YearStr = HebrewNumberFormatter.FormatYear(heb.HebYear),
                    Short = $"{HebrewNumberFormatter.FormatDay(heb.HebDay)} ב{heb.MonthName}"
                };
                return (shifted, true);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("GetHalachicToday", ex);
                return (civil, false);
            }
        }

        public static UpcomingShabbat? GetShabbat(LocationInfo loc)
        {
            var json = MarshalAndFree(GetUpcomingShabbat(
                loc.Latitude, loc.Longitude, loc.Elevation, loc.TimeZone,
                loc.IsInIsrael ? 1 : 0, loc.CandleLightingMinutes));
            var res = JsonSerializer.Deserialize<UpcomingShabbat>(json, JsonOpts);
            if (res != null)
            {
                // Defensive parse: older DLL builds returned "Mon 2 Jan · 15:04";
                // we extract the trailing HH:MM so callers always see a clean time.
                res.CandleLighting = ExtractTrailingTime(res.CandleLighting);
                res.Havdalah = ExtractTrailingTime(res.Havdalah);
            }
            return res;
        }

        private static readonly Regex TrailingTimePattern = new(@"(\d{1,2}:\d{2})\s*$", RegexOptions.Compiled);

        private static string ExtractTrailingTime(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var m = TrailingTimePattern.Match(s);
            return m.Success ? m.Groups[1].Value : s;
        }
    }
}
