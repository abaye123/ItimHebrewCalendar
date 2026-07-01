using System;
using System.Linq;
using System.Threading.Tasks;
using ItimHebrewCalendar.Models;
using Windows.ApplicationModel.Appointments;

namespace ItimHebrewCalendar.Services
{
    // One-way push from EventsRepository to a dedicated "עיתים" calendar in the
    // Windows Appointments store. Events with any halachic-anchor reminder are
    // skipped, since the wall-clock time changes daily and Windows Appointments
    // can't represent that.
    public static class WindowsCalendarSyncService
    {
        private const string CalendarName = "עיתים";
        private static AppointmentCalendar? _calendar;
        private static bool _initialized;
        private static bool _started;

        public static void Start()
        {
            if (_started) return;
            _started = true;
            EventsRepository.Changed += OnEventsChanged;
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            EventsRepository.Changed -= OnEventsChanged;
        }

        private static async void OnEventsChanged()
        {
            if (!App.Settings.WindowsCalendarSyncEnabled) return;
            try { await SyncAllAsync(); }
            catch (Exception ex) { SettingsManager.LogError("WindowsCalendarSyncService.OnEventsChanged", ex); }
        }

        public static async Task<int> SyncAllAsync()
        {
            try
            {
                if (!await EnsureCalendarAsync()) return 0;

                int synced = 0;
                var horizon = DateTime.Today.AddMonths(Math.Max(1, App.Settings.IcsExportMonthsAhead));
                foreach (var ev in EventsRepository.All)
                {
                    if (ev.HasZmanAnchor) continue;
                    if (await SyncOneAsync(ev, horizon)) synced++;
                }
                return synced;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("WindowsCalendarSyncService.SyncAllAsync", ex);
                return 0;
            }
        }

        private static async Task<bool> EnsureCalendarAsync()
        {
            if (_calendar != null) return true;
            if (_initialized && _calendar == null) return false;
            _initialized = true;

            try
            {
                var store = await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AppCalendarsReadWrite);
                if (store == null) return false;

                var existing = await store.FindAppointmentCalendarsAsync(FindAppointmentCalendarsOptions.IncludeHidden);
                _calendar = existing.FirstOrDefault(c => c.DisplayName == CalendarName)
                            ?? await store.CreateAppointmentCalendarAsync(CalendarName);
                return _calendar != null;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("WindowsCalendarSyncService.EnsureCalendar", ex);
                return false;
            }
        }

        private static async Task<bool> SyncOneAsync(UserEvent ev, DateTime horizon)
        {
            if (_calendar == null) return false;
            if (!ev.StartGregorian.HasValue) return false;

            // First non-recurring occurrence in the horizon. For recurring events we
            // sync only the next instance - Windows Appointments has its own RRULE
            // model and lifting our recurrence rules into it is intentionally out of scope.
            var firstOcc = EventOccurrenceExpander
                .Expand(ev, DateTime.Today, horizon)
                .FirstOrDefault();
            if (firstOcc.Event == null) return false;

            try
            {
                var appt = new Appointment
                {
                    Subject = ev.Title,
                    Details = ev.Description ?? "",
                    AllDay = ev.IsAllDay,
                };

                var startLocal = firstOcc.Date.Date.Add(ev.StartTime ?? TimeSpan.Zero);
                appt.StartTime = new DateTimeOffset(startLocal);
                appt.Duration = ev.Duration ?? (ev.IsAllDay ? TimeSpan.FromDays(1) : TimeSpan.FromHours(1));

                foreach (var rule in ev.Reminders.Where(r => r.Enabled && r.AnchorKind == ReminderAnchorKind.FixedOffset))
                {
                    appt.Reminder = TimeSpan.FromMinutes(Math.Abs(rule.OffsetMinutes));
                    break; // Windows Appointment supports a single reminder span
                }

                var existingId = ev.ExternalRef?.WindowsAppointmentId;
                if (!string.IsNullOrEmpty(existingId))
                {
                    var existingAppt = await _calendar.GetAppointmentAsync(existingId);
                    if (existingAppt != null)
                        await _calendar.SaveAppointmentAsync(appt);
                    else
                        await _calendar.SaveAppointmentAsync(appt);
                }
                else
                {
                    await _calendar.SaveAppointmentAsync(appt);
                }

                ev.ExternalRef = new ExternalSyncRef
                {
                    WindowsAppointmentId = appt.LocalId,
                    LastSyncedUtc = DateTime.UtcNow
                };
                EventsRepository.AddOrUpdate(ev);
                return true;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("WindowsCalendarSyncService.SyncOne", ex);
                return false;
            }
        }
    }
}
