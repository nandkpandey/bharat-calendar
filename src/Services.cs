using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BharatCalendar;

/// <summary>Checks every 20 seconds for event reminders and holiday-eve notices.</summary>
public sealed class ReminderService
{
    readonly DataStore _store;
    readonly Action<string, string, DateTime> _notify;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };

    public ReminderService(DataStore store, Action<string, string, DateTime> notify)
    {
        _store = store;
        _notify = notify;
        _timer.Tick += (_, _) => Check();
    }

    public void Start()
    {
        Check();
        _timer.Start();
    }

    public void Check()
    {
        var now = DateTime.Now;
        var data = _store.Data;
        var seen = new HashSet<string>(data.Notified);
        bool changed = false;

        foreach (var ev in data.Events.ToList())
        {
            if (ev.ReminderMinutes < 0) continue;
            for (int i = -1; i <= 8; i++)
            {
                var day = DateTime.Today.AddDays(i);
                if (!ev.OccursOn(day)) continue;
                var start = day + (ev.AllDay || ev.Start == null ? TimeSpan.FromHours(9) : ev.Start.Value);
                var remindAt = start.AddMinutes(-ev.ReminderMinutes);
                if (now < remindAt || now > remindAt.AddHours(2)) continue;
                var key = $"E|{ev.Id}|{day:yyyy-MM-dd}";
                if (!seen.Add(key)) continue;
                data.Notified.Add(key);
                changed = true;
                _notify(ev.Title, Describe(ev, start), day);
            }
        }

        if (data.Settings.HolidayReminders && now >= DateTime.Today.AddHours(18))
        {
            var tomorrow = DateTime.Today.AddDays(1);
            foreach (var h in _store.HolidaysOn(tomorrow).Where(h => h.Kind == HolidayKind.Gazetted))
            {
                var key = $"H|{h.Date:yyyy-MM-dd}|{h.Name}";
                if (!seen.Add(key)) continue;
                data.Notified.Add(key);
                changed = true;
                _notify($"Holiday tomorrow: {h.Name}", $"{tomorrow:dddd, d MMMM} is a gazetted holiday.", tomorrow);
            }
        }

        if (changed)
        {
            if (data.Notified.Count > 400) data.Notified.RemoveRange(0, data.Notified.Count - 400);
            _store.Save();
        }
    }

    static string Describe(CalendarEvent ev, DateTime start)
    {
        string when = start.Date == DateTime.Today ? "Today"
            : start.Date == DateTime.Today.AddDays(1) ? "Tomorrow"
            : start.ToString("dddd, d MMM");
        var text = ev.AllDay ? $"{when} · All day" : $"{when} at {start:h:mm tt}";
        var note = ev.Notes.Split('\n').FirstOrDefault()?.Trim();
        return string.IsNullOrEmpty(note) ? text : $"{text}\n{note}";
    }
}

public static class StartupHelper
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "BharatCalendar";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) != null;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimized");
        else key.DeleteValue(ValueName, false);
    }
}

/// <summary>Writes holidays and events to an iCalendar (.ics) file for Outlook / Google Calendar.</summary>
public static class IcsExporter
{
    public static void Export(string path, IEnumerable<Holiday> holidays, IEnumerable<CalendarEvent> events)
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append("\r\n");
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Bharat Calendar//EN");
        Line("CALSCALE:GREGORIAN");
        Line("X-WR-CALNAME:Bharat Calendar");

        foreach (var h in holidays)
        {
            Line("BEGIN:VEVENT");
            Line($"UID:holiday-{h.Date:yyyyMMdd}-{Math.Abs(h.Name.GetHashCode()):x}@bharatcalendar");
            Line($"DTSTAMP:{stamp}");
            Line($"DTSTART;VALUE=DATE:{h.Date:yyyyMMdd}");
            Line($"DTEND;VALUE=DATE:{h.Date.AddDays(1):yyyyMMdd}");
            Line($"SUMMARY:{Esc(h.Name)}");
            var kind = h.Kind == HolidayKind.Gazetted ? "Gazetted holiday" : "Restricted holiday";
            Line($"DESCRIPTION:{Esc($"{kind} (Government of India) · {h.Tradition}")}");
            Line("TRANSP:TRANSPARENT");
            Line("END:VEVENT");
        }

        foreach (var e in events)
        {
            Line("BEGIN:VEVENT");
            Line($"UID:{e.Id}@bharatcalendar");
            Line($"DTSTAMP:{stamp}");
            if (e.AllDay || e.Start == null)
            {
                Line($"DTSTART;VALUE=DATE:{e.Date:yyyyMMdd}");
                Line($"DTEND;VALUE=DATE:{e.Date.AddDays(1):yyyyMMdd}");
            }
            else
            {
                var start = e.Date.Date + e.Start.Value;
                var end = e.Date.Date + (e.End ?? e.Start.Value + TimeSpan.FromHours(1));
                Line($"DTSTART:{start:yyyyMMdd'T'HHmmss}");
                Line($"DTEND:{end:yyyyMMdd'T'HHmmss}");
            }
            if (e.Repeat != Recurrence.None) Line($"RRULE:FREQ={e.Repeat.ToString().ToUpperInvariant()}");
            Line($"SUMMARY:{Esc(e.Title)}");
            if (!string.IsNullOrWhiteSpace(e.Notes)) Line($"DESCRIPTION:{Esc(e.Notes)}");
            if (e.ReminderMinutes >= 0)
            {
                Line("BEGIN:VALARM");
                Line("ACTION:DISPLAY");
                Line($"DESCRIPTION:{Esc(e.Title)}");
                Line($"TRIGGER:-PT{e.ReminderMinutes}M");
                Line("END:VALARM");
            }
            Line("END:VEVENT");
        }

        Line("END:VCALENDAR");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    static string Esc(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .Replace("\r\n", "\\n").Replace("\n", "\\n");
}
