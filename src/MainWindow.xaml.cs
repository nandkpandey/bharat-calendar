using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BharatCalendar;

public partial class MainWindow : Window
{
    readonly DataStore _store;
    DateTime _month;
    DateTime _selected = DateTime.Today;
    bool _suppressJump;
    DateTime _lastWheel;

    AppSettings S => _store.Data.Settings;

    public MainWindow(DataStore store)
    {
        _store = store;
        InitializeComponent();
        _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        MonthCombo.ItemsSource = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12).ToList();
        YearCombo.ItemsSource = Enumerable.Range(1950, 151).ToList();

        GazettedFilter.IsChecked = S.ShowGazetted;
        RestrictedFilter.IsChecked = S.ShowRestricted;
        EventsFilter.IsChecked = S.ShowEvents;

        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        ThemeManager.Changed += Refresh;

        // Keep "Today" correct if the app runs past midnight.
        var midnight = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        DateTime lastDay = DateTime.Today;
        midnight.Tick += (_, _) => { if (DateTime.Today != lastDay) { lastDay = DateTime.Today; Refresh(); } };
        midnight.Start();

        Refresh();
    }

    Brush Res(string key) => (Brush)FindResource(key);

    // ---------- Rendering ----------

    public void Refresh()
    {
        BuildHeader();
        BuildGrid();
        BuildSidePanel();
    }

    void BuildHeader()
    {
        MonthTitle.Text = _month.ToString("MMMM yyyy");
        var monthHolidays = _store.Holidays.Where(h => h.Date.Year == _month.Year && h.Date.Month == _month.Month).ToList();
        int gaz = monthHolidays.Count(h => h.Kind == HolidayKind.Gazetted);
        int res = monthHolidays.Count(h => h.Kind == HolidayKind.Restricted);
        MonthSubtitle.Text = _store.HasOfficialList(_month.Year)
            ? $"{gaz} gazetted · {res} restricted holiday{(res == 1 ? "" : "s")} this month"
            : $"Only fixed-date national holidays shown · official {_month.Year} list not loaded yet (Settings → Import holiday list)";

        _suppressJump = true;
        MonthCombo.SelectedIndex = _month.Month - 1;
        YearCombo.SelectedItem = _month.Year;
        _suppressJump = false;

        ThemeButton.Content = ThemeManager.IsDark ? "" : "";

        WeekHeader.Children.Clear();
        var names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
        int first = S.WeekStartsMonday ? 1 : 0;
        for (int i = 0; i < 7; i++)
        {
            int dow = (first + i) % 7;
            WeekHeader.Children.Add(new TextBlock
            {
                Text = names[dow].ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = dow == 0 ? Res("Sunday") : Res("SubText")
            });
        }
    }

    void BuildGrid()
    {
        DayGrid.Children.Clear();
        int first = S.WeekStartsMonday ? 1 : 0;
        int offset = ((int)_month.DayOfWeek - first + 7) % 7;
        var start = _month.AddDays(-offset);
        for (int i = 0; i < 42; i++)
            DayGrid.Children.Add(BuildCell(start.AddDays(i)));
    }

    FrameworkElement BuildCell(DateTime d)
    {
        bool inMonth = d.Month == _month.Month && d.Year == _month.Year;
        bool isToday = d == DateTime.Today;
        bool isSelected = d == _selected;
        var items = ItemsFor(d, true);
        bool gazetted = items.Any(i => i.Holiday?.Kind == HolidayKind.Gazetted);

        Brush normalBorder = Res("Border");
        var cell = new Border
        {
            Margin = new Thickness(3),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(5, 4, 5, 4),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            BorderBrush = isSelected ? Res("Accent") : normalBorder,
            Background = isSelected ? Res("SelectedCell") : gazetted ? Res("GazCell") : inMonth ? Res("CellBg") : Res("CellOther"),
            Cursor = Cursors.Hand,
            ClipToBounds = true,
            Tag = d
        };

        var dock = new DockPanel { LastChildFill = true };
        var top = new StackPanel { Orientation = Orientation.Horizontal };
        var number = new TextBlock
        {
            Text = d.Day.ToString(),
            FontSize = 13,
            FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = isToday ? Res("AccentText")
                : (d.DayOfWeek == DayOfWeek.Sunday || gazetted) ? Res("Sunday") : Res("Text")
        };
        top.Children.Add(new Border
        {
            Width = 26, Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = isToday ? Res("Accent") : Brushes.Transparent,
            Child = number
        });
        if (d.Day == 1)
        {
            top.Children.Add(new TextBlock
            {
                Text = d.ToString("MMM"),
                FontSize = 11,
                Foreground = Res("SubText"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });
        }
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);

        var chips = new StackPanel { Margin = new Thickness(0, 1, 0, 0) };
        const int maxChips = 3;
        foreach (var item in items.Take(maxChips)) chips.Children.Add(Chip(item));
        if (items.Count > maxChips)
        {
            chips.Children.Add(new TextBlock
            {
                Text = $"+{items.Count - maxChips} more",
                FontSize = 11,
                Foreground = Res("SubText"),
                Margin = new Thickness(4, 2, 0, 0)
            });
        }
        dock.Children.Add(chips);
        cell.Child = dock;

        if (!inMonth) cell.Opacity = 0.5;
        if (items.Count > 0)
            cell.ToolTip = d.ToString("dddd, d MMMM yyyy") + "\n" + string.Join("\n", items.Select(i => $"• {i.Title} — {i.Subtitle}"));

        cell.MouseLeftButtonDown += Cell_MouseDown;
        if (!isSelected)
        {
            cell.MouseEnter += (_, _) => cell.BorderBrush = Res("SubText");
            cell.MouseLeave += (_, _) => cell.BorderBrush = normalBorder;
        }
        return cell;
    }

    static Border Chip(DayItem item) => new()
    {
        CornerRadius = new CornerRadius(4),
        Background = item.ChipBackground,
        Padding = new Thickness(6, 1, 6, 2),
        Margin = new Thickness(0, 2, 0, 0),
        Child = new TextBlock
        {
            Text = item.ChipText,
            FontSize = 11.5,
            Foreground = item.ChipForeground,
            TextTrimming = TextTrimming.CharacterEllipsis
        }
    };

    void BuildSidePanel()
    {
        SelDayName.Text = _selected == DateTime.Today ? $"{_selected:dddd} · Today" : _selected.ToString("dddd");
        SelDate.Text = _selected.ToString("d MMMM yyyy");
        var selItems = ItemsFor(_selected, false);
        SelList.ItemsSource = selItems;
        SelEmpty.Visibility = selItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var query = SearchBox.Text.Trim();
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        List<DayItem> list;
        if (query.Length > 0)
        {
            list = Search(query);
            ListHeader.Text = $"Search results ({list.Count})";
            ListEmpty.Text = "No festivals or events match your search.";
        }
        else
        {
            list = Upcoming(60);
            ListHeader.Text = "Upcoming · next 60 days";
            ListEmpty.Text = "Nothing coming up in the next 60 days.";
        }
        UpcomingList.ItemsSource = list;
        ListEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Data for display ----------

    List<DayItem> ItemsFor(DateTime d, bool applyFilters)
    {
        var list = new List<DayItem>();
        foreach (var h in _store.HolidaysOn(d))
        {
            if (applyFilters && h.Kind == HolidayKind.Gazetted && !S.ShowGazetted) continue;
            if (applyFilters && h.Kind == HolidayKind.Restricted && !S.ShowRestricted) continue;
            list.Add(HolidayItem(h));
        }
        if (!applyFilters || S.ShowEvents)
        {
            foreach (var e in _store.Data.Events.Where(e => e.OccursOn(d))
                         .OrderBy(e => e.AllDay ? 0 : 1).ThenBy(e => e.Start))
                list.Add(EventItem(e, d));
        }
        return list;
    }

    DayItem HolidayItem(Holiday h)
    {
        bool gaz = h.Kind == HolidayKind.Gazetted;
        var kind = h.IsGenerated ? "National holiday" : gaz ? "Gazetted holiday" : "Restricted holiday";
        return new DayItem
        {
            Date = h.Date,
            Title = h.Name,
            ChipText = h.Name,
            Subtitle = string.IsNullOrEmpty(h.Tradition) ? kind : $"{kind} · {h.Tradition}",
            Accent = Res(gaz ? "GazFg" : "ResFg"),
            ChipBackground = Res(gaz ? "GazBg" : "ResBg"),
            ChipForeground = Res(gaz ? "GazFg" : "ResFg"),
            Holiday = h
        };
    }

    static DayItem EventItem(CalendarEvent e, DateTime day)
    {
        Brush color;
        try { color = ThemeManager.MakeBrush(e.Color); } catch { color = ThemeManager.MakeBrush("#2563EB"); }
        string time = e.AllDay || e.Start == null
            ? "All day"
            : $"{FormatTime(e.Start.Value)} – {FormatTime(e.End ?? e.Start.Value)}";
        var parts = new List<string> { time };
        if (e.Repeat != Recurrence.None) parts.Add($"Repeats {e.Repeat.ToString().ToLowerInvariant()}");
        if (e.ReminderMinutes >= 0) parts.Add("Reminder on");
        return new DayItem
        {
            Date = day,
            Title = e.Title,
            ChipText = e.AllDay || e.Start == null ? e.Title : $"{FormatTime(e.Start.Value)} {e.Title}",
            Subtitle = string.Join(" · ", parts),
            Accent = color,
            ChipBackground = color,
            ChipForeground = Brushes.White,
            Event = e
        };
    }

    public static string FormatTime(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt");

    List<DayItem> Upcoming(int days)
    {
        var list = new List<DayItem>();
        for (int i = 0; i <= days && list.Count < 80; i++)
            list.AddRange(ItemsFor(DateTime.Today.AddDays(i), true));
        return list;
    }

    List<DayItem> Search(string q)
    {
        bool Match(string? s) => s != null && s.Contains(q, StringComparison.OrdinalIgnoreCase);
        // Fixed-date holidays exist for every year up to 2100; only search the next few years of them.
        int thisYear = DateTime.Today.Year;
        var list = _store.Holidays
            .Where(h => !h.IsGenerated || (h.Date.Year >= thisYear && h.Date.Year <= thisYear + 3))
            .Where(h => Match(h.Name) || Match(h.Tradition)).Select(HolidayItem).ToList();
        list.AddRange(_store.Data.Events.Where(e => Match(e.Title) || Match(e.Notes))
            .Select(e => EventItem(e, e.NextOccurrence(DateTime.Today))));
        // Upcoming matches first, then past ones (most recent first).
        var today = DateTime.Today;
        return list.Where(i => i.Date >= today).OrderBy(i => i.Date)
            .Concat(list.Where(i => i.Date < today).OrderByDescending(i => i.Date))
            .Take(150).ToList();
    }

    // ---------- Navigation ----------

    public void Select(DateTime d)
    {
        _selected = d.Date;
        _month = new DateTime(d.Year, d.Month, 1);
        Refresh();
    }

    void ChangeMonth(int delta)
    {
        var m = _month.AddMonths(delta);
        if (m.Year < 1950 || m.Year > 2100) return;
        int day = Math.Min(_selected.Day, DateTime.DaysInMonth(m.Year, m.Month));
        Select(new DateTime(m.Year, m.Month, day));
    }

    void Cell_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var d = (DateTime)((FrameworkElement)sender).Tag;
        Select(d);
        if (e.ClickCount == 2) AddEvent(d);
    }

    void Item_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DayItem item) return;
        if (item.Event != null) EditEvent(item.Event);
        else Select(item.Date);
    }

    void Prev_Click(object sender, RoutedEventArgs e) => ChangeMonth(-1);
    void Next_Click(object sender, RoutedEventArgs e) => ChangeMonth(1);
    void Today_Click(object sender, RoutedEventArgs e) => Select(DateTime.Today);

    void Jump_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressJump || MonthCombo.SelectedIndex < 0 || YearCombo.SelectedItem is not int year) return;
        int month = MonthCombo.SelectedIndex + 1;
        Select(new DateTime(year, month, Math.Min(_selected.Day, DateTime.DaysInMonth(year, month))));
    }

    void DayGrid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((DateTime.Now - _lastWheel).TotalMilliseconds < 180) return;
        _lastWheel = DateTime.Now;
        ChangeMonth(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    void Search_Changed(object sender, TextChangedEventArgs e) => BuildSidePanel();

    void Filter_Click(object sender, RoutedEventArgs e)
    {
        S.ShowGazetted = GazettedFilter.IsChecked == true;
        S.ShowRestricted = RestrictedFilter.IsChecked == true;
        S.ShowEvents = EventsFilter.IsChecked == true;
        _store.Save();
        Refresh();
    }

    void Theme_Click(object sender, RoutedEventArgs e)
    {
        S.Theme = ThemeManager.IsDark ? "Light" : "Dark";
        _store.Save();
        ThemeManager.Apply(S.Theme);
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(_store, () =>
        {
            GazettedFilter.IsChecked = S.ShowGazetted;
            RestrictedFilter.IsChecked = S.ShowRestricted;
            EventsFilter.IsChecked = S.ShowEvents;
            Refresh();
        }) { Owner = this }.ShowDialog();
    }

    // ---------- Events ----------

    void AddEvent_Click(object sender, RoutedEventArgs e) => AddEvent(_selected);

    public void AddEvent(DateTime date)
    {
        var ev = new CalendarEvent { Date = date.Date, AllDay = false, ReminderMinutes = 15 };
        var win = new EventWindow(ev, true) { Owner = IsVisible ? this : null };
        if (win.ShowDialog() == true)
        {
            _store.Data.Events.Add(ev);
            _store.Save();
            Select(ev.Date);
        }
    }

    void EditEvent(CalendarEvent ev)
    {
        var win = new EventWindow(ev, false) { Owner = this };
        if (win.ShowDialog() != true) return;
        if (win.Deleted) _store.Data.Events.Remove(ev);
        _store.Save();
        Refresh();
    }

    // ---------- Keyboard & window ----------

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (ctrl && e.Key == Key.N) { AddEvent(_selected); e.Handled = true; return; }
        if (ctrl && e.Key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; return; }
        if (e.Key == Key.Escape && SearchBox.Text.Length > 0) { SearchBox.Text = ""; e.Handled = true; return; }

        var focused = Keyboard.FocusedElement;
        if (focused is TextBox || focused is ComboBox || focused is ComboBoxItem) return;

        switch (e.Key)
        {
            case Key.PageUp: ChangeMonth(-1); break;
            case Key.PageDown: ChangeMonth(1); break;
            case Key.Left: Select(_selected.AddDays(-1)); break;
            case Key.Right: Select(_selected.AddDays(1)); break;
            case Key.Up: Select(_selected.AddDays(-7)); break;
            case Key.Down: Select(_selected.AddDays(7)); break;
            case Key.Home: Select(DateTime.Today); break;
            case Key.Enter: AddEvent(_selected); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        var app = (App)Application.Current;
        if (!App.IsExiting && S.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            app.ShowTrayHint();
            return;
        }
        if (!App.IsExiting) app.ExitApp();
    }
}
