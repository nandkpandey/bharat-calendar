using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BharatCalendar;

public sealed class DataStore
{
    public static readonly string Folder =
        Environment.GetEnvironmentVariable("BHARATCAL_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BharatCalendar");
    public static string HolidayFolder => Path.Combine(Folder, "holidays");
    static string DataFile => Path.Combine(Folder, "data.json");

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppData Data { get; private set; } = new();
    public List<Holiday> Holidays { get; } = new();
    public string HolidaySource { get; private set; } = "";
    readonly Dictionary<DateTime, List<Holiday>> _byDate = new();

    public void Load()
    {
        Directory.CreateDirectory(Folder);
        if (File.Exists(DataFile))
        {
            try
            {
                Data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataFile), Options) ?? new AppData();
            }
            catch
            {
                // Keep the unreadable file for recovery and start fresh.
                File.Copy(DataFile, DataFile + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}", true);
                Data = new AppData();
            }
        }
        LoadHolidays();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var tmp = DataFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, Options));
        File.Move(tmp, DataFile, true);
    }

    public void LoadHolidays()
    {
        Holidays.Clear();
        _byDate.Clear();

        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BharatCalendar.Data.holidays.json"))
        {
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var file = Parse(reader.ReadToEnd());
                HolidaySource = file.Source ?? "";
                AddRange(file);
            }
        }

        if (Directory.Exists(HolidayFolder))
        {
            foreach (var path in Directory.GetFiles(HolidayFolder, "*.json"))
            {
                try { AddRange(Parse(File.ReadAllText(path))); } catch { /* skip bad files */ }
            }
        }

        AddFixedDateHolidays();

        Holidays.Sort((a, b) => a.Date != b.Date ? a.Date.CompareTo(b.Date) : a.Kind.CompareTo(b.Kind));
        foreach (var h in Holidays)
        {
            if (!_byDate.TryGetValue(h.Date, out var list)) _byDate[h.Date] = list = new List<Holiday>();
            list.Add(h);
        }
    }

    public const int FirstYear = 1950, LastYear = 2100;

    static readonly (int Month, int Day, string Name)[] FixedDateHolidays =
    {
        (1, 26, "Republic Day"),
        (8, 15, "Independence Day"),
        (10, 2, "Mahatma Gandhi's Birthday"),
        (12, 25, "Christmas Day"),
    };

    /// <summary>Years that have an official (imported or built-in) holiday list.</summary>
    public HashSet<int> OfficialYears { get; } = new();

    public bool HasOfficialList(int year) => OfficialYears.Contains(year);

    /// <summary>
    /// For years with no official list, show the national holidays whose date never changes,
    /// so the calendar is never completely empty. An imported list for that year replaces them.
    /// </summary>
    void AddFixedDateHolidays()
    {
        OfficialYears.Clear();
        foreach (var h in Holidays) OfficialYears.Add(h.Date.Year);

        for (int year = FirstYear; year <= LastYear; year++)
        {
            if (OfficialYears.Contains(year)) continue;
            foreach (var (month, day, name) in FixedDateHolidays)
            {
                Holidays.Add(new Holiday
                {
                    Date = new DateTime(year, month, day),
                    Name = name,
                    Kind = HolidayKind.Gazetted,
                    Tradition = month == 12 ? "Christian · fixed date" : "National · fixed date",
                    IsGenerated = true
                });
            }
        }
    }

    void AddRange(HolidayFile file)
    {
        foreach (var h in ToHolidays(file))
        {
            bool duplicate = Holidays.Any(x => x.Date == h.Date &&
                string.Equals(x.Name, h.Name, StringComparison.OrdinalIgnoreCase));
            if (!duplicate) Holidays.Add(h);
        }
    }

    public IReadOnlyList<Holiday> HolidaysOn(DateTime date) =>
        _byDate.TryGetValue(date.Date, out var list) ? list : Array.Empty<Holiday>();

    /// <summary>Validates a holiday file and returns how many holidays it contains.</summary>
    public static int CountValid(string path) => ToHolidays(Parse(File.ReadAllText(path))).Count();

    static HolidayFile Parse(string json) =>
        JsonSerializer.Deserialize<HolidayFile>(json, Options) ?? new HolidayFile();

    static IEnumerable<Holiday> ToHolidays(HolidayFile file)
    {
        foreach (var dto in file.Holidays ?? new List<HolidayDto>())
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Date)) continue;
            if (!DateTime.TryParseExact(dto.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) continue;
            var kind = string.Equals(dto.Type?.Trim(), "gazetted", StringComparison.OrdinalIgnoreCase)
                ? HolidayKind.Gazetted : HolidayKind.Restricted;
            yield return new Holiday { Date = date, Name = dto.Name.Trim(), Kind = kind, Tradition = dto.Tradition?.Trim() ?? "" };
        }
    }

    sealed class HolidayFile
    {
        public string? Source { get; set; }
        public List<HolidayDto>? Holidays { get; set; }
    }

    sealed class HolidayDto
    {
        public string? Date { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Tradition { get; set; }
    }
}
