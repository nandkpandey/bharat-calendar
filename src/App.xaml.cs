using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace BharatCalendar;

public partial class App : Application
{
    public static DataStore Store { get; } = new();
    public static bool IsExiting { get; private set; }

    Mutex? _mutex;
    EventWaitHandle? _showSignal;
    Forms.NotifyIcon? _tray;
    MainWindow? _main;
    ReminderService? _reminders;
    DateTime? _balloonDate;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance: a second launch just brings the running window forward.
        _mutex = new Mutex(true, "BharatCalendar.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            try { EventWaitHandle.OpenExisting("BharatCalendar.Show").Set(); } catch { }
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "BharatCalendar.Show");
        new Thread(() =>
        {
            while (_showSignal.WaitOne())
            {
                if (IsExiting) break;
                Dispatcher.BeginInvoke(new Action(ShowMain));
            }
        }) { IsBackground = true }.Start();

        var culture = new CultureInfo("en-IN");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Something went wrong:\n\n" + args.Exception.Message, "Bharat Calendar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        Store.Load();
        ThemeManager.Apply(Store.Data.Settings.Theme);
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == UserPreferenceCategory.General && Store.Data.Settings.Theme == "System")
                Dispatcher.BeginInvoke(new Action(() => ThemeManager.Apply("System")));
        };

        CreateTray();
        _main = new MainWindow(Store);
        if (!e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            _main.Show();

        _reminders = new ReminderService(Store, Notify);
        _reminders.Start();
    }

    void CreateTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Text = "Bharat Calendar",
            Visible = true,
            Icon = LoadTrayIcon()
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Bharat Calendar", null, (_, _) => ShowMain());
        menu.Items.Add("New event…", null, (_, _) => { ShowMain(); _main?.AddEvent(DateTime.Today); });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, a) => { if (a.Button == Forms.MouseButtons.Left) ShowMain(); };
        _tray.BalloonTipClicked += (_, _) =>
        {
            ShowMain();
            if (_balloonDate is DateTime d) _main?.Select(d);
        };
    }

    static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var info = GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (info != null)
                return new Drawing.Icon(info.Stream, Forms.SystemInformation.SmallIconSize);
        }
        catch { }
        return Drawing.SystemIcons.Application;
    }

    void Notify(string title, string text, DateTime date)
    {
        if (_tray == null) return;
        _balloonDate = date;
        _tray.ShowBalloonTip(10000, title, text, Forms.ToolTipIcon.None);
    }

    public void ShowTrayHint()
    {
        if (Store.Data.Settings.TrayHintShown) return;
        Store.Data.Settings.TrayHintShown = true;
        Store.Save();
        _tray?.ShowBalloonTip(6000, "Bharat Calendar is still running",
            "It stays in the system tray so it can remind you. Right-click the tray icon and choose Exit to close it fully.",
            Forms.ToolTipIcon.None);
    }

    public void ShowMain()
    {
        if (_main == null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
        _main.Topmost = true;
        _main.Topmost = false;
        _main.Focus();
    }

    public void ExitApp()
    {
        IsExiting = true;
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        try { Store.Save(); } catch { }
        _showSignal?.Set();
        Shutdown();
    }
}
