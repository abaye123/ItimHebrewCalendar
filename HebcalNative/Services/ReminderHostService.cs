using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    // Loads upcoming reminders and fires them via NotificationDispatcher.
    // One timer drives the next firing point; reminders that fall within an aggregation
    // window are collapsed into a single notification.
    public static class ReminderHostService
    {
        private static readonly string StatePath = Path.Combine(
            SettingsManager.GetAppDir(), "reminder_state.json");

        private const int LookaheadHours = 36;
        private const int AggregationWindowSeconds = 60;

        private static readonly object Lock = new();
        private static Timer? _timer;
        private static List<ReminderOccurrence> _upcoming = new();
        private static readonly HashSet<string> _firedKeys = new();
        private static DateTimeOffset _lastSeenUtc;
        private static bool _started;

        public static void Start()
        {
            if (_started) return;
            _started = true;

            LoadState();
            EventsRepository.Changed += OnDataChanged;
            ReplayMissed();
            Reschedule();

            // Refresh at the next midnight; the timer also covers normal operation.
            ScheduleMidnightRefresh();
        }

        public static void Stop()
        {
            _started = false;
            EventsRepository.Changed -= OnDataChanged;
            _timer?.Dispose();
            _timer = null;
            SaveState();
        }

        public static void RefreshNow()
        {
            if (!_started) return;
            Reschedule();
        }

        // Called by SettingsWindow after the user edits standalone reminders.
        public static void OnSettingsChanged() => RefreshNow();

        private static void OnDataChanged() => RefreshNow();

        // ─── Core scheduling ───────────────────────────────────────────────────────

        private static void Reschedule()
        {
            try
            {
                var loc = App.Settings.GetEffectiveLocation();
                var now = DateTimeOffset.Now;
                var horizon = now.AddHours(LookaheadHours);
                var occurrences = CollectOccurrences(loc, now, horizon);

                lock (Lock)
                {
                    _upcoming = occurrences
                        .Where(o => o.FireAt >= now)
                        .OrderBy(o => o.FireAt)
                        .ToList();
                    SetTimerForNext(now);
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ReminderHostService.Reschedule", ex);
            }
        }

        private static void SetTimerForNext(DateTimeOffset now)
        {
            _timer?.Dispose();
            _timer = null;

            if (_upcoming.Count == 0) return;
            var next = _upcoming[0].FireAt;
            var due = next - now;
            if (due < TimeSpan.Zero) due = TimeSpan.Zero;
            // Cap to prevent absurdly long timers (the midnight refresh will reschedule).
            if (due > TimeSpan.FromHours(LookaheadHours)) due = TimeSpan.FromHours(LookaheadHours);

            _timer = new Timer(_ => OnTimerFire(), null, due, Timeout.InfiniteTimeSpan);
        }

        private static void OnTimerFire()
        {
            try
            {
                var fireWindowEnd = DateTimeOffset.Now.AddSeconds(AggregationWindowSeconds);
                List<ReminderOccurrence> batch;
                lock (Lock)
                {
                    batch = _upcoming.TakeWhile(o => o.FireAt <= fireWindowEnd).ToList();
                    _upcoming.RemoveRange(0, batch.Count);
                    foreach (var b in batch) _firedKeys.Add(KeyOf(b));
                }
                if (batch.Count > 0)
                {
                    NotificationDispatcher.Show(batch);
                    SaveState();
                }
                lock (Lock) SetTimerForNext(DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ReminderHostService.OnTimerFire", ex);
            }
        }

        private static void ScheduleMidnightRefresh()
        {
            try
            {
                var now = DateTime.Now;
                var nextMidnight = now.Date.AddDays(1).AddSeconds(5);
                var due = nextMidnight - now;
                _ = new Timer(_ =>
                {
                    Reschedule();
                    ScheduleMidnightRefresh();
                }, null, due, Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ReminderHostService.MidnightRefresh", ex);
            }
        }

        // ─── Missed reminders on startup ───────────────────────────────────────────

        private static void ReplayMissed()
        {
            try
            {
                var lookback = TimeSpan.FromHours(Math.Max(1, App.Settings.MissedReminderLookbackHours));
                var now = DateTimeOffset.Now;
                var since = _lastSeenUtc == default
                    ? now - lookback
                    : (_lastSeenUtc > now - lookback ? _lastSeenUtc : now - lookback);

                var loc = App.Settings.GetEffectiveLocation();
                var occurrences = CollectOccurrences(loc, since, now);
                var missed = occurrences
                    .Where(o => o.FireAt >= since && o.FireAt <= now)
                    .Where(o => !_firedKeys.Contains(KeyOf(o)))
                    .OrderBy(o => o.FireAt)
                    .ToList();

                if (missed.Count > 0)
                {
                    NotificationDispatcher.ShowMissedSummary(missed);
                    foreach (var m in missed) _firedKeys.Add(KeyOf(m));
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ReminderHostService.ReplayMissed", ex);
            }
        }

        // ─── Occurrence collection ─────────────────────────────────────────────────

        private static List<ReminderOccurrence> CollectOccurrences(
            LocationInfo loc, DateTimeOffset from, DateTimeOffset to)
        {
            var result = new List<ReminderOccurrence>();
            var fromDate = from.Date;
            var toDate = to.Date;

            // User events
            foreach (var ev in EventsRepository.All)
            {
                foreach (var occ in EventOccurrenceExpander.Expand(ev, fromDate, toDate))
                    foreach (var rule in ev.Reminders)
                        foreach (var fire in ReminderScheduler.ResolveForEventOccurrence(occ, rule, loc))
                            if (fire.FireAt >= from && fire.FireAt <= to)
                                result.Add(fire);

                // Absolute date+time reminders fire on their own date, which may be
                // outside the event's occurrence window — resolve them per event.
                foreach (var rule in ev.Reminders)
                    foreach (var fire in ReminderScheduler.ResolveAbsoluteForEvent(ev, rule, loc))
                        if (fire.FireAt >= from && fire.FireAt <= to)
                            result.Add(fire);
            }

            // Standalone zman reminders
            foreach (var sr in App.Settings.StandaloneZmanReminders)
            {
                for (var d = fromDate; d <= toDate; d = d.AddDays(1))
                {
                    bool isShabbat = d.DayOfWeek == DayOfWeek.Saturday;
                    foreach (var fire in ReminderScheduler.ResolveStandalone(sr, d, loc, isShabbat))
                        if (fire.FireAt >= from && fire.FireAt <= to)
                            result.Add(fire);
                }
            }

            return result;
        }

        // ─── State persistence ─────────────────────────────────────────────────────

        private class PersistedState
        {
            public DateTimeOffset LastSeenUtc { get; set; }
            public List<string> RecentlyFired { get; set; } = new();
        }

        private static void LoadState()
        {
            try
            {
                if (!File.Exists(StatePath)) return;
                var json = File.ReadAllText(StatePath);
                var state = JsonSerializer.Deserialize<PersistedState>(json);
                if (state == null) return;
                _lastSeenUtc = state.LastSeenUtc;
                lock (Lock)
                {
                    _firedKeys.Clear();
                    foreach (var k in state.RecentlyFired) _firedKeys.Add(k);
                }
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ReminderHostService.LoadState", ex);
            }
        }

        private static void SaveState()
        {
            try
            {
                var cutoff = DateTimeOffset.Now.AddHours(-48);
                List<string> keys;
                lock (Lock)
                {
                    keys = _firedKeys
                        .Where(k => TryParseKeyTime(k, out var t) && t >= cutoff)
                        .ToList();
                    _firedKeys.Clear();
                    foreach (var k in keys) _firedKeys.Add(k);
                }
                var state = new PersistedState
                {
                    LastSeenUtc = DateTimeOffset.Now,
                    RecentlyFired = keys
                };
                var dir = SettingsManager.GetAppDir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ReminderHostService.SaveState", ex);
            }
        }

        private static string KeyOf(ReminderOccurrence o) =>
            $"{o.SourceId}|{o.FireAt:O}";

        private static bool TryParseKeyTime(string key, out DateTimeOffset t)
        {
            t = default;
            int sep = key.IndexOf('|');
            if (sep < 0) return false;
            return DateTimeOffset.TryParse(key[(sep + 1)..], out t);
        }
    }
}
