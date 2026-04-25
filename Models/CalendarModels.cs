using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ItimHebrewCalendar.Models
{
    public class HebrewDateInfo
    {
        [JsonPropertyName("hebYear")] public int HebYear { get; set; }
        [JsonPropertyName("hebMonth")] public int HebMonth { get; set; }
        [JsonPropertyName("hebDay")] public int HebDay { get; set; }
        [JsonPropertyName("monthName")] public string MonthName { get; set; } = "";
        [JsonPropertyName("monthNameEn")] public string MonthNameEn { get; set; } = "";
        [JsonPropertyName("render")] public string Render { get; set; } = "";
        [JsonPropertyName("renderEn")] public string RenderEn { get; set; } = "";
        [JsonPropertyName("dayOfWeek")] public int DayOfWeek { get; set; }
    }

    public class GregorianDateInfo
    {
        [JsonPropertyName("year")] public int Year { get; set; }
        [JsonPropertyName("month")] public int Month { get; set; }
        [JsonPropertyName("day")] public int Day { get; set; }
    }

    public class TodayHebrewDate
    {
        [JsonPropertyName("day")] public int Day { get; set; }
        [JsonPropertyName("dayStr")] public string DayStr { get; set; } = "";
        [JsonPropertyName("month")] public int Month { get; set; }
        [JsonPropertyName("monthName")] public string MonthName { get; set; } = "";
        [JsonPropertyName("year")] public int Year { get; set; }
        [JsonPropertyName("yearStr")] public string YearStr { get; set; } = "";
        [JsonPropertyName("short")] public string Short { get; set; } = "";
    }

    public class MonthlyCalendar
    {
        [JsonPropertyName("year")] public int Year { get; set; }
        [JsonPropertyName("month")] public int Month { get; set; }
        [JsonPropertyName("days")] public List<CalendarDay> Days { get; set; } = new();
    }

    public class CalendarDay
    {
        [JsonPropertyName("gregYear")] public int GregYear { get; set; }
        [JsonPropertyName("gregMonth")] public int GregMonth { get; set; }
        [JsonPropertyName("gregDay")] public int GregDay { get; set; }
        [JsonPropertyName("hebYear")] public int HebYear { get; set; }
        [JsonPropertyName("hebMonth")] public int HebMonth { get; set; }
        [JsonPropertyName("hebDay")] public int HebDay { get; set; }
        [JsonPropertyName("hebDayStr")] public string HebDayStr { get; set; } = "";
        [JsonPropertyName("hebMonthName")] public string HebMonthName { get; set; } = "";
        [JsonPropertyName("dayOfWeek")] public int DayOfWeek { get; set; }
        [JsonPropertyName("events")] public List<CalendarEvent> Events { get; set; } = new();

        public DateTime Date => new(GregYear, GregMonth, GregDay);
        public bool IsShabbat => DayOfWeek == 6;
        public bool IsToday => Date.Date == DateTime.Today;
    }

    public class CalendarEvent
    {
        [JsonPropertyName("desc")] public string Description { get; set; } = "";
        [JsonPropertyName("descEn")] public string DescriptionEn { get; set; } = "";
        [JsonPropertyName("flags")] public long Flags { get; set; }
        [JsonPropertyName("emoji")] public string Emoji { get; set; } = "";
        [JsonPropertyName("isHoliday")] public bool IsHoliday { get; set; }
        [JsonPropertyName("isMajor")] public bool IsMajor { get; set; }
        [JsonPropertyName("isCandleLighting")] public bool IsCandleLighting { get; set; }
        [JsonPropertyName("isHavdalah")] public bool IsHavdalah { get; set; }
        [JsonPropertyName("isParasha")] public bool IsParasha { get; set; }
        [JsonPropertyName("isRoshChodesh")] public bool IsRoshChodesh { get; set; }
        [JsonPropertyName("isFastDay")] public bool IsFastDay { get; set; }
    }

    public class ZmanimInfo
    {
        [JsonPropertyName("alotHaShachar")] public string AlotHaShachar { get; set; } = "";
        [JsonPropertyName("misheyakir")] public string Misheyakir { get; set; } = "";
        [JsonPropertyName("misheyakirMachmir")] public string MisheyakirMachmir { get; set; } = "";
        [JsonPropertyName("sunrise")] public string Sunrise { get; set; } = "";
        [JsonPropertyName("sofZmanShmaMGA")] public string SofZmanShmaMGA { get; set; } = "";
        [JsonPropertyName("sofZmanShma")] public string SofZmanShma { get; set; } = "";
        [JsonPropertyName("sofZmanTfillaMGA")] public string SofZmanTfillaMGA { get; set; } = "";
        [JsonPropertyName("sofZmanTfilla")] public string SofZmanTfilla { get; set; } = "";
        [JsonPropertyName("chatzot")] public string Chatzot { get; set; } = "";
        [JsonPropertyName("minchaGedola")] public string MinchaGedola { get; set; } = "";
        [JsonPropertyName("minchaKetana")] public string MinchaKetana { get; set; } = "";
        [JsonPropertyName("plagHaMincha")] public string PlagHaMincha { get; set; } = "";
        [JsonPropertyName("sunset")] public string Sunset { get; set; } = "";
        [JsonPropertyName("tzeit")] public string Tzeit { get; set; } = "";
        [JsonPropertyName("tzeit72")] public string Tzeit72 { get; set; } = "";
        [JsonPropertyName("candleLighting18")] public string CandleLighting18 { get; set; } = "";
    }

    public class UpcomingShabbat
    {
        [JsonPropertyName("parasha")] public string Parasha { get; set; } = "";
        [JsonPropertyName("candleLighting")] public string CandleLighting { get; set; } = "";
        [JsonPropertyName("havdalah")] public string Havdalah { get; set; } = "";
        [JsonPropertyName("fridayDate")] public string FridayDate { get; set; } = "";
        [JsonPropertyName("saturdayDate")] public string SaturdayDate { get; set; } = "";
    }

    public class LocationInfo
    {
        public string Name { get; set; } = "";
        public string NameEn { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
        public string TimeZone { get; set; } = "";
        public bool IsInIsrael { get; set; }
        public int CandleLightingMinutes { get; set; } = 18;
    }
}
