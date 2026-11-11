using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BharatCalendar;

public partial class SettingsWindow : Window
{
    readonly DataStore _store;
    readonly Action _onChanged;
    readonly bool _loading = true;

    AppSettings S => _store.Data.Settings;

    public SettingsWindow(DataStore store, Action onChanged)
    {
        InitializeComponent();
        _store = store;
        _onChanged = onChanged;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);

        ThemeBox.ItemsSource = new[] { "System", "Light", "Dark" };
        ThemeBox.SelectedItem = S.Theme;
        MondayBox.IsChecked = S.WeekStartsMonday;
        HolidayRemBox.IsChecked = S.HolidayReminders;
        TrayBox.IsChecked = S.CloseToTray;
        try { StartupBox.IsChecked = StartupHelper.IsEnabled(); } catch { StartupBox.IsEnabled = false; }
        UpdateHolidayInfo();
        _loading = false;
    }

    void UpdateHolidayInfo()
    {
        var h = _store.Holidays.Where(x => !x.IsGenerated).ToList();
        var years = _store.OfficialYears.OrderBy(y => y).ToList();
        HolidayInfo.Text = h.Count == 0
            ? "No official holiday lists loaded."
            : $"{h.Count} official holidays loaded for {string.Join(", ", years)}. " +
              "Other years show only the fixed-date national holidays (Republic Day, Independence Day, " +
              "Gandhi Jayanti, Christmas) until you import that year's list.";
        SourceInfo.Text = _store.HolidaySource;
    }

    void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not string theme) return;
        S.Theme = theme;
        _store.Save();
        ThemeManager.Apply(theme);
    }

    void Option_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        S.WeekStartsMonday = MondayBox.IsChecked == true;
        S.HolidayReminders = HolidayRemBox.IsChecked == true;
        S.CloseToTray = TrayBox.IsChecked == true;
        if (sender == StartupBox)
        {
            try { StartupHelper.Set(StartupBox.IsChecked == true); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not change the startup setting:\n" + ex.Message, "Settings");
                StartupBox.IsChecked = StartupHelper.IsEnabled();
            }
        }
        _store.Save();
        _onChanged();
    }

    void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Holiday list (*.json)|*.json", Title = "Import holiday list" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            int count = DataStore.CountValid(dlg.FileName);
            if (count == 0)
            {
                MessageBox.Show(this, "No valid holidays were found in that file. Check the format shown in Settings.",
                    "Import holiday list", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Directory.CreateDirectory(DataStore.HolidayFolder);
            File.Copy(dlg.FileName, Path.Combine(DataStore.HolidayFolder, Path.GetFileName(dlg.FileName)), true);
            _store.LoadHolidays();
            UpdateHolidayInfo();
            _onChanged();
            MessageBox.Show(this, $"Imported {count} holidays.", "Import holiday list",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not read that file:\n" + ex.Message, "Import holiday list",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "iCalendar file (*.ics)|*.ics",
            FileName = "Bharat Calendar.ics",
            Title = "Export holidays and events"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            // Export official lists only; fixed-date holidays for every year to 2100 would flood the file.
            IcsExporter.Export(dlg.FileName, _store.Holidays.Where(x => !x.IsGenerated), _store.Data.Events);
            MessageBox.Show(this, "Saved. Open the file to add it to Outlook, or import it in Google Calendar.",
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save the file:\n" + ex.Message, "Export",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DataStore.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DataStore.Folder}\"") { UseShellExecute = true });
    }

    void Done_Click(object sender, RoutedEventArgs e) => Close();
}
