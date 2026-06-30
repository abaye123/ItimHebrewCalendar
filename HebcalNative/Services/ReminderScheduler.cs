using System;
using System.Collections.Generic;
using System.Linq;
using ItimHebrewCalendar.Models;

namespace ItimHebrewCalendar.Services
{
    public static class ReminderScheduler
    {
        public static string GetZmanLabel(ZmanimKey k) => k switch
        {
            ZmanimKey.AlotHaShachar     => "עלות השחר",
            ZmanimKey.Misheyakir        => "משיכיר",
            ZmanimKey.MisheyakirMachmir => "משיכיר (מחמיר)",
            ZmanimKey.Sunrise           => "הנץ החמה",
            ZmanimKey.SofZmanShmaMGA    => "סוף ק\"ש (מג\"א)",
            ZmanimKey.SofZmanShma       => "סוף ק\"ש (גר\"א)",
            ZmanimKey.SofZmanTfillaMGA  => "סוף תפילה (מג\"א)",
            ZmanimKey.SofZmanTfilla     => "סוף תפילה (גר\"א)",
            ZmanimKey.Chatzot           => "חצות",
            ZmanimKey.MinchaGedola      => "מנחה גדולה",
            ZmanimKey.MinchaKetana      => "מנחה קטנה",
            ZmanimKey.PlagHaMincha      => "פלג המנחה",
            ZmanimKey.Sunset            => "שקיעה",
            ZmanimKey.Tzeit             => "צאת הכוכבים",
            ZmanimKey.Tzeit72           => "צאת הכוכבים ר\"ת",
            ZmanimKey.CandleLighting18  => "הדלקת נרות (18 דק')",
            _ => k.ToString()
        };

        public static string GetZmanValue(ZmanimInfo info, ZmanimKey k) => k switch
        {
            ZmanimKey.AlotHaShachar     => info.AlotHaShachar,
            ZmanimKey.Misheyakir        => info.Misheyakir,
            ZmanimKey.MisheyakirMachmir => info.MisheyakirMachmir,
            ZmanimKey.Sunrise           => info.Sunrise,
            ZmanimKey.SofZmanShmaMGA    => info.SofZmanShmaMGA,
            ZmanimKey.SofZmanShma       => info.SofZmanShma,
            ZmanimKey.SofZmanTfillaMGA  => info.SofZmanTfillaMGA,
            ZmanimKey.SofZmanTfilla     => info.SofZmanTfilla,
            ZmanimKey.Chatzot           => info.Chatzot,
            ZmanimKey.MinchaGedola      => info.MinchaGedola,
            ZmanimKey.MinchaKetana      => info.MinchaKetana,
            ZmanimKey.PlagHaMincha      => info.PlagHaMincha,
            ZmanimKey.Sunset            => info.Sunset,
            ZmanimKey.Tzeit             => info.Tzeit,
            ZmanimKey.Tzeit72           => info.Tzeit72,
            ZmanimKey.CandleLighting18  => info.CandleLighting18,
            _ => ""
        };

        public static DateTimeOffset? ResolveZmanInstant(DateTime date, ZmanimKey key, LocationInfo loc)
        {
            try
            {
                var info = ZmanimService.GetZmanim(date, loc);
                if (info == null) return null;
                var s = GetZmanValue(info, key);
                if (string.IsNullOrEmpty(s) || !TimeSpan.TryParse(s, out var t)) return null;
                return ToOffset(date.Date.Add(t), loc.TimeZone);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("ReminderScheduler.ResolveZmanInstant", ex);
                return null;
            }
        }

        // Returns one or more concrete reminder firings for a single rule on a single occurrence.
        public static IEnumerable<ReminderOccurrence> ResolveForEventOccurrence(
            EventOccurrence occ, ReminderRule rule, LocationInfo loc)
        {
            if (!rule.Enabled) yield break;
            // Absolute date+time reminders are tied to the event, not to a specific
            // occurrence date, so they're resolved once in ResolveAbsoluteForEvent.
            if (rule.AnchorKind == ReminderAnchorKind.AbsoluteDateTime) yield break;
            var ev = occ.Event;

            DateTimeOffset? eventStart = null;
            if (ev.StartTime.HasValue)
                eventStart = ToOffset(occ.Date.Date.Add(ev.StartTime.Value), loc.TimeZone);
            else
                eventStart = ToOffset(occ.Date.Date, loc.TimeZone);

            if (rule.AnchorKind == ReminderAnchorKind.FixedOffset)
            {
                if (!eventStart.HasValue) yield break;
                var fire = eventStart.Value.AddMinutes(rule.OffsetMinutes);
                yield return new ReminderOccurrence
                {
                    SourceId = ev.Id,
                    SourceTitle = ev.Title,
                    SourceDescription = ev.Description,
                    FireAt = fire,
                    Kind = ReminderOccurrenceKind.UserEvent,
                    AnchorLabel = FormatOffsetLabel(rule.OffsetMinutes, "ההתחלה"),
                    EventStart = eventStart
                };
                yield break;
            }

            // Zman anchor
            var resolved = new List<(ZmanimKey key, DateTimeOffset fire)>();
            foreach (var anchor in rule.ZmanAnchors)
            {
                var zt = ResolveZmanInstant(occ.Date, anchor.Zman, loc);
                if (!zt.HasValue) continue;
                resolved.Add((anchor.Zman, zt.Value.AddMinutes(rule.OffsetMinutes)));
            }
            if (resolved.Count == 0) yield break;

            if (rule.ZmanCombination == ZmanCombination.Earliest)
            {
                var first = resolved.OrderBy(r => r.fire).First();
                yield return new ReminderOccurrence
                {
                    SourceId = ev.Id,
                    SourceTitle = ev.Title,
                    SourceDescription = ev.Description,
                    FireAt = first.fire,
                    Kind = ReminderOccurrenceKind.UserEvent,
                    AnchorLabel = FormatOffsetLabel(rule.OffsetMinutes, GetZmanLabel(first.key)),
                    EventStart = eventStart
                };
            }
            else
            {
                foreach (var r in resolved)
                {
                    yield return new ReminderOccurrence
                    {
                        SourceId = ev.Id,
                        SourceTitle = ev.Title,
                        SourceDescription = ev.Description,
                        FireAt = r.fire,
                        Kind = ReminderOccurrenceKind.UserEvent,
                        AnchorLabel = FormatOffsetLabel(rule.OffsetMinutes, GetZmanLabel(r.key)),
                        EventStart = eventStart
                    };
                }
            }
        }

