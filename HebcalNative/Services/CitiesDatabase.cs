using System.Collections.Generic;
using System.Linq;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    public static class CitiesDatabase
    {
        public static readonly List<LocationInfo> Cities = new()
        {
            new() { Name = "ירושלים", NameEn = "Jerusalem", Latitude = 31.7683, Longitude = 35.2137, Elevation = 757, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 40 },
            new() { Name = "תל אביב", NameEn = "Tel Aviv", Latitude = 32.0853, Longitude = 34.7818, Elevation = 5, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "חיפה", NameEn = "Haifa", Latitude = 32.7940, Longitude = 34.9896, Elevation = 50, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "באר שבע", NameEn = "Beer Sheva", Latitude = 31.2518, Longitude = 34.7913, Elevation = 260, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "אשדוד", NameEn = "Ashdod", Latitude = 31.8014, Longitude = 34.6435, Elevation = 5, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "נתניה", NameEn = "Netanya", Latitude = 32.3328, Longitude = 34.8667, Elevation = 30, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "פתח תקווה", NameEn = "Petah Tikva", Latitude = 32.0871, Longitude = 34.8878, Elevation = 34, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "לוד", NameEn = "Lod", Latitude = 31.9518, Longitude = 34.8948, Elevation = 63, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "ראשון לציון", NameEn = "Rishon LeZion", Latitude = 31.9730, Longitude = 34.7925, Elevation = 35, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "בני ברק", NameEn = "Bnei Brak", Latitude = 32.0809, Longitude = 34.8338, Elevation = 30, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "אילת", NameEn = "Eilat", Latitude = 29.5577, Longitude = 34.9519, Elevation = 12, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "טבריה", NameEn = "Tiberias", Latitude = 32.7922, Longitude = 35.5312, Elevation = -200, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "צפת", NameEn = "Safed", Latitude = 32.9650, Longitude = 35.4967, Elevation = 900, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "עפולה", NameEn = "Afula", Latitude = 32.6093, Longitude = 35.2894, Elevation = 60, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "נוף הגליל", NameEn = "Nof HaGalil", Latitude = 32.7036, Longitude = 35.3033, Elevation = 500, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 40 },
            new() { Name = "בית שמש", NameEn = "Beit Shemesh", Latitude = 31.7488, Longitude = 34.9887, Elevation = 300, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "מודיעין", NameEn = "Modi'in", Latitude = 31.8942, Longitude = 35.0104, Elevation = 300, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "הרצליה", NameEn = "Herzliya", Latitude = 32.1663, Longitude = 34.8434, Elevation = 35, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "רעננה", NameEn = "Ra'anana", Latitude = 32.1836, Longitude = 34.8705, Elevation = 64, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "רמת גן", NameEn = "Ramat Gan", Latitude = 32.0823, Longitude = 34.8142, Elevation = 54, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "אשקלון", NameEn = "Ashkelon", Latitude = 31.6688, Longitude = 34.5743, Elevation = 60, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "רחובות", NameEn = "Rehovot", Latitude = 31.8928, Longitude = 34.8113, Elevation = 80, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },
            new() { Name = "קרית שמונה", NameEn = "Kiryat Shmona", Latitude = 33.2077, Longitude = 35.5697, Elevation = 150, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 18 },

            new() { Name = "ביתר עילית", NameEn = "Beitar Illit", Latitude = 31.6950, Longitude = 35.1158, Elevation = 750, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 40 },
            new() { Name = "מודיעין עילית", NameEn = "Modi'in Illit", Latitude = 31.9396, Longitude = 35.0375, Elevation = 350, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "אלעד", NameEn = "Elad", Latitude = 32.0522, Longitude = 34.9519, Elevation = 100, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 20 },
            new() { Name = "עמנואל", NameEn = "Emmanuel", Latitude = 32.1611, Longitude = 35.1344, Elevation = 450, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 20 },
            new() { Name = "רכסים", NameEn = "Rekhasim", Latitude = 32.7447, Longitude = 35.0808, Elevation = 240, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 25 },
            new() { Name = "אופקים", NameEn = "Ofakim", Latitude = 31.3117, Longitude = 34.6206, Elevation = 168, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 20 },
            new() { Name = "נתיבות", NameEn = "Netivot", Latitude = 31.4200, Longitude = 34.5894, Elevation = 143, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 20 },
            new() { Name = "חריש", NameEn = "Harish", Latitude = 32.4641, Longitude = 35.0438, Elevation = 130, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 20 },
            new() { Name = "חצור הגלילית", NameEn = "Hatzor HaGlilit", Latitude = 32.9789, Longitude = 35.5419, Elevation = 300, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 25 },
            new() { Name = "קרית יערים (טלז-סטון)", NameEn = "Kiryat Ye'arim", Latitude = 31.8100, Longitude = 35.1000, Elevation = 600, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "תפרח", NameEn = "Tifrach", Latitude = 31.3219, Longitude = 34.6775, Elevation = 200, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 20 },
            new() { Name = "כרמיאל", NameEn = "Karmiel", Latitude = 32.9167, Longitude = 35.2956, Elevation = 270, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 25 },
            new() { Name = "מירון", NameEn = "Meron", Latitude = 32.9842, Longitude = 35.4397, Elevation = 700, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "גבעת זאב", NameEn = "Giv'at Ze'ev", Latitude = 31.8561, Longitude = 35.1683, Elevation = 700, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 40 },
            new() { Name = "רמת בית שמש", NameEn = "Ramat Beit Shemesh", Latitude = 31.7300, Longitude = 34.9900, Elevation = 320, TimeZone = "Asia/Jerusalem", IsInIsrael = true, CandleLightingMinutes = 30 },
            new() { Name = "ניו יורק", NameEn = "New York", Latitude = 40.7128, Longitude = -74.0060, Elevation = 10, TimeZone = "America/New_York", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "לוס אנג'לס", NameEn = "Los Angeles", Latitude = 34.0522, Longitude = -118.2437, Elevation = 71, TimeZone = "America/Los_Angeles", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "לונדון", NameEn = "London", Latitude = 51.5074, Longitude = -0.1278, Elevation = 11, TimeZone = "Europe/London", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "פריז", NameEn = "Paris", Latitude = 48.8566, Longitude = 2.3522, Elevation = 35, TimeZone = "Europe/Paris", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "טורונטו", NameEn = "Toronto", Latitude = 43.6532, Longitude = -79.3832, Elevation = 76, TimeZone = "America/Toronto", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "מיאמי", NameEn = "Miami", Latitude = 25.7617, Longitude = -80.1918, Elevation = 2, TimeZone = "America/New_York", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "ברוקלין", NameEn = "Brooklyn", Latitude = 40.6782, Longitude = -73.9442, Elevation = 16, TimeZone = "America/New_York", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "מלבורן", NameEn = "Melbourne", Latitude = -37.8136, Longitude = 144.9631, Elevation = 31, TimeZone = "Australia/Melbourne", IsInIsrael = false, CandleLightingMinutes = 18 },
            new() { Name = "סידני", NameEn = "Sydney", Latitude = -33.8688, Longitude = 151.2093, Elevation = 58, TimeZone = "Australia/Sydney", IsInIsrael = false, CandleLightingMinutes = 18 },
        };

        public static LocationInfo Default =>
            Cities.FirstOrDefault(c => c.NameEn == "Jerusalem") ?? Cities[0];

        public static LocationInfo? FindByName(string name) =>
            Cities.FirstOrDefault(c => c.Name == name || c.NameEn == name);

        public static LocationInfo? FindClosest(double lat, double lon)
        {
            LocationInfo? best = null;
            double bestKm = double.MaxValue;
            foreach (var c in Cities)
            {
                var km = HaversineKm(lat, lon, c.Latitude, c.Longitude);
                if (km < bestKm) { bestKm = km; best = c; }
            }
            return best;
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double toRad(double d) => d * System.Math.PI / 180.0;
            var dLat = toRad(lat2 - lat1);
            var dLon = toRad(lon2 - lon1);
            var a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2)
                  + System.Math.Cos(toRad(lat1)) * System.Math.Cos(toRad(lat2))
                  * System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
            return R * 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));
        }
    }
}
