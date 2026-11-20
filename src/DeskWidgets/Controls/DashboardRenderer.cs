using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using DeskWidgets.Models;

namespace DeskWidgets.Controls;

/// <summary>
/// Draws the whole dashboard with GDI+ into a Graphics supplied by the caller. It never touches
/// the window itself. The glass background (shadow, fill, border) is cached in one bitmap, so the
/// once-per-second clock update only redraws text and a cheap bitmap copy.
/// </summary>
internal sealed partial class DashboardRenderer : IDisposable
{
    /// <summary>Transparent border around the panel that holds the soft shadow (logical pixels).</summary>
    public const int Inset = 14;

    private readonly Dictionary<(string Family, int Size4, FontStyle Style), Font> _fonts = new();
    private readonly Dictionary<(StringAlignment, StringAlignment), StringFormat> _formats = new();
    private Bitmap? _backdrop;
    private string _backdropKey = "";

    // Per-frame colours (text colours are already faded for the current opacity).
    private string _family = "Segoe UI";
    private float _op = 1f;
    private bool _isLight;
    private Color _text, _dim, _accent, _accentText, _plainAccent;

    public List<HitRegion> Regions { get; } = new();
    public int ViewYear { get; set; } = DateTime.Today.Year;
    public int ViewMonth { get; set; } = DateTime.Today.Month;
    public DateTime Selected { get; set; } = DateTime.Today;
    public bool EditMode { get; set; }

    /// <param name="g">Graphics for a surface of pixelWidth x pixelHeight.</param>
    /// <param name="scale">DPI scale (1.0 = 96 DPI). Layout is done in logical units.</param>
    public void Render(Graphics g, int pixelWidth, int pixelHeight, float scale, AppSettings s, DateTime now)
    {
        Regions.Clear();
        g.Clear(Color.Transparent);
        // Keep the font cache bounded while the user drags the resize handle. Done here, before any
        // font is fetched for this frame, so a font can never be disposed while it is still in use.
        if (_fonts.Count > 48) ClearFonts();

        _op = Math.Clamp(s.OpacityPercent / 100f, 0f, 1f);
        if (_op <= 0f) return; // 0% = completely invisible (the tray icon is the way back)

        float lw = pixelWidth / scale, lh = pixelHeight / scale;
        var panel = new RectangleF(Inset, Inset, lw - 2 * Inset, lh - 2 * Inset);
        if (panel.Width < 80 || panel.Height < 80) return;

        _family = s.FontFamily;
        var pal = ThemePalette.Resolve(s);
        _isLight = pal.IsLight;
        // Glass fades linearly with the slider; text fades more gently so it stays readable.
        float textAlpha = MathF.Sqrt(_op);
        _text = Mul(pal.Text, textAlpha);
        _dim = Mul(pal.TextDim, textAlpha);
        _accent = Mul(pal.Accent, textAlpha);
        _accentText = Mul(pal.AccentText, textAlpha);
        _plainAccent = pal.Accent;

        float radius = Math.Min(s.CornerRadius, Math.Min(panel.Width, panel.Height) / 2f);

        // 1. Cached glass background.
        string key = $"{pixelWidth}x{pixelHeight}@{scale:F2}|{pal.Background.ToArgb()}|{pal.IsLight}|{s.OpacityPercent}|{radius:F1}|{s.Shadow}";
        if (_backdrop == null || key != _backdropKey)
        {
            _backdrop?.Dispose();
            _backdrop = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppPArgb);
            using var bg = Graphics.FromImage(_backdrop);
            bg.ScaleTransform(scale, scale);
            bg.SmoothingMode = SmoothingMode.AntiAlias;
            bg.PixelOffsetMode = PixelOffsetMode.HighQuality;
            DrawGlass(bg, panel, radius, s.Shadow, pal);
            _backdropKey = key;
        }
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImageUnscaled(_backdrop, 0, 0);
        g.CompositingMode = CompositingMode.SourceOver;

        // 2. Content in logical units.
        g.ScaleTransform(scale, scale);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        const float pad = 18f, gap = 12f;
        var inner = RectangleF.Inflate(panel, -pad, -pad);
        RectangleF clockR, calR, cdR;
        bool wide = panel.Width >= 560f && panel.Width > panel.Height * 1.15f;
        if (!wide)
        {
            float cdH = Math.Clamp(inner.Height * 0.19f, 84f, 120f);
            float clH = Math.Clamp(inner.Height * 0.22f, 78f, 150f);
            clockR = new RectangleF(inner.X, inner.Y, inner.Width, clH);
            cdR = new RectangleF(inner.X, inner.Bottom - cdH, inner.Width, cdH);
            float calTop = clockR.Bottom + gap;
            calR = new RectangleF(inner.X, calTop, inner.Width, Math.Max(60f, cdR.Y - gap - calTop));
        }
        else
        {
            float leftW = inner.Width * 0.42f;
            float cdH = Math.Clamp(inner.Height * 0.34f, 90f, 140f);
            clockR = new RectangleF(inner.X, inner.Y, leftW, Math.Max(60f, inner.Height - cdH - gap));
            cdR = new RectangleF(inner.X, inner.Bottom - cdH, leftW, cdH);
            calR = new RectangleF(inner.X + leftW + pad, inner.Y, inner.Width - leftW - pad, inner.Height);
        }

