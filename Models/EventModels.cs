using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ItimHebrewCalendar.Models
{
    public enum ZmanimKey
    {
        AlotHaShachar,
        Misheyakir,
        MisheyakirMachmir,
        Sunrise,
        SofZmanShmaMGA,
        SofZmanShma,
        SofZmanTfillaMGA,
        SofZmanTfilla,
        Chatzot,
        MinchaGedola,
        MinchaKetana,
        PlagHaMincha,
        Sunset,
        Tzeit,
        Tzeit72,
        CandleLighting18
    }

    public enum ReminderAnchorKind
    {
        FixedOffset,
        Zman,
        AbsoluteDateTime
    }

    public enum ZmanCombination
    {
        Earliest,
        All
    }

    public enum RecurrenceKind
    {
        None,
        DailyGregorian,
        WeeklyGregorian,
        MonthlyGregorian,
        YearlyGregorian,
        MonthlyHebrew,
        YearlyHebrew
    }

    public enum LeapMonthPolicy
    {
        Skip,
        ShiftToNextAvailable
    }

    [Flags]
    public enum DaysOfWeek
    {
        None = 0,
        Sunday    = 1 << 0,
        Monday    = 1 << 1,
        Tuesday   = 1 << 2,
        Wednesday = 1 << 3,
        Thursday  = 1 << 4,
        Friday    = 1 << 5,
        Saturday  = 1 << 6,
        Weekdays  = Sunday | Monday | Tuesday | Wednesday | Thursday,
        Weekend   = Friday | Saturday,
        All       = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday
    }

    public class HebrewDateRef
    {
        [JsonPropertyName("year")]  public int Year  { get; set; }
        [JsonPropertyName("month")] public int Month { get; set; }
        [JsonPropertyName("day")]   public int Day   { get; set; }
    }

    public class ZmanimAnchor
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("zman")]
        public ZmanimKey Zman { get; set; }
    }

    public class ReminderRule
    {
        [JsonPropertyName("id")] public Guid Id { get; set; } = Guid.NewGuid();

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("anchorKind")]
        public ReminderAnchorKind AnchorKind { get; set; } = ReminderAnchorKind.FixedOffset;

        [JsonPropertyName("zmanAnchors")]
        public List<ZmanimAnchor> ZmanAnchors { get; set; } = new();

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("zmanCombination")]
        public ZmanCombination ZmanCombination { get; set; } = ZmanCombination.Earliest;

        [JsonPropertyName("offsetMinutes")] public int OffsetMinutes { get; set; }

        // Used only when AnchorKind == AbsoluteDateTime: a specific local date+time
        // at which the reminder fires, independent of the event's own date.
        [JsonPropertyName("absoluteWhen")] public DateTime? AbsoluteWhen { get; set; }

        [JsonPropertyName("enabled")]       public bool Enabled { get; set; } = true;
    }

    public class RecurrenceRule
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("kind")]
        public RecurrenceKind Kind { get; set; } = RecurrenceKind.None;

        [JsonPropertyName("interval")] public int Interval { get; set; } = 1;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("weekdays")]
        public DaysOfWeek? Weekdays { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("leapPolicy")]
        public LeapMonthPolicy LeapPolicy { get; set; } = LeapMonthPolicy.Skip;

        [JsonPropertyName("until")] public DateTime? Until { get; set; }
        [JsonPropertyName("count")] public int? Count { get; set; }
    }

    public class ExternalSyncRef
    {
        [JsonPropertyName("windowsAppointmentId")] public string? WindowsAppointmentId { get; set; }
        [JsonPropertyName("lastSyncedUtc")]        public DateTime? LastSyncedUtc { get; set; }
    }

    public class UserEvent
    {
        [JsonPropertyName("id")] public Guid Id { get; set; } = Guid.NewGuid();
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }

        // Exactly one of StartGregorian / StartHebrew is the canonical source;
        // EventsRepository fills the other on load.
        [JsonPropertyName("startGregorian")] public DateTime? StartGregorian { get; set; }
        [JsonPropertyName("startHebrew")]    public HebrewDateRef? StartHebrew { get; set; }

        [JsonPropertyName("startTime")] public TimeSpan? StartTime { get; set; }
        [JsonPropertyName("duration")]  public TimeSpan? Duration  { get; set; }

        [JsonPropertyName("recurrence")] public RecurrenceRule? Recurrence { get; set; }
        [JsonPropertyName("reminders")]  public List<ReminderRule> Reminders { get; set; } = new();

        [JsonPropertyName("colorTag")] public string? ColorTag { get; set; }

        [JsonPropertyName("createdUtc")]  public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        [JsonPropertyName("modifiedUtc")] public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("externalRef")] public ExternalSyncRef? ExternalRef { get; set; }

        [JsonPropertyName("isImported")] public bool IsImported { get; set; }

        [JsonIgnore]
        public bool IsAllDay => StartTime == null;

        [JsonIgnore]
        public bool HasZmanAnchor
        {
            get
            {
                foreach (var r in Reminders)
                    if (r.Enabled && r.AnchorKind == ReminderAnchorKind.Zman)
                        return true;
                return false;
            }
        }
    }

    public class StandaloneZmanReminder
    {
        [JsonPropertyName("id")] public Guid Id { get; set; } = Guid.NewGuid();
        [JsonPropertyName("label")] public string Label { get; set; } = "";

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("zman")]
        public ZmanimKey Zman { get; set; }

        [JsonPropertyName("offsetMinutes")] public int OffsetMinutes { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("activeDays")]
        public DaysOfWeek ActiveDays { get; set; } = DaysOfWeek.All;

        [JsonPropertyName("skipShabbatYomTov")] public bool SkipShabbatYomTov { get; set; }
        [JsonPropertyName("enabled")]           public bool Enabled { get; set; } = true;
    }

    // A single resolved firing of a reminder, used by the scheduler & dispatcher.
    public class ReminderOccurrence
    {
        public Guid SourceId { get; set; }
        public string SourceTitle { get; set; } = "";
        public string? SourceDescription { get; set; }
        public DateTimeOffset FireAt { get; set; }
        public ReminderOccurrenceKind Kind { get; set; }
        public string? AnchorLabel { get; set; }   // e.g. "10 דק' לפני הזריחה"
        public DateTimeOffset? EventStart { get; set; }   // null for standalone zman reminders
    }

    public enum ReminderOccurrenceKind
    {
        UserEvent,
        StandaloneZman
    }
}
