using System;
using System.Collections.Generic;
using System.Text;
using ItimHebrewCalendar.Models;
using Microsoft.Windows.AppNotifications;

namespace ItimHebrewCalendar.Services
{
    public static class NotificationDispatcher
    {
        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered) return;
            try
            {
                AppNotificationManager.Default.Register();
                _registered = true;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("NotificationDispatcher.Register", ex);
            }
        }

        public static void Unregister()
        {
            if (!_registered) return;
            try
            {
                AppNotificationManager.Default.Unregister();
                _registered = false;
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("NotificationDispatcher.Unregister", ex);
            }
        }

        // Show a single immediate toast for one or more reminder occurrences. Multiple
        // occurrences are merged into a single toast per the spec.
        public static void Show(IReadOnlyList<ReminderOccurrence> occurrences)
        {
            if (occurrences.Count == 0) return;
            EnsureRegistered();

            try
            {
                var xml = BuildXml(occurrences, isMissedSummary: false);
                var notif = new AppNotification(xml);
                AppNotificationManager.Default.Show(notif);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("NotificationDispatcher.Show", ex);
            }
        }

        // Surface a "new release available" toast from the auto-update checker.
        public static void ShowUpdateAvailable(UpdateChecker.ReleaseInfo release)
        {
            EnsureRegistered();
            try
            {
                var ver = release.ParsedVersion?.ToString() ?? release.TagName;
                var title = "עדכון זמין";
                var body = $"גרסה {ver} זמינה להורדה. פתח את ההגדרות כדי להתקין.";

                var sb = new StringBuilder();
                sb.Append("<toast>");
                sb.Append("<visual><binding template='ToastGeneric'>");
                sb.Append("<text>").Append(Esc(title)).Append("</text>");
                sb.Append("<text>").Append(Esc(body)).Append("</text>");
                sb.Append("<text placement='attribution'>עיתים</text>");
                sb.Append("</binding></visual>");
                sb.Append("</toast>");

                AppNotificationManager.Default.Show(new AppNotification(sb.ToString()));
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("NotificationDispatcher.ShowUpdateAvailable", ex);
            }
        }

        // One-shot summary of reminders that fired while the app was off.
        public static void ShowMissedSummary(IReadOnlyList<ReminderOccurrence> missed)
        {
            if (missed.Count == 0) return;
            EnsureRegistered();
            try
            {
                var xml = BuildXml(missed, isMissedSummary: true);
                var notif = new AppNotification(xml);
                AppNotificationManager.Default.Show(notif);
            }
            catch (Exception ex)
            {
                SettingsManager.LogError("NotificationDispatcher.ShowMissedSummary", ex);
            }
        }

        private static string BuildXml(IReadOnlyList<ReminderOccurrence> items, bool isMissedSummary)
        {
            string title;
            string body;

            if (isMissedSummary)
            {
                title = items.Count == 1
                    ? "תזכורת שהוחמצה"
                    : $"{items.Count} תזכורות שהוחמצו";
                body = BuildMissedBody(items);
            }
            else if (items.Count == 1)
            {
                var only = items[0];
                title = only.SourceTitle;
                body = BuildSingleBody(only);
            }
            else
            {
                title = $"{items.Count} תזכורות";
                body = BuildMultipleBody(items);
            }

            // AppNotification accepts toast XML schema. Keep it minimal — text + attribution.
            var sb = new StringBuilder();
            sb.Append("<toast>");
            sb.Append("<visual><binding template='ToastGeneric'>");
            sb.Append("<text>").Append(Esc(title)).Append("</text>");
            sb.Append("<text>").Append(Esc(body)).Append("</text>");
            sb.Append("<text placement='attribution'>עיתים</text>");
            sb.Append("</binding></visual>");
            sb.Append("</toast>");
            return sb.ToString();
        }

        private static string BuildSingleBody(ReminderOccurrence o)
        {
            var sb = new StringBuilder();
            if (o.EventStart.HasValue)
                sb.Append("בשעה ").Append(o.EventStart.Value.ToString("HH:mm"));
            if (!string.IsNullOrEmpty(o.AnchorLabel))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(o.AnchorLabel);
            }
            if (!string.IsNullOrEmpty(o.SourceDescription))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(o.SourceDescription);
            }
            if (sb.Length == 0) sb.Append("תזכורת");
            return sb.ToString();
        }

        private static string BuildMultipleBody(IReadOnlyList<ReminderOccurrence> items)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append("• ").Append(items[i].SourceTitle);
                if (!string.IsNullOrEmpty(items[i].AnchorLabel))
                    sb.Append(" — ").Append(items[i].AnchorLabel);
            }
            return sb.ToString();
        }

        private static string BuildMissedBody(IReadOnlyList<ReminderOccurrence> items)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count && i < 6; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append("• ")
                  .Append(items[i].FireAt.ToString("HH:mm"))
                  .Append(" — ")
                  .Append(items[i].SourceTitle);
            }
            if (items.Count > 6)
                sb.Append("\n…").Append(items.Count - 6).Append(" נוספות");
            return sb.ToString();
        }

        private static string Esc(string s) => s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