        DrawClock(g, clockR, s, now);
        DrawCalendar(g, calR, s, now);
        DrawCountdown(g, cdR, s, now);

        if (EditMode) DrawEditOverlay(g, panel, radius);
    }

    private void DrawGlass(Graphics g, RectangleF panel, float radius, bool shadow, ThemePalette pal)
    {
        using var path = RoundedRect(panel, radius);

        if (shadow)
        {
            var state = g.Save();
            g.SetClip(path, CombineMode.Exclude);
            for (int i = Inset - 2; i >= 1; i--)
            {
                int a = (int)(5.5f * _op * (1f - i / (float)Inset));
                if (a <= 0) continue;
                var r = new RectangleF(panel.X - i, panel.Y - i + 3, panel.Width + 2 * i, panel.Height + 2 * i);
                using var p = RoundedRect(r, radius + i);
                using var b = new SolidBrush(Color.FromArgb(a, 0, 0, 0));
                g.FillPath(b, p);
            }
            g.Restore(state);
        }

        using (var fill = new SolidBrush(Color.FromArgb((int)(255 * 0.96f * _op), pal.Background)))
            g.FillPath(fill, path);

        // Soft highlight along the top edge for the "glass" feel.
        var state2 = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        var top = new RectangleF(panel.X, panel.Y, panel.Width, Math.Max(2f, panel.Height * 0.45f));
        int hi = (int)(255 * (pal.IsLight ? 0.55f : 0.09f) * _op);
        using (var grad = new LinearGradientBrush(top, Color.FromArgb(hi, Color.White), Color.FromArgb(0, Color.White), 90f))
            g.FillRectangle(grad, top);
        g.Restore(state2);

        var edge = pal.IsLight ? Color.Black : Color.White;
        using var pen = new Pen(Color.FromArgb((int)(255 * (pal.IsLight ? 0.14f : 0.20f) * _op), edge), 1f);
        g.DrawPath(pen, path);
    }

    private void DrawEditOverlay(Graphics g, RectangleF panel, float radius)
    {
        var r = RectangleF.Inflate(panel, -3f, -3f);
        using (var path = RoundedRect(r, Math.Max(0f, radius - 3f)))
        using (var pen = new Pen(_accent, 1.5f) { DashStyle = DashStyle.Dash })
            g.DrawPath(pen, path);

        using var grip = new Pen(_accent, 1.5f);
        float x = panel.Right - 8f, y = panel.Bottom - 8f;
        for (int i = 1; i <= 3; i++)
            g.DrawLine(grip, x - i * 5f, y, x, y - i * 5f);
    }

    // ---- helpers -------------------------------------------------------------------------

    private static Color Mul(Color c, float f) => Color.FromArgb((int)Math.Round(c.A * Math.Clamp(f, 0f, 1f)), c.R, c.G, c.B);

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        if (d < 1f)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private Font GetFont(float sizePx, FontStyle style)
    {
        sizePx = Math.Max(6f, sizePx);
        var key = (_family, (int)Math.Round(sizePx * 4f), style);
        if (_fonts.TryGetValue(key, out var cached)) return cached;
        Font font;
        try { font = new Font(_family, key.Item2 / 4f, style, GraphicsUnit.Pixel); }
        catch (ArgumentException) { font = new Font("Segoe UI", key.Item2 / 4f, FontStyle.Regular, GraphicsUnit.Pixel); }
        _fonts[key] = font;
        return font;
    }

    private StringFormat GetFormat(StringAlignment h, StringAlignment v)
    {
        if (_formats.TryGetValue((h, v), out var f)) return f;
        f = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = h,
            LineAlignment = v,
            Trimming = StringTrimming.EllipsisCharacter
        };
        _formats[(h, v)] = f;
        return f;
    }

    private void DrawText(Graphics g, string text, float sizePx, FontStyle style, Color color, RectangleF rect,
        StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center)
    {
        if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0) return;
        using var brush = new SolidBrush(color);
        g.DrawString(text, GetFont(sizePx, style), brush, rect, GetFormat(h, v));
    }

    private void ClearFonts()
    {
        foreach (var f in _fonts.Values) f.Dispose();
        _fonts.Clear();
    }

    /// <summary>Drops cached fonts and the background bitmap (called when settings change).</summary>
    public void InvalidateCaches()
    {
        ClearFonts();
        _backdropKey = "";
    }

    public void Dispose()
    {
        ClearFonts();
        foreach (var f in _formats.Values) f.Dispose();
        _formats.Clear();
        _backdrop?.Dispose();
        _backdrop = null;
    }
}
