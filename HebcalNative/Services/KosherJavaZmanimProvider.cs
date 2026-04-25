using System;
using Zmanim;
using Zmanim.TimeZone;
using Zmanim.Utilities;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    internal static class KosherJavaZmanimProvider
    {
        public static ZmanimInfo? GetZmanim(DateTime date, LocationInfo loc)
        {
            try
            {
                ITimeZone tz;
                try
                {
                    tz = new WindowsTimeZone(loc.TimeZone);
                }
                catch
                {
                    tz = new WindowsTimeZone();
                }

                var geo = new GeoLocation(loc.Name, loc.Latitude, loc.Longitude, loc.Elevation, tz);
                var cal = new ComplexZmanimCalendar(geo);
                cal.DateWithLocation.Date = date.Date;

                string Fmt(DateTime? t) => t.HasValue ? t.Value.ToString("HH:mm") : "";

                return new ZmanimInfo
                {
                    AlotHaShachar     = Fmt(cal.GetAlos72()),
                    Misheyakir        = Fmt(cal.GetMisheyakir10Point2Degrees()),
                    MisheyakirMachmir = Fmt(cal.GetMisheyakir11Point5Degrees()),
                    Sunrise           = Fmt(cal.GetSunrise()),
                    SofZmanShmaMGA    = Fmt(cal.GetSofZmanShmaMGA()),
                    SofZmanShma       = Fmt(cal.GetSofZmanShmaGRA()),
                    SofZmanTfillaMGA  = Fmt(cal.GetSofZmanTfilaMGA()),
                    SofZmanTfilla     = Fmt(cal.GetSofZmanTfilaGRA()),
                    Chatzot           = Fmt(cal.GetChatzos()),
                    MinchaGedola      = Fmt(cal.GetMinchaGedola()),
                    MinchaKetana      = Fmt(cal.GetMinchaKetana()),
                    PlagHaMincha      = Fmt(cal.GetPlagHamincha()),
                    Sunset            = Fmt(cal.GetSunset()),
                    Tzeit             = Fmt(cal.GetTzais()),
                    Tzeit72           = Fmt(cal.GetTzais72()),
                    CandleLighting18  = Fmt(cal.GetSunset()?.AddMinutes(-18))
                };
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("KosherJavaZmanimProvider", ex);
                return null;
            }
        }
    }

    public static class ZmanimService
    {
        public static ZmanimInfo? GetZmanim(DateTime date, LocationInfo loc)
        {
            return App.Settings.ZmanimSource switch
            {
                ZmanimSource.KosherJava => KosherJavaZmanimProvider.GetZmanim(date, loc),
                _ => HebcalBridge.GetZmanim(date, loc)
            };
        }
    }
}
