using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    public static class GeoDetection
    {
        public class DetectionResult
        {
            public double DetectedLatitude { get; set; }
            public double DetectedLongitude { get; set; }
            public LocationInfo ClosestMatch { get; set; } = new();
        }

        public static async Task<DetectionResult?> DetectAsync(CancellationToken ct = default)
        {
            try
            {
                var access = await Geolocator.RequestAccessAsync();
                if (access != GeolocationAccessStatus.Allowed) return null;

                var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
                var pos = await locator.GetGeopositionAsync(
                    maximumAge: TimeSpan.FromMinutes(10),
                    timeout: TimeSpan.FromSeconds(10));

                var p = pos.Coordinate.Point.Position;
                var closest = CitiesDatabase.FindClosest(p.Latitude, p.Longitude);
                if (closest == null) return null;

                return new DetectionResult
                {
                    DetectedLatitude = p.Latitude,
                    DetectedLongitude = p.Longitude,
                    ClosestMatch = closest
                };
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("GeoDetection", ex);
                return null;
            }
        }
    }
}
