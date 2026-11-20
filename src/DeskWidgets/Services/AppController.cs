using System.Drawing;
using DeskWidgets.Controls;
using DeskWidgets.Forms;
using DeskWidgets.Models;
using Microsoft.Win32;

namespace DeskWidgets.Services;

/// <summary>
/// The application "brain": owns the settings, the dashboard window, the settings window and the
/// tray icon, and is the single place where settings are changed, applied and saved.
/// </summary>
internal sealed class AppController : ApplicationContext
{
    private readonly SynchronizationContext _ui;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu = new();
    private readonly Icon? _trayIcon;
    private ToolStripMenuItem _miShow = null!, _miHide = null!, _miEdit = null!, _miLock = null!;
    private SettingsForm? _settingsForm;
    private bool _disposed;

    public AppSettings Settings { get; private set; }
    public SettingsService Store { get; }
    public DashboardForm Dashboard { get; private set; }
    public bool EditMode { get; private set; }
    public bool IsExiting { get; private set; }

    /// <summary>Raised when settings were changed from outside the settings window (drag, tray, dialogs).</summary>
    public event Action? SettingsRefreshed;

    /// <summary>True when the user may move or resize the dashboard right now.</summary>
    public bool CanTransform => !Settings.LockPosition || EditMode;

    public AppController()
    {
        if (SynchronizationContext.Current == null)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _ui = SynchronizationContext.Current!;
        Store = new SettingsService();
        Settings = Store.Load(out string? warning);
        Settings.StartWithWindows = StartupService.IsEnabled();

        try { _trayIcon = Environment.ProcessPath is { } p ? Icon.ExtractAssociatedIcon(p) : null; }
        catch { _trayIcon = null; }

        _tray = new NotifyIcon
        {
            Icon = _trayIcon ?? SystemIcons.Application,
            Text = "DeskWidgets",
            ContextMenuStrip = _menu,
            Visible = true
        };
        BuildTrayMenu();
        _tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleDashboard(); };

