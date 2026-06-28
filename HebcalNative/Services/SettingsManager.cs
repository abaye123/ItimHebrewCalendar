using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    public enum CalendarViewMode
    {
        Monthly,
        Daily
    }

    [Flags]
    public enum ZmanimDisplay
    {
        None = 0,
        AlotHaShachar = 1 << 0,
        Misheyakir = 1 << 1,
        Sunrise = 1 << 2,
        SofZmanShmaMGA = 1 << 3,
        SofZmanShma = 1 << 4,
        SofZmanTfillaMGA = 1 << 5,
        SofZmanTfilla = 1 << 6,
        Chatzot = 1 << 7,
        MinchaGedola = 1 << 8,
        MinchaKetana = 1 << 9,
        PlagHaMincha = 1 << 10,
        Sunset = 1 << 11,
        Tzeit = 1 << 12,
        Tzeit72 = 1 << 13,

        Default = AlotHaShachar | Sunrise | SofZmanShma | Chatzot
                | MinchaGedola | Sunset | Tzeit
    }

    public enum AppTheme
    {
        System,
        Light,
        Dark
    }

    public enum ZmanimSource
    {
        KosherJava,
        Hebcal
    }

    public enum TrayIconStyle
    {
        Tile,
        TextOnly,
        Classic
    }

    // How the month name is spelled. Only Cheshvan has real variants in practice.
    public enum HebMonthSpelling
    {
        Full,        // חשוון
        Haser,       // חשון
        Marcheshvan  // מרחשוון
    }

    public enum HebYearStyle
    {
        WithMillennium,     // ה'תשפ"ו
        WithoutMillennium,  // תשפ"ו
        Numeric             // 5786
    }

    public enum GregDateStyle
    {
        Long,            // 29 ביוני 2026
        LongWithWeekday, // יום שני, 29 ביוני 2026
        Numeric,         // 29/06/2026
        Iso              // 2026-06-29
    }

    // Controls how Hebrew (and accompanying Gregorian) dates are rendered across the app.
    // The defaults reproduce the historical hard-coded formatting exactly.
    public class HebrewDateFormatOptions
    {
        public bool UseGershayim { get; set; } = true;   // geresh/gershayim on day & year gematria
        public bool UseBetPrefix { get; set; } = true;   // "ב" before the month (ז' בחשוון)
        public bool UseComma { get; set; } = false;      // comma between month and year

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HebMonthSpelling MonthSpelling { get; set; } = HebMonthSpelling.Full;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HebYearStyle YearStyle { get; set; } = HebYearStyle.WithMillennium;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public GregDateStyle GregStyle { get; set; } = GregDateStyle.LongWithWeekday;
    }

    public class AppSettings
    {
        public string CityName { get; set; } = "ירושלים";

        public bool UseIsraeliHolidays { get; set; } = true;
        public int CandleLightingMinutes { get; set; } = 30;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ZmanimDisplay ZmanimToShow { get; set; } = ZmanimDisplay.Default;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AppTheme Theme { get; set; } = AppTheme.System;

        public bool ShowGregorianInCalendar { get; set; } = true;
        public bool StartWithWindows { get; set; } = true;
        public bool ShowHebrewDateInTray { get; set; } = true;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TrayIconStyle TrayIconStyle { get; set; } = TrayIconStyle.Tile;
        public bool CloseTrayPopupOnFocusLoss { get; set; } = true;
        public bool ShowModernHolidays { get; set; } = false;
        public bool UseSunsetDateTransition { get; set; } = false;
        public bool ShowSecondTempleTimer { get; set; } = true;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ZmanimSource ZmanimSource { get; set; } = ZmanimSource.KosherJava;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CalendarViewMode DefaultMainView { get; set; } = CalendarViewMode.Monthly;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CalendarViewMode DefaultTrayView { get; set; } = CalendarViewMode.Monthly;

        public List<StandaloneZmanReminder> StandaloneZmanReminders { get; set; } = new();

        public bool WindowsCalendarSyncEnabled { get; set; } = false;

        public int IcsExportMonthsAhead { get; set; } = 12;

        public int MissedReminderLookbackHours { get; set; } = 24;

        public bool AutoCheckUpdates { get; set; } = true;

        public HebrewDateFormatOptions DateFormat { get; set; } = new();

        // Stored as UTC; null = never checked.
        public DateTime? LastUpdateCheckUtc { get; set; }

        public LocationInfo GetEffectiveLocation()
        {
            var city = CitiesDatabase.FindByName(CityName) ?? CitiesDatabase.Default;
            return new LocationInfo
            {
                Name = city.Name,
                NameEn = city.NameEn,
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                Elevation = city.Elevation,
                TimeZone = city.TimeZone,
                IsInIsrael = UseIsraeliHolidays,
                CandleLightingMinutes = CandleLightingMinutes > 0
                    ? CandleLightingMinutes
                    : city.CandleLightingMinutes
            };
        }
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ItimHebrewCalendar");

        private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");
        private static readonly string ErrorLogPath = Path.Combine(SettingsFolder, "errors.log");

        public static string GetSettingsPath() => SettingsPath;
        public static string GetAppDir() => SettingsFolder;
        public static string GetErrorLogPath() => ErrorLogPath;

        public static AppSettings Load()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                    Directory.CreateDirectory(SettingsFolder);

                if (!File.Exists(SettingsPath))
                {
                    var defaults = new AppSettings();
                    Save(defaults);
                    return defaults;
                }

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                    Directory.CreateDirectory(SettingsFolder);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                LogError("Save", ex);
            }
        }

        public static void LogError(string context, Exception ex)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                    Directory.CreateDirectory(SettingsFolder);
                File.AppendAllText(ErrorLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}\n\n");
            }
            catch { }
        }
    }
}
