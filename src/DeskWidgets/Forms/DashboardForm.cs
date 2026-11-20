using System.Drawing;
using DeskWidgets.Controls;
using DeskWidgets.Models;
using DeskWidgets.Services;

namespace DeskWidgets.Forms;

/// <summary>
/// The dashboard window. It is a layered, per-pixel-alpha tool window that never takes focus and has
/// no taskbar button. All drawing is done by <see cref="DashboardRenderer"/>; this class handles the
/// window, mouse input (move, resize, clicks), the one-second timer and desktop placement.
/// </summary>
internal sealed class DashboardForm : Form
{
    private const int HotkeyId = 0xD5;
    public static readonly uint ShowMessageId = NativeMethods.RegisterWindowMessage("DeskWidgets.ShowDashboard");

    private enum DragKind { None, Move, Resize }

    private readonly AppController _ctl;
    private readonly DashboardRenderer _renderer = new();
    private readonly LayeredSurface _surface = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    private IntPtr _parent = IntPtr.Zero;   // WorkerW handle when attached, otherwise zero
    private string _lastStamp = "";
    private int _tick;

    private DragKind _drag = DragKind.None;
    private int _edges;                      // 1 = left, 2 = top, 4 = right, 8 = bottom
    private Point _dragStartCursor;
    private Rectangle _dragStartRect;
    private bool _moved;
    private HitRegion? _downRegion;

    public DashboardForm(AppController controller)
    {
        _ctl = controller;
        Text = "DeskWidgets";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = false;
        _timer.Tick += OnTick;
    }

    public float DpiScale => Math.Max(1f, DeviceDpi / 96f);
    public IntPtr ParentHandle => _parent;

    public bool EditMode
    {
        get => _renderer.EditMode;
        set => _renderer.EditMode = value;
    }

    public Rectangle ScreenRect => IsHandleCreated ? DesktopIntegration.GetScreenRect(Handle) : Rectangle.Empty;

    public void SetScreenRect(Rectangle r)
    {
        if (!IsHandleCreated) return;
        DesktopIntegration.SetScreenBounds(Handle, _parent, r);
    }

