using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BharatCalendar;

public partial class EventWindow : Window
{
    static readonly string[] Palette =
        { "#2563EB", "#16A34A", "#9333EA", "#DB2777", "#0D9488", "#EA580C", "#B45309", "#4B5563" };

    static readonly (string Label, int Minutes)[] Reminders =
    {
        ("No reminder", -1), ("At start time", 0), ("5 minutes before", 5), ("15 minutes before", 15),
        ("30 minutes before", 30), ("1 hour before", 60), ("2 hours before", 120),
        ("1 day before", 1440), ("2 days before", 2880), ("1 week before", 10080)
    };

    static readonly List<TimeSpan> Times = Enumerable.Range(0, 96).Select(i => TimeSpan.FromMinutes(i * 15)).ToList();

    readonly CalendarEvent _ev;
    bool _loading = true;

    public bool Deleted { get; private set; }

    public EventWindow(CalendarEvent ev, bool isNew)
    {
        InitializeComponent();
        _ev = ev;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);

        Header.Text = Title = isNew ? "New event" : "Edit event";
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;

        MonthBox.ItemsSource = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12).ToList();
        YearBox.ItemsSource = Enumerable.Range(1950, 151).ToList();
        var labels = Times.Select(MainWindow.FormatTime).ToList();
        StartBox.ItemsSource = labels;
        EndBox.ItemsSource = labels;
        RepeatBox.ItemsSource = new[] { "Does not repeat", "Every day", "Every week", "Every month", "Every year" };
        ReminderBox.ItemsSource = Reminders.Select(r => r.Label).ToList();

        TitleBox.Text = ev.Title;
        NotesBox.Text = ev.Notes;
        MonthBox.SelectedIndex = ev.Date.Month - 1;
        YearBox.SelectedItem = ev.Date.Year;
        FillDays(ev.Date.Day);

        AllDayBox.IsChecked = ev.AllDay;
        var start = ev.Start ?? DefaultStart(ev.Date);
        var end = ev.End ?? (start + TimeSpan.FromHours(1));
        StartBox.SelectedIndex = IndexOf(start);
        EndBox.SelectedIndex = Math.Max(IndexOf(end), StartBox.SelectedIndex);
        RepeatBox.SelectedIndex = (int)ev.Repeat;
        int ri = Array.FindIndex(Reminders, r => r.Minutes == ev.ReminderMinutes);
        ReminderBox.SelectedIndex = ri < 0 ? 0 : ri;

        var palette = Palette.Contains(ev.Color, StringComparer.OrdinalIgnoreCase) ? Palette : Palette.Append(ev.Color).ToArray();
        foreach (var hex in palette)
        {
            ColorPanel.Children.Add(new RadioButton
            {
                GroupName = "EventColour",
                Tag = hex,
                Template = (ControlTemplate)FindResource("SwatchTemplate"),
                Background = ThemeManager.MakeBrush(hex),
                IsChecked = string.Equals(hex, ev.Color, StringComparison.OrdinalIgnoreCase),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0)
            });
        }

        _loading = false;
        UpdateTimeRow();
        UpdateWeekday();
        Loaded += (_, _) => { TitleBox.Focus(); TitleBox.SelectAll(); };
    }

    static TimeSpan DefaultStart(DateTime date)
    {
        if (date.Date == DateTime.Today && DateTime.Now.Hour < 23)
            return TimeSpan.FromHours(DateTime.Now.Hour + 1);
        return TimeSpan.FromHours(10);
    }

    static int IndexOf(TimeSpan t) => Math.Clamp((int)(t.TotalMinutes / 15), 0, Times.Count - 1);

    void FillDays(int preferredDay)
    {
        if (YearBox.SelectedItem is not int year || MonthBox.SelectedIndex < 0) return;
        int days = DateTime.DaysInMonth(year, MonthBox.SelectedIndex + 1);
        DayBox.ItemsSource = Enumerable.Range(1, days).ToList();
        DayBox.SelectedIndex = Math.Min(preferredDay, days) - 1;
    }

    DateTime? SelectedDate =>
        YearBox.SelectedItem is int y && MonthBox.SelectedIndex >= 0 && DayBox.SelectedIndex >= 0
            ? new DateTime(y, MonthBox.SelectedIndex + 1, DayBox.SelectedIndex + 1)
            : null;

    void UpdateWeekday() => WeekdayText.Text = SelectedDate?.ToString("dddd") ?? "";

    void MonthYear_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        FillDays(DayBox.SelectedIndex + 1);
        UpdateWeekday();
    }

    void DayPart_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) UpdateWeekday();
    }

    void AllDay_Click(object sender, RoutedEventArgs e) => UpdateTimeRow();

    void UpdateTimeRow() =>
        TimeRow.Visibility = AllDayBox.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;

    void Start_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (EndBox.SelectedIndex <= StartBox.SelectedIndex)
            EndBox.SelectedIndex = Math.Min(StartBox.SelectedIndex + 4, Times.Count - 1);
    }

    void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        if (title.Length == 0) { ShowError("Please enter a title."); TitleBox.Focus(); return; }
        if (SelectedDate is not DateTime date) { ShowError("Please choose a date."); return; }

        bool allDay = AllDayBox.IsChecked == true;
        TimeSpan? start = null, end = null;
        if (!allDay)
        {
            start = Times[StartBox.SelectedIndex];
            end = Times[EndBox.SelectedIndex];
            if (end <= start) { ShowError("The end time must be after the start time."); return; }
        }

        _ev.Title = title;
        _ev.Date = date;
        _ev.AllDay = allDay;
        _ev.Start = start;
        _ev.End = end;
        _ev.Notes = NotesBox.Text.Trim();
        _ev.Repeat = (Recurrence)Math.Max(0, RepeatBox.SelectedIndex);
        _ev.ReminderMinutes = Reminders[Math.Max(0, ReminderBox.SelectedIndex)].Minutes;
        _ev.Color = ColorPanel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as string
                    ?? Palette[0];
        DialogResult = true;
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        var extra = _ev.Repeat != Recurrence.None ? "\n\nThis is a repeating event; all repeats will be removed." : "";
        var answer = MessageBox.Show(this, $"Delete \"{_ev.Title}\"?{extra}", "Delete event",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        Deleted = true;
        DialogResult = true;
    }
}
