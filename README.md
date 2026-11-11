# Bharat Calendar

A Windows 11 desktop calendar that shows the official Government of India holidays and lets you keep your own events with reminders.

> **Disclaimer:** Bharat Calendar is an independent, unofficial project. It is not affiliated with, endorsed by, or connected to the Government of India or any of its departments. Holiday dates are taken from publicly available DoPT orders and may contain errors or change (for example, when festival dates depend on the sighting of the moon). Check official sources for anything important.

## Features

- **Official holidays.** Gazetted and restricted holidays from the Department of Personnel & Training (DoPT) lists for 2026 and 2027. They cover national days and festivals of all religions and regions: Hindu, Muslim, Christian, Sikh, Jain, Buddhist and Parsi festivals, plus regional ones such as Pongal, Bihu, Onam, Ugadi, Vaisakhi and Chhath.
- **Fixed-date holidays for other years.** Years without an official list still show Republic Day, Independence Day, Gandhi Jayanti and Christmas.
- **Your events.** Events can be all-day or timed, can repeat (daily, weekly, monthly or yearly) and have a colour and notes.
- **Reminders.** You get Windows notifications for events, and a notice the evening before each gazetted holiday. The app keeps running in the system tray so reminders still arrive after you close the window.
- **Search and filters.** Search festivals and events, show or hide each holiday type, and see an "upcoming" list for the next 60 days.
- **Themes.** Light, dark or follow-Windows, with a Windows 11 title bar to match.
- **Export.** Save holidays and events as an `.ics` file for Outlook or Google Calendar.
- **Keyboard shortcuts.** Ctrl+N new event, Ctrl+F search, arrow keys move between days, Page Up/Page Down or the mouse wheel change month, Home jumps to today.

## Install

Download `BharatCalendar-Setup-x.y.z.exe` from [Releases](../../releases) and run it. Admin rights aren't needed, and the .NET runtime is bundled. Requires 64-bit Windows 10 (1809) or Windows 11.

## Adding holidays for a new year

DoPT publishes each year's list around July of the year before. Save it as JSON and use **Settings → Import holiday list**:

```json
{
  "holidays": [
    { "date": "2028-01-26", "name": "Republic Day", "type": "gazetted", "tradition": "National" },
    { "date": "2028-01-14", "name": "Makar Sankranti", "type": "restricted", "tradition": "Hindu" }
  ]
}
```

`type` is `gazetted` or `restricted`. Imported lists are stored in `%APPDATA%\BharatCalendar\holidays`. Built-in lists are in [`src/Data/holidays.json`](src/Data/holidays.json).

Islamic festival dates depend on when the moon is sighted, so they can shift by a day. The lists are for Central Government offices in Delhi/New Delhi; offices in other states also observe 3 optional holidays chosen locally.

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
./build.ps1      # publishes a self-contained exe and builds dist\BharatCalendar-Setup-<version>.exe
```

For development:

```powershell
dotnet run --project src
```

Set the environment variable `BHARATCAL_DATA` to a folder path to use separate test data instead of `%APPDATA%\BharatCalendar`.

## Project layout

| Path | What it is |
|---|---|
| `src/MainWindow.*` | Month grid, side panel, navigation |
| `src/EventWindow.*` | Add / edit event dialog |
| `src/SettingsWindow.*` | Settings, import, export |
| `src/DataStore.cs` | Loading and saving events and holiday lists |
| `src/Services.cs` | Reminders, start-with-Windows, .ics export |
| `src/ThemeManager.cs`, `src/Styles.xaml` | Light/dark theme and control styles |
| `src/Data/holidays.json` | Official holiday lists |
| `installer/setup.iss` | Inno Setup installer script |
| `tools/make-icon.ps1` | Generates `src/app.ico` |

## Holiday data sources

DoPT Office Memoranda F.No.12/2/2023-JCA dated 03.07.2025 (holidays for 2026) and 16.07.2026 (holidays for 2027).

## Privacy

The app collects no data. Your events and settings are stored only on your own PC, in `%APPDATA%\BharatCalendar`. The app doesn't connect to the internet, and it has no accounts, analytics or tracking.

## License

[MIT](LICENSE). The installer bundles the .NET runtime (also MIT) and is built with Inno Setup; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
