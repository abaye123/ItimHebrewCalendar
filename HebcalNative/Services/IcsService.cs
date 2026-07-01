using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ItimHebrewCalendar.Models;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ItimHebrewCalendar.Services
{
    // Minimal RFC 5545 (iCalendar) export & import. Designed for compatibility
    // with Outlook / Google Calendar / Apple Calendar - only the fields they
    // commonly read are produced. Halachic anchors are flattened: each occurrence
    // gets a fixed wall-clock time computed at export time.
    public static class IcsService
    {
        public static async Task<string?> PickSaveAsync(Window window)
        {
            try
            {
                var picker = new FileSavePicker { SuggestedFileName = "itim-events" };
                picker.FileTypeChoices.Add("iCalendar", new List<string> { ".ics" });
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                var file = await picker.PickSaveFileAsync();
                return file?.Path;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("IcsService.PickSaveAsync", ex);
                return null;
            }
        }

        public static async Task<string?> PickOpenAsync(Window window)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".ics");
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                var file = await picker.PickSingleFileAsync();
                return file?.Path;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("IcsService.PickOpenAsync", ex);
                return null;
            }
        }

        // ─── Export ────────────────────────────────────────────────────────────────

        public static int Export(string path, int monthsAhead, LocationInfo loc)
        {
            int count = 0;
            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//ItimHebrewCalendar//ICS//HE");
            sb.AppendLine("CALSCALE:GREGORIAN");

            var from = DateTime.Today;
            var to = DateTime.Today.AddMonths(Math.Max(1, monthsAhead));
            foreach (var ev in EventsRepository.All)
            {
                foreach (var occ in EventOccurrenceExpander.Expand(ev, from, to))
                {
                    AppendEvent(sb, occ, loc);
                    count++;
                }
            }

            sb.AppendLine("END:VCALENDAR");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return count;
        }

        private static void AppendEvent(StringBuilder sb, EventOccurrence occ, LocationInfo loc)
        {
            var ev = occ.Event;
            var date = occ.Date;
            var startTime = ev.StartTime ?? TimeSpan.Zero;

            // For zman-anchored reminders we don't move the event itself; the event
            // still has a real wall-clock start. ICS reminders (VALARM) use the event
            // start as the anchor, so we resolve zman → wall clock here for VALARM.
            var startDt = date.Add(startTime);
            var endDt = ev.Duration.HasValue ? startDt.Add(ev.Duration.Value) : startDt.AddHours(1);

            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{ev.Id}-{date:yyyyMMdd}@itim");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            if (ev.IsAllDay)
            {
                sb.AppendLine($"DTSTART;VALUE=DATE:{date:yyyyMMdd}");
                sb.AppendLine($"DTEND;VALUE=DATE:{date.AddDays(1):yyyyMMdd}");
            }
            else
            {
                sb.AppendLine($"DTSTART:{startDt:yyyyMMddTHHmmss}");
                sb.AppendLine($"DTEND:{endDt:yyyyMMddTHHmmss}");
            }
            sb.AppendLine($"SUMMARY:{Escape(ev.Title)}");
            if (!string.IsNullOrEmpty(ev.Description))
                sb.AppendLine($"DESCRIPTION:{Escape(ev.Description)}");

            foreach (var rule in ev.Reminders)
            {
                if (!rule.Enabled) continue;
                int triggerMin = ResolveTriggerMinutesBefore(occ, rule, loc, startDt);
                sb.AppendLine("BEGIN:VALARM");
                sb.AppendLine("ACTION:DISPLAY");
                sb.AppendLine($"DESCRIPTION:{Escape(ev.Title)}");
                sb.AppendLine($"TRIGGER:-PT{Math.Max(0, triggerMin)}M");
                sb.AppendLine("END:VALARM");
            }

            sb.AppendLine("END:VEVENT");
        }

        private static int ResolveTriggerMinutesBefore(
            EventOccurrence occ, ReminderRule rule, LocationInfo loc, DateTime startDt)
        {
            if (rule.AnchorKind == ReminderAnchorKind.FixedOffset)
            {
                // negative offset = before
                return -rule.OffsetMinutes;
            }
            DateTime? earliest = null;
            foreach (var anchor in rule.ZmanAnchors)
            {
                var t = ReminderScheduler.ResolveZmanInstant(occ.Date, anchor.Zman, loc);
                if (!t.HasValue) continue;
                var local = t.Value.LocalDateTime.AddMinutes(rule.OffsetMinutes);
                if (earliest == null || local < earliest) earliest = local;
            }
            if (!earliest.HasValue) return 0;
            return (int)Math.Max(0, (startDt - earliest.Value).TotalMinutes);
        }

        // ─── Import ────────────────────────────────────────────────────────────────

        public static int Import(string path)
        {
            var lines = File.ReadAllLines(path);
            var events = new List<UserEvent>();
            UserEvent? current = null;
            DateTime? startUtc = null, endUtc = null;
            DateTime? startDate = null;
            bool currentIsAllDay = false;

            foreach (var rawLine in UnfoldLines(lines))
            {
                var line = rawLine;
                if (line.StartsWith("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    current = new UserEvent { IsImported = true };
                    startUtc = endUtc = null;
                    startDate = null;
                    currentIsAllDay = false;
                }
                else if (line.StartsWith("END:VEVENT", StringComparison.OrdinalIgnoreCase) && current != null)
                {
                    if (currentIsAllDay && startDate.HasValue)
                    {
                        current.StartGregorian = startDate.Value;
                        current.StartTime = null;
                    }
                    else if (startUtc.HasValue)
                    {
                        current.StartGregorian = startUtc.Value.Date;
                        current.StartTime = startUtc.Value.TimeOfDay;
                        if (endUtc.HasValue && endUtc.Value > startUtc.Value)
                            current.Duration = endUtc.Value - startUtc.Value;
                    }
                    if (!string.IsNullOrWhiteSpace(current.Title) && current.StartGregorian.HasValue)
                        events.Add(current);
                    current = null;
                }
                else if (current != null)
                {
                    if (line.StartsWith("SUMMARY", StringComparison.OrdinalIgnoreCase))
                        current.Title = Unescape(StripField(line));
                    else if (line.StartsWith("DESCRIPTION", StringComparison.OrdinalIgnoreCase))
                        current.Description = Unescape(StripField(line));
                    else if (line.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase))
                    {
                        var raw = StripField(line);
                        if (line.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase))
                        {
                            currentIsAllDay = true;
                            if (TryParseDate(raw, out var d)) startDate = d;
                        }
                        else
                        {
                            if (TryParseDateTime(raw, out var dt)) startUtc = dt;
                        }
                    }
                    else if (line.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase))
                    {
                        var raw = StripField(line);
                        if (TryParseDateTime(raw, out var dt)) endUtc = dt;
                    }
                }
            }

            return EventsRepository.ImportMany(events);
        }

        // RFC5545 line unfolding: continuation lines start with whitespace.
        private static IEnumerable<string> UnfoldLines(string[] lines)
        {
            string? buf = null;
            foreach (var line in lines)
            {
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    buf += line.Substring(1);
                }
                else
                {
                    if (buf != null) yield return buf;
                    buf = line;
                }
            }
            if (buf != null) yield return buf;
        }

        private static string StripField(string line)
        {
            int colon = line.IndexOf(':');
            return colon >= 0 ? line[(colon + 1)..] : line;
        }

        private static string Escape(string s) => s
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n");

        private static string Unescape(string s) => s
            .Replace("\\n", "\n")
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\");

        private static bool TryParseDate(string s, out DateTime d)
        {
            d = default;
            if (s.Length < 8) return false;
            return DateTime.TryParseExact(s.Substring(0, 8), "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d);
        }

        private static bool TryParseDateTime(string s, out DateTime dt)
        {
            dt = default;
            // Strip TZID parameters that may have leaked through if not separated by ':'
            string core = s.EndsWith("Z") ? s[..^1] : s;
            if (DateTime.TryParseExact(core, new[] { "yyyyMMddTHHmmss", "yyyyMMddTHHmm" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out dt))
                return true;
            return false;
        }
    }
}