    // ---- window plumbing ------------------------------------------------------------------

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Ctrl+Alt+D toggles the dashboard. If another program owns the shortcut, we simply go without it.
        NativeMethods.RegisterHotKey(Handle, HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, (uint)Keys.D);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        try { NativeMethods.UnregisterHotKey(Handle, HotkeyId); } catch { /* handle already gone */ }
        _timer.Stop();
        bool lostWithParent = _parent != IntPtr.Zero;
        _parent = IntPtr.Zero;
        base.OnHandleDestroyed(e);
        // If Explorer restarted while we were attached to its WorkerW window, Windows destroyed us too.
        if (lostWithParent && !_ctl.IsExiting) _ctl.OnDashboardWindowLost();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt64() == HotkeyId)
        {
            _ctl.ToggleDashboard();
            return;
        }
        if (ShowMessageId != 0 && (uint)m.Msg == ShowMessageId)
        {
            _ctl.ShowDashboard(true);
            return;
        }
        if (m.Msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            m.Result = new IntPtr(NativeMethods.MA_NOACTIVATE);
            return;
        }
        base.WndProc(ref m);
    }

    // Layered windows are drawn by UpdateLayeredWindow, never by WM_PAINT.
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            RenderNow();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        RenderNow();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        _renderer.InvalidateCaches();
        BeginInvoke(new Action(() => { EnsureSize(); RenderNow(); }));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _renderer.Dispose();
            _surface.Dispose();
        }
        base.Dispose(disposing);
    }

    // ---- rendering ------------------------------------------------------------------------

    public void RenderNow()
    {
        if (!IsHandleCreated || IsDisposed || !Visible) return;
        var rect = ScreenRect;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        if (!_surface.Ensure(rect.Width, rect.Height)) return;

        var now = DateTime.Now;
        using (var g = _surface.BeginGraphics())
            _renderer.Render(g, rect.Width, rect.Height, DpiScale, _ctl.Settings, now);
        _surface.Present(Handle);
        _lastStamp = Stamp(now);
    }

    private string Stamp(DateTime now)
        => _ctl.Settings.ShowSeconds
            ? now.ToString("yyyyMMddHHmmss")
            : now.ToString("yyyyMMddHHmm");

    private void OnTick(object? sender, EventArgs e)
    {
        if (!Visible) return;
        if (Stamp(DateTime.Now) != _lastStamp) RenderNow();
        if (++_tick % 3 == 0) KeepZOrder();
    }

    /// <summary>Re-applies the stacking order now and then, in case clicking the desktop changed it.</summary>
    private void KeepZOrder()
    {
        if (_drag != DragKind.None || !IsHandleCreated) return;
        switch (_ctl.Settings.DesktopMode)
        {
            case DesktopModes.Desktop:
                if (_parent == IntPtr.Zero && !DesktopIntegration.PlaceAboveDesktop(Handle))
                    DesktopIntegration.PlaceNormal(Handle);
                break;
            case DesktopModes.AlwaysOnTop:
                DesktopIntegration.PlaceTopMost(Handle);
                break;
        }
    }

    /// <summary>Called after any setting changed: re-sizes to the saved size if needed and redraws.</summary>
    public void ApplyAll()
    {
        if (!IsHandleCreated) return;
        _renderer.EditMode = _ctl.EditMode;
        EnsureSize();
        _lastStamp = "";
        RenderNow();
    }

    private void EnsureSize()
    {
        var rect = ScreenRect;
        var s = _ctl.Settings;
        float sc = DpiScale;
        int w = (int)Math.Round((s.Width + 2 * DashboardRenderer.Inset) * sc);
        int h = (int)Math.Round((s.Height + 2 * DashboardRenderer.Inset) * sc);
        if (rect.Width != w || rect.Height != h)
            SetScreenRect(new Rectangle(rect.X, rect.Y, w, h));
    }

    /// <summary>Applies the chosen desktop mode. Falls back to "Desktop" mode when WorkerW is unavailable.</summary>
    public void ApplyDesktopMode()
    {
        if (!IsHandleCreated) return;
        string mode = _ctl.Settings.DesktopMode;
        var rect = ScreenRect;

        if (mode == DesktopModes.WorkerW && _parent == IntPtr.Zero)
        {
            IntPtr workerW = DesktopIntegration.FindWorkerW();
            if (workerW != IntPtr.Zero && DesktopIntegration.AttachToParent(Handle, workerW))
            {
                _parent = workerW;
                SetScreenRect(rect);
            }
            else
            {
                mode = DesktopModes.Desktop;
                _ctl.ReportDesktopFallback();
            }
        }

        if (mode != DesktopModes.WorkerW && _parent != IntPtr.Zero)
        {
            DesktopIntegration.DetachFromParent(Handle);
            _parent = IntPtr.Zero;
            SetScreenRect(rect);
        }

        switch (mode)
        {
            case DesktopModes.Desktop:
                if (!DesktopIntegration.PlaceAboveDesktop(Handle)) DesktopIntegration.PlaceNormal(Handle);
                break;
            case DesktopModes.Normal:
                DesktopIntegration.PlaceNormal(Handle);
                break;
            case DesktopModes.AlwaysOnTop:
                DesktopIntegration.PlaceTopMost(Handle);
                break;
        }
        RenderNow();
    }

    // ---- mouse ----------------------------------------------------------------------------

    private PointF ToLogical(Point p) => new(p.X / DpiScale, p.Y / DpiScale);

    private HitRegion? HitTest(PointF logical)
    {
        var regions = _renderer.Regions;
        for (int i = regions.Count - 1; i >= 0; i--)
            if (regions[i].Rect.Contains(logical)) return regions[i];
        return null;
    }

    private int EdgesAt(PointF lp)
    {
        const float zone = 10f;
        float lw = Width / DpiScale, lh = Height / DpiScale;
        int e = 0;
        if (lp.X <= DashboardRenderer.Inset + zone) e |= 1;
        if (lp.Y <= DashboardRenderer.Inset + zone) e |= 2;
        if (lp.X >= lw - DashboardRenderer.Inset - zone) e |= 4;
        if (lp.Y >= lh - DashboardRenderer.Inset - zone) e |= 8;
        return e;
    }

    private static Cursor CursorForEdges(int e) => e switch
    {
        1 or 4 => Cursors.SizeWE,
        2 or 8 => Cursors.SizeNS,
        3 or 12 => Cursors.SizeNWSE,
        6 or 9 => Cursors.SizeNESW,
        _ => Cursors.Default
    };

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        var lp = ToLogical(e.Location);
        _downRegion = HitTest(lp);
        _moved = false;
        if (_downRegion != null) return;       // buttons never start a drag
        if (!_ctl.CanTransform) return;        // locked

        _edges = EdgesAt(lp);
        _drag = _edges != 0 ? DragKind.Resize : DragKind.Move;
        _dragStartCursor = Cursor.Position;
        _dragStartRect = ScreenRect;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_drag != DragKind.None)
        {
            var cur = Cursor.Position;
            int dx = cur.X - _dragStartCursor.X, dy = cur.Y - _dragStartCursor.Y;
            if (!_moved && Math.Abs(dx) + Math.Abs(dy) < 4) return; // ignore tiny jitters
            _moved = true;
            SetScreenRect(_drag == DragKind.Move ? MovedRect(dx, dy) : ResizedRect(dx, dy));
            return;
        }

        var lp = ToLogical(e.Location);
        if (HitTest(lp) != null) Cursor = Cursors.Hand;
        else if (_ctl.CanTransform) Cursor = CursorForEdges(EdgesAt(lp));
        else Cursor = Cursors.Default;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        // If capture is lost mid-drag (Alt+Tab, a dialog, the lock screen) stop dragging.
        if (!Capture && _drag != DragKind.None)
        {
            _drag = DragKind.None;
            _downRegion = null;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        Capture = false;

        if (_drag != DragKind.None)
        {
            var kind = _drag;
            _drag = DragKind.None;
            if (_moved) _ctl.OnWindowChanged(ScreenRect, kind == DragKind.Resize);
            _downRegion = null;
            return;
        }

        var down = _downRegion;
        _downRegion = null;
        if (down != null && HitTest(ToLogical(e.Location)) == down) RunAction(down);
    }

    private Rectangle MovedRect(int dx, int dy)
        => new(_dragStartRect.X + dx, _dragStartRect.Y + dy, _dragStartRect.Width, _dragStartRect.Height);

    private Rectangle ResizedRect(int dx, int dy)
    {
        float sc = DpiScale;
        int inset2 = 2 * DashboardRenderer.Inset;
        int minW = (int)Math.Round((Limits.MinWidth + inset2) * sc), maxW = (int)Math.Round((Limits.MaxWidth + inset2) * sc);
        int minH = (int)Math.Round((Limits.MinHeight + inset2) * sc), maxH = (int)Math.Round((Limits.MaxHeight + inset2) * sc);
        var r = _dragStartRect;
        int left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;

        if ((_edges & 1) != 0) left = Math.Clamp(r.Left + dx, r.Right - maxW, r.Right - minW);
        if ((_edges & 4) != 0) right = Math.Clamp(r.Right + dx, r.Left + minW, r.Left + maxW);
        if ((_edges & 2) != 0) top = Math.Clamp(r.Top + dy, r.Bottom - maxH, r.Bottom - minH);
        if ((_edges & 8) != 0) bottom = Math.Clamp(r.Bottom + dy, r.Top + minH, r.Top + maxH);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private void RunAction(HitRegion region)
    {
        switch (region.Action)
        {
            case HitAction.PrevMonth:
                (_renderer.ViewYear, _renderer.ViewMonth) = CalendarMath.AddMonths(_renderer.ViewYear, _renderer.ViewMonth, -1);
                RenderNow();
                break;
            case HitAction.NextMonth:
                (_renderer.ViewYear, _renderer.ViewMonth) = CalendarMath.AddMonths(_renderer.ViewYear, _renderer.ViewMonth, 1);
                RenderNow();
                break;
            case HitAction.GoToday:
                _renderer.ViewYear = DateTime.Today.Year;
                _renderer.ViewMonth = DateTime.Today.Month;
                _renderer.Selected = DateTime.Today;
                RenderNow();
                break;
            case HitAction.SelectDay:
                if (region.Date is DateTime d)
                {
                    _renderer.Selected = d;
                    if (d.Month != _renderer.ViewMonth || d.Year != _renderer.ViewYear)
                    {
                        _renderer.ViewYear = d.Year;
                        _renderer.ViewMonth = d.Month;
                    }
                    RenderNow();
                }
                break;
            case HitAction.OpenSettings:
                BeginInvoke(new Action(() => _ctl.OpenSettings()));
                break;
            case HitAction.EditCountdown:
                BeginInvoke(new Action(() => _ctl.EditCountdown()));
                break;
            case HitAction.DeleteCountdown:
                BeginInvoke(new Action(() => _ctl.DeleteCountdown()));
                break;
        }
    }
}
