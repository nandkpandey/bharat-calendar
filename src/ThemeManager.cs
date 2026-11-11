using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace BharatCalendar;

public static class ThemeManager
{
    public static bool IsDark { get; private set; }
    public static event Action? Changed;

    static readonly Dictionary<string, (string Light, string Dark)> Palette = new()
    {
        ["Bg"] = ("#F6F6F8", "#161619"),
        ["Surface"] = ("#FFFFFF", "#1F1F23"),
        ["Surface2"] = ("#F1F1F4", "#29292E"),
        ["Border"] = ("#E3E3E8", "#34343B"),
        ["Hover"] = ("#E6E6EB", "#35353C"),
        ["Text"] = ("#18181B", "#F4F4F5"),
        ["SubText"] = ("#666672", "#A1A1AA"),
        ["Accent"] = ("#D9480F", "#FF8A3D"),
        ["AccentHover"] = ("#B93D0C", "#FFA066"),
        ["AccentText"] = ("#FFFFFF", "#1B1208"),
        ["CellBg"] = ("#FFFFFF", "#1E1E22"),
        ["CellOther"] = ("#F6F6F8", "#18181B"),
        ["SelectedCell"] = ("#FFF3EB", "#2D2219"),
        ["Sunday"] = ("#C81E1E", "#F87171"),
        ["GazBg"] = ("#FDE3E0", "#4A1E1C"),
        ["GazFg"] = ("#B42318", "#FCA5A5"),
        ["GazCell"] = ("#FFF7F6", "#251B1C"),
        ["ResBg"] = ("#FDEFC8", "#40300E"),
        ["ResFg"] = ("#8A4B08", "#FCD34D"),
        ["ScrollThumb"] = ("#C4C4CC", "#4A4A52"),
    };

    public static void Apply(string mode)
    {
        IsDark = mode == "Dark" || (mode == "System" && SystemIsDark());
        var res = Application.Current.Resources;
        foreach (var (key, value) in Palette)
            res[key] = MakeBrush(IsDark ? value.Dark : value.Light);

        foreach (Window w in Application.Current.Windows)
            ApplyTitleBar(w);
        Changed?.Invoke();
    }

    public static SolidColorBrush MakeBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_CAPTION_COLOR = 35;
    const int DWMWA_TEXT_COLOR = 36;

    /// <summary>Colours the Windows 11 title bar to match the app theme.</summary>
    public static void ApplyTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int dark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            var bg = ((SolidColorBrush)Application.Current.Resources[
                window is MainWindow ? "Bg" : "Surface"]).Color;
            var fg = ((SolidColorBrush)Application.Current.Resources["Text"]).Color;
            int caption = bg.R | (bg.G << 8) | (bg.B << 16);
            int text = fg.R | (fg.G << 8) | (fg.B << 16);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
        catch { /* older Windows: ignore */ }
    }
}
