using System;
using System.Threading;
using System.Threading.Tasks;

namespace ItimHebrewCalendar.Services
{
    // Runs a release check every 24 hours while the app is alive. If the last
    // recorded check was more than CheckInterval ago (or never), the first run
    // fires shortly after startup. A toast is surfaced once per session when a
    // new version becomes available; the user can also disable the whole thing
    // from Settings (`AutoCheckUpdates`).
    public static class UpdateCheckScheduler
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
        private static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(1);

        private static Timer? _timer;
        private static readonly object _lock = new();
        private static UpdateChecker.ReleaseInfo? _availableUpdate;
        private static Version? _toastedVersion;

        // Latest update found by any check (manual or auto) since the app started.
        public static UpdateChecker.ReleaseInfo? AvailableUpdate
        {
            get { lock (_lock) return _availableUpdate; }
        }

        public static void Start()
        {
            Stop();
            if (!App.Settings.AutoCheckUpdates) return;

            var dueIn = StartupGrace;
            var last = App.Settings.LastUpdateCheckUtc;
            if (last.HasValue)
            {
                var elapsed = DateTime.UtcNow - last.Value;
                if (elapsed < CheckInterval)
                {
                    // Wait out the remainder; still bounded below by the startup grace.
                    var remaining = CheckInterval - elapsed;
                    dueIn = remaining > StartupGrace ? remaining : StartupGrace;
                }
            }

            _timer = new Timer(_ => _ = CheckNow(showToastOnAvailable: true), null, dueIn, CheckInterval);
        }

        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        // Called by SettingsWindow after the user toggles the auto-check switch
        // (or any other settings save). Idempotent - Start() stops first.
        public static void OnSettingsChanged() => Start();

        public static async Task<UpdateChecker.ReleaseInfo?> CheckNow(bool showToastOnAvailable)
        {
            try
            {
                var release = await UpdateChecker.GetLatestReleaseAsync().ConfigureAwait(false);

                App.Settings.LastUpdateCheckUtc = DateTime.UtcNow;
                SettingsManager.Save(App.Settings);

                var current = UpdateChecker.GetCurrentVersion();
                if (release.ParsedVersion != null
                    && UpdateChecker.IsNewer(current, release.ParsedVersion)
                    && !string.IsNullOrEmpty(release.AssetUrl))
                {
                    lock (_lock) _availableUpdate = release;

                    if (showToastOnAvailable && ShouldToast(release.ParsedVersion))
                    {
                        NotificationDispatcher.ShowUpdateAvailable(release);
                    }
                    return release;
                }

                // Same or older - clear any stale cache so the toggle reflects reality.
                lock (_lock) _availableUpdate = null;
                return null;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("UpdateCheckScheduler.CheckNow", ex);
                return null;
            }
        }

        private static bool ShouldToast(Version v)
        {
            // Only toast once per (session × version) so the 24-hour loop doesn't
            // re-pester the user about the same release they've seen before.
            lock (_lock)
            {
                if (_toastedVersion != null && _toastedVersion >= v) return false;
                _toastedVersion = v;
                return true;
            }
        }
    }
}
