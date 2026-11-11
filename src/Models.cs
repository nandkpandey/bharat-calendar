using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace BharatCalendar;

public enum HolidayKind { Gazetted, Restricted }

public enum Recurrence { None, Daily, Weekly, Monthly, Yearly }

public sealed class Holiday
{
    public DateTime Date { get; init; }
    public string Name { get; init; } = "";
    public HolidayKind Kind { get; init; }
    public string Tradition { get; init; } = "";
    /// <summary>True for fixed-date national holidays added automatically to years without an official list.</summary>
    public bool IsGenerated { get; init; }
}

public sealed class CalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public bool AllDay { get; set; } = true;
    public TimeSpan? Start { get; set; }
    public TimeSpan? End { get; set; }
    public string Notes { get; set; } = "";
    public string Color { get; set; } = "#2563EB";
    /// <summary>Minutes before start; -1 means no reminder.</summary>
    public int ReminderMinutes { get; set; } = -1;
    public Recurrence Repeat { get; set; } = Recurrence.None;

    public bool OccursOn(DateTime day)
    {
        day = day.Date;
        var start = Date.Date;
        if (day < start) return false;
        return Repeat switch
        {
            Recurrence.None => day == start,
            Recurrence.Daily => true,
            Recurrence.Weekly => (day - start).Days % 7 == 0,
            Recurrence.Monthly => day.Day == start.Day,
            Recurrence.Yearly => day.Month == start.Month && day.Day == start.Day,
            _ => false
        };
    }

    public DateTime NextOccurrence(DateTime from)
    {
        if (Repeat == Recurrence.None) return Date.Date;
        for (int i = 0; i < 1500; i++)
        {
            var d = from.Date.AddDays(i);
            if (OccursOn(d)) return d;
        }
        return Date.Date;
    }
}

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public bool ShowGazetted { get; set; } = true;
    public bool ShowRestricted { get; set; } = true;
    public bool ShowEvents { get; set; } = true;
    public bool HolidayReminders { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool WeekStartsMonday { get; set; }
    public bool TrayHintShown { get; set; }
}

public sealed class AppData
{
    public AppSettings Settings { get; set; } = new();
    public List<CalendarEvent> Events { get; set; } = new();
    public List<string> Notified { get; set; } = new();
}

/// <summary>A row shown in the calendar or side panel (a holiday or one occurrence of an event).</summary>
public sealed class DayItem
{
    public DateTime Date { get; init; }
    public string Title { get; init; } = "";
    public string ChipText { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public Brush Accent { get; init; } = Brushes.Gray;
    public Brush ChipBackground { get; init; } = Brushes.Gray;
    public Brush ChipForeground { get; init; } = Brushes.White;
    public CalendarEvent? Event { get; init; }
    public Holiday? Holiday { get; init; }

    public bool IsEvent => Event != null;
    public string DateText => Date.ToString("ddd, d MMM yyyy");
    public string DatedSubtitle => $"{DateText}  ·  {Subtitle}";

    public string WhenText
    {
        get
        {
            int days = (Date.Date - DateTime.Today).Days;
            return days switch
            {
                0 => "Today",
                1 => "Tomorrow",
                -1 => "Yesterday",
                > 1 => $"in {days} days",
                _ => $"{-days} days ago"
            };
        }
    }
}