        Dashboard = CreateDashboard();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (Settings.Visible) ShowDashboard(false);
        if (warning != null) Notify(warning);
    }

    // ---- tray -----------------------------------------------------------------------------

    private void BuildTrayMenu()
    {
        _miShow = new ToolStripMenuItem("Show dashboard", null, (_, _) => ShowDashboard(true));
        _miHide = new ToolStripMenuItem("Hide dashboard", null, (_, _) => HideDashboard());
        _miEdit = new ToolStripMenuItem("Edit dashboard (move / resize)", null, (_, _) => ToggleEditMode());
        _miLock = new ToolStripMenuItem("Lock position", null, (_, _) => ToggleLock());
        var miSettings = new ToolStripMenuItem("Open settings", null, (_, _) => OpenSettings());
        var miReset = new ToolStripMenuItem("Reset position", null, (_, _) => ResetPosition());
        var miExit = new ToolStripMenuItem("Exit", null, (_, _) => ExitApp());

        _menu.Items.AddRange(new ToolStripItem[]
        {
            _miShow, _miHide, new ToolStripSeparator(), _miEdit, _miLock, new ToolStripSeparator(),
            miSettings, miReset, new ToolStripSeparator(), miExit
        });
        _menu.Opening += (_, _) =>
        {
            bool visible = Dashboard != null && !Dashboard.IsDisposed && Dashboard.Visible;
            _miShow.Enabled = !visible || Settings.OpacityPercent < 10;
            _miHide.Enabled = visible;
            _miEdit.Checked = EditMode;
            _miLock.Checked = Settings.LockPosition;
        };
    }

    public void Notify(string message)
    {
        try { _tray.ShowBalloonTip(4000, "DeskWidgets", message, ToolTipIcon.Info); } catch { /* cosmetic only */ }
    }

    // ---- dashboard window -----------------------------------------------------------------

    private DashboardForm CreateDashboard()
    {
        var form = new DashboardForm(this);
        _ = form.Handle; // create the native window now so it can be sized and positioned before it is shown
        Dashboard = form;
        PlaceWindow();
        return form;
    }

    /// <summary>Called by the dashboard when Windows destroyed its window (for example Explorer restarted).</summary>
    public void OnDashboardWindowLost()
    {
        Settings.DesktopMode = DesktopModes.Desktop;
        _ui.Post(_ =>
        {
            if (IsExiting || _disposed) return;
            var old = Dashboard;
            try { old.Dispose(); } catch { /* already gone */ }
            Dashboard = CreateDashboard();
            if (Settings.Visible) ShowDashboard(false);
            Notify("Explorer restarted, so DeskWidgets switched to Desktop mode.");
            SettingsRefreshed?.Invoke();
        }, null);
    }

    public void ReportDesktopFallback()
    {
        Settings.DesktopMode = DesktopModes.Desktop;
        Store.ScheduleSave(Settings);
        Notify("Could not attach to the desktop wallpaper layer (WorkerW). Using Desktop mode instead.");
        _ui.Post(_ => SettingsRefreshed?.Invoke(), null);
    }

    /// <summary>
    /// Shows the dashboard. When the user asks explicitly (tray, hotkey) and opacity is nearly 0%,
    /// opacity is raised to 60% so the dashboard cannot stay invisible.
    /// </summary>
    public void ShowDashboard(bool explicitRequest)
    {
        if (Dashboard == null || Dashboard.IsDisposed) Dashboard = CreateDashboard();
        Settings.Visible = true;
        if (explicitRequest && Settings.OpacityPercent < 10) Settings.OpacityPercent = 60;

        PlaceWindow();
        if (!Dashboard.Visible) Dashboard.Show();
        Dashboard.ApplyDesktopMode();
        Dashboard.ApplyAll();
        Store.ScheduleSave(Settings);
        SettingsRefreshed?.Invoke();
    }

    public void HideDashboard()
    {
        Settings.Visible = false;
        if (Dashboard != null && !Dashboard.IsDisposed) Dashboard.Hide();
        Store.ScheduleSave(Settings);
        SettingsRefreshed?.Invoke();
    }

    public void ToggleDashboard()
    {
        if (Dashboard != null && !Dashboard.IsDisposed && Dashboard.Visible && Settings.OpacityPercent >= 10) HideDashboard();
        else ShowDashboard(true);
    }

    /// <summary>Applies the current settings to the window and schedules a save.</summary>
    public void Changed()
    {
        if (Dashboard != null && !Dashboard.IsDisposed) Dashboard.ApplyAll();
        Store.ScheduleSave(Settings);
    }

    public void SetDesktopMode(string mode)
    {
        Settings.DesktopMode = mode;
        if (Dashboard != null && !Dashboard.IsDisposed) Dashboard.ApplyDesktopMode();
        Store.ScheduleSave(Settings);
    }

    // ---- position and size ----------------------------------------------------------------

    /// <summary>
    /// Computes the correct window rectangle from the settings (size clamped to the screen, preset or
    /// custom position, never off-screen) and applies it. Safe to call at any time.
    /// </summary>
    public void PlaceWindow()
    {
        var f = Dashboard;
        if (f == null || f.IsDisposed || !f.IsHandleCreated) return;

        float sc = f.DpiScale;
        int inset = DashboardRenderer.Inset;
        bool custom = Settings.PositionPreset == PositionPresets.Custom && Settings.X.HasValue && Settings.Y.HasValue;
        var current = f.ScreenRect;
        var primary = Screen.PrimaryScreen ?? Screen.AllScreens[0];

        Screen screen;
        if (custom) screen = Screen.FromPoint(new Point(Settings.X!.Value + 40, Settings.Y!.Value + 40));
        else if (!current.IsEmpty && f.Visible) screen = Screen.FromRectangle(current);
        else screen = primary;

        var area = screen.WorkingArea;
        var logicalArea = new Rectangle(0, 0, (int)(area.Width / sc), (int)(area.Height / sc));
        var panel = PositionMath.ClampPanelSize(Settings.Width, Settings.Height, logicalArea, inset);
        Settings.Width = panel.Width;
        Settings.Height = panel.Height;

        var winSize = new Size((int)Math.Round((panel.Width + 2 * inset) * sc), (int)Math.Round((panel.Height + 2 * inset) * sc));
        Point pos = custom
            ? new Point(Settings.X!.Value, Settings.Y!.Value)
            : PositionMath.PresetPosition(Settings.PositionPreset, area, winSize,
                (int)Math.Round(Settings.Margin * sc), (int)Math.Round(inset * sc));

        var rect = new Rectangle(pos, winSize);
        var workAreas = Screen.AllScreens.Select(s => s.WorkingArea).ToArray();
        var recovered = PositionMath.RecoverIfOffScreen(rect, workAreas, primary.WorkingArea);
        if (recovered.HasValue) rect = recovered.Value;

        Settings.X = rect.X;
        Settings.Y = rect.Y;
        if (current != rect) f.SetScreenRect(rect);
    }

    /// <summary>Called by the dashboard after the user finished dragging or resizing it.</summary>
    public void OnWindowChanged(Rectangle rect, bool resized)
    {
        float sc = Dashboard.DpiScale;
        int inset2 = 2 * DashboardRenderer.Inset;
        if (resized)
        {
            Settings.Width = (int)Math.Round(rect.Width / sc) - inset2;
            Settings.Height = (int)Math.Round(rect.Height / sc) - inset2;
        }
        Settings.X = rect.X;
        Settings.Y = rect.Y;
        Settings.PositionPreset = PositionPresets.Custom;
        Settings.Sanitize();
        PlaceWindow();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void ApplyPreset(string preset)
    {
        Settings.PositionPreset = preset;
        if (preset != PositionPresets.Custom) { Settings.X = null; Settings.Y = null; }
        PlaceWindow();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void SetCustomPosition(int x, int y)
    {
        Settings.PositionPreset = PositionPresets.Custom;
        Settings.X = x;
        Settings.Y = y;
        Settings.Sanitize();
        PlaceWindow();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void SetMargin(int margin)
    {
        Settings.Margin = Math.Clamp(margin, 0, Limits.MaxMargin);
        if (Settings.PositionPreset != PositionPresets.Custom) PlaceWindow();
        Changed();
    }

    public void SetSize(int width, int height)
    {
        Settings.Width = width;
        Settings.Height = height;
        Settings.Sanitize();
        if (Settings.PositionPreset != PositionPresets.Custom) { Settings.X = null; Settings.Y = null; }
        Dashboard.ApplyAll();
        PlaceWindow();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void ResetPosition()
    {
        Settings.PositionPreset = PositionPresets.MiddleRight;
        Settings.X = null;
        Settings.Y = null;
        if (Dashboard != null && !Dashboard.IsDisposed && !Dashboard.Visible) ShowDashboard(true);
        PlaceWindow();
        Dashboard?.ApplyDesktopMode();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void RestoreDefaultLayout()
    {
        var d = new AppSettings();
        Settings.Width = d.Width;
        Settings.Height = d.Height;
        Settings.Margin = d.Margin;
        Settings.LockPosition = false;
        Settings.PositionPreset = d.PositionPreset;
        Settings.X = null;
        Settings.Y = null;
        Dashboard.ApplyAll();
        PlaceWindow();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    /// <summary>Resets every setting to its default (including Start with Windows and the countdown).</summary>
    public void ResetAllSettings()
    {
        StartupService.SetEnabled(false);
        Settings = new AppSettings();
        Settings.Sanitize();
        EditMode = false;
        if (!Dashboard.Visible) Dashboard.Show();
        Dashboard.ApplyAll();
        PlaceWindow();
        Dashboard.ApplyDesktopMode();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void ToggleLock()
    {
        Settings.LockPosition = !Settings.LockPosition;
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void SetLock(bool locked)
    {
        Settings.LockPosition = locked;
        Changed();
    }

    public void ToggleEditMode() => SetEditMode(!EditMode);

    public void SetEditMode(bool on)
    {
        EditMode = on;
        Changed();
        SettingsRefreshed?.Invoke();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _ui.Post(_ =>
        {
            if (IsExiting || _disposed || Dashboard == null || Dashboard.IsDisposed) return;
            PlaceWindow();
            Dashboard.ApplyAll();
            Store.ScheduleSave(Settings);
            SettingsRefreshed?.Invoke();
        }, null);
    }

    // ---- appearance, clock, general -------------------------------------------------------

    public void SetOpacity(int percent)
    {
        Settings.OpacityPercent = Math.Clamp(percent, 0, 100);
        Changed();
    }

    public bool SetStartWithWindows(bool enabled)
    {
        bool ok = StartupService.SetEnabled(enabled);
        Settings.StartWithWindows = StartupService.IsEnabled();
        Changed();
        return ok;
    }

    // ---- countdown ------------------------------------------------------------------------

    public void EditCountdown()
    {
        using var dlg = new CountdownDialog(Settings.CountdownName, Settings.CountdownDate);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        Settings.CountdownName = dlg.EventName;
        Settings.CountdownDate = dlg.EventDate;
        Settings.Sanitize();
        Changed();
        SettingsRefreshed?.Invoke();
    }

    public void DeleteCountdown()
    {
        if (Settings.CountdownDate == null && string.IsNullOrWhiteSpace(Settings.CountdownName)) return;
        var answer = MessageBox.Show("Delete this countdown?", "DeskWidgets",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        Settings.CountdownName = "";
        Settings.CountdownDate = null;
        Changed();
        SettingsRefreshed?.Invoke();
    }

    // ---- settings window and exit ---------------------------------------------------------

    public void OpenSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(this);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
            _settingsForm.Show();
        }
        if (_settingsForm.WindowState == FormWindowState.Minimized) _settingsForm.WindowState = FormWindowState.Normal;
        _settingsForm.Activate();
    }

    public void ExitApp()
    {
        IsExiting = true;
        Store.SaveNow(Settings);
        _tray.Visible = false; // remove the icon immediately so no "ghost" icon stays behind
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            IsExiting = true;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            try { Store.SaveNow(Settings); } catch { /* best effort */ }
            _settingsForm?.Dispose();
            if (Dashboard != null && !Dashboard.IsDisposed) Dashboard.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _menu.Dispose();
            _trayIcon?.Dispose();
            Store.Dispose();
        }
        base.Dispose(disposing);
    }
}