        // Fires once at the rule's absolute local date+time, regardless of the event's
        // own date. Iterated per event (not per occurrence) by the host service.
        public static IEnumerable<ReminderOccurrence> ResolveAbsoluteForEvent(
            UserEvent ev, ReminderRule rule, LocationInfo loc)
        {
            if (!rule.Enabled) yield break;
            if (rule.AnchorKind != ReminderAnchorKind.AbsoluteDateTime) yield break;
            if (!rule.AbsoluteWhen.HasValue) yield break;

            var fire = ToOffset(rule.AbsoluteWhen.Value, loc.TimeZone);
            DateTimeOffset? eventStart = ev.StartGregorian.HasValue
                ? ToOffset(ev.StartGregorian.Value.Date.Add(ev.StartTime ?? TimeSpan.Zero), loc.TimeZone)
                : null;

            yield return new ReminderOccurrence
            {
                SourceId = ev.Id,
                SourceTitle = ev.Title,
                SourceDescription = ev.Description,
                FireAt = fire,
                Kind = ReminderOccurrenceKind.UserEvent,
                AnchorLabel = $"בתאריך שנקבע ({rule.AbsoluteWhen.Value:dd/MM HH:mm})",
                EventStart = eventStart
            };
        }

        public static IEnumerable<ReminderOccurrence> ResolveStandalone(
            StandaloneZmanReminder reminder, DateTime date, LocationInfo loc, bool isShabbatOrYomTov)
        {
            if (!reminder.Enabled) yield break;
            if (reminder.SkipShabbatYomTov && isShabbatOrYomTov) yield break;

            var dayFlag = date.DayOfWeek switch
            {
                DayOfWeek.Sunday    => DaysOfWeek.Sunday,
                DayOfWeek.Monday    => DaysOfWeek.Monday,
                DayOfWeek.Tuesday   => DaysOfWeek.Tuesday,
                DayOfWeek.Wednesday => DaysOfWeek.Wednesday,
                DayOfWeek.Thursday  => DaysOfWeek.Thursday,
                DayOfWeek.Friday    => DaysOfWeek.Friday,
                DayOfWeek.Saturday  => DaysOfWeek.Saturday,
                _ => DaysOfWeek.None
            };
            if ((reminder.ActiveDays & dayFlag) == 0) yield break;

            var zt = ResolveZmanInstant(date, reminder.Zman, loc);
            if (!zt.HasValue) yield break;

            var fire = zt.Value.AddMinutes(reminder.OffsetMinutes);
            yield return new ReminderOccurrence
            {
                SourceId = reminder.Id,
                SourceTitle = string.IsNullOrEmpty(reminder.Label)
                    ? GetZmanLabel(reminder.Zman)
                    : reminder.Label,
                SourceDescription = null,
                FireAt = fire,
                Kind = ReminderOccurrenceKind.StandaloneZman,
                AnchorLabel = FormatOffsetLabel(reminder.OffsetMinutes, GetZmanLabel(reminder.Zman)),
                EventStart = null
            };
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────

        private static DateTimeOffset ToOffset(DateTime localInZone, string tz)
        {
            try
            {
                var info = TimeZoneInfo.FindSystemTimeZoneById(tz);
                var offset = info.GetUtcOffset(localInZone);
                return new DateTimeOffset(localInZone, offset);
            }
            catch
            {
                return new DateTimeOffset(localInZone, TimeZoneInfo.Local.GetUtcOffset(localInZone));
            }
        }

        public static string FormatOffsetLabel(int offsetMinutes, string anchorName)
        {
            if (offsetMinutes == 0) return $"בזמן {anchorName}";

            int abs = Math.Abs(offsetMinutes);
            string amount;
            if (abs % 60 == 0)
            {
                int h = abs / 60;
                amount = h == 1 ? "שעה" : h == 2 ? "שעתיים" : $"{h} שעות";
            }
            else
            {
                amount = $"{abs} דק'";
            }
            return offsetMinutes < 0 ? $"{amount} לפני {anchorName}" : $"{amount} אחרי {anchorName}";
        }
    }
}
