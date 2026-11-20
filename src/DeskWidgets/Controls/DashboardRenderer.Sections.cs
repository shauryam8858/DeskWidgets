using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using DeskWidgets.Models;

namespace DeskWidgets.Controls;

internal sealed partial class DashboardRenderer
{
    // ---- Clock ---------------------------------------------------------------------------

    private void DrawClock(Graphics g, RectangleF r, AppSettings s, DateTime now)
    {
        var inv = CultureInfo.InvariantCulture;
        string time = s.Use24Hour ? now.ToString("HH:mm", inv) : now.ToString("h:mm", inv);
        if (s.ShowSeconds) time += now.ToString(":ss", inv);
        string suffix = s.Use24Hour ? "" : (now.Hour < 12 ? "AM" : "PM");

        float timeAreaH = r.Height * (s.ShowDate ? 0.64f : 0.95f);
        float availW = Math.Max(40f, r.Width - 30f); // leave room for the settings button
        float size = Math.Max(16f, timeAreaH * 0.92f);

        var timeFont = GetFont(size, FontStyle.Regular);
        var sufFont = GetFont(size * 0.30f, FontStyle.Regular);
        SizeF tm = g.MeasureString(time, timeFont, PointF.Empty, StringFormat.GenericTypographic);
        SizeF sm = suffix.Length > 0
            ? g.MeasureString(suffix, sufFont, PointF.Empty, StringFormat.GenericTypographic)
            : SizeF.Empty;
        float total = tm.Width + (suffix.Length > 0 ? sm.Width + 6f : 0f);
        float factor = total > availW ? availW / total : 1f;

        if (factor < 1f)
        {
            size = Math.Max(14f, size * factor);
            timeFont = GetFont(size, FontStyle.Regular);
            sufFont = GetFont(size * 0.30f, FontStyle.Regular);
            tm = g.MeasureString(time, timeFont, PointF.Empty, StringFormat.GenericTypographic);
            if (suffix.Length > 0)
                sm = g.MeasureString(suffix, sufFont, PointF.Empty, StringFormat.GenericTypographic);
        }

        float ty = r.Y + Math.Max(0f, (timeAreaH - tm.Height) / 2f);
        using (var tb = new SolidBrush(_text))
            g.DrawString(time, timeFont, tb, new PointF(r.X, ty), StringFormat.GenericTypographic);
        if (suffix.Length > 0)
        {
            using var sb = new SolidBrush(_dim);
            g.DrawString(suffix, sufFont, sb, new PointF(r.X + tm.Width + 6f, ty + tm.Height - sm.Height - tm.Height * 0.14f),
                StringFormat.GenericTypographic);
        }

        if (s.ShowDate)
        {
            var cult = CultureInfo.CurrentCulture;
            string date = now.ToString("dddd, d MMMM yyyy", cult);
            var dateRect = new RectangleF(r.X, r.Y + timeAreaH, r.Width, Math.Max(16f, r.Height - timeAreaH));
            float dsize = Math.Clamp(dateRect.Height * 0.55f, 11f, 17f);
            var dfont = GetFont(dsize, FontStyle.Regular);
            float dw = g.MeasureString(date, dfont, PointF.Empty, StringFormat.GenericTypographic).Width;
            if (dw > r.Width) dsize = Math.Max(10f, dsize * r.Width / dw);
            DrawText(g, date, dsize, FontStyle.Regular, _dim, dateRect);
        }

        DrawSettingsButton(g, new RectangleF(r.Right - 28f, r.Y, 28f, 28f));
    }

    private void DrawSettingsButton(Graphics g, RectangleF b)
    {
        float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
        using var pen = new Pen(_dim, 1.6f);
        g.DrawEllipse(pen, cx - 4.5f, cy - 4.5f, 9f, 9f);
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            float x1 = cx + (float)Math.Cos(a) * 7f, y1 = cy + (float)Math.Sin(a) * 7f;
            float x2 = cx + (float)Math.Cos(a) * 9.5f, y2 = cy + (float)Math.Sin(a) * 9.5f;
            g.DrawLine(pen, x1, y1, x2, y2);
        }
        Regions.Add(new HitRegion(b, HitAction.OpenSettings));
    }

    // ---- Calendar ------------------------------------------------------------------------

    private void DrawCalendar(Graphics g, RectangleF r, AppSettings s, DateTime now)
    {
        const float btn = 28f;
        const float headerH = 30f;
        var cult = CultureInfo.CurrentCulture;
        string title = new DateTime(ViewYear, ViewMonth, 1).ToString("MMMM yyyy", cult);

        // Header: title on the left; Today, previous, next on the right.
        float x = r.Right - btn;
        var nextR = new RectangleF(x, r.Y, btn, btn);
        var prevR = new RectangleF(x - btn - 2f, r.Y, btn, btn);
        float todayW = 52f;
        var todayR = new RectangleF(prevR.X - todayW - 4f, r.Y, todayW, btn);
        bool showToday = r.Width >= 230f;
        float titleRight = showToday ? todayR.X : prevR.X;
        DrawText(g, title, 15f, FontStyle.Bold, _text,
            new RectangleF(r.X, r.Y, Math.Max(20f, titleRight - r.X - 4f), btn));

        DrawChevron(g, prevR, true);
        DrawChevron(g, nextR, false);
        Regions.Add(new HitRegion(prevR, HitAction.PrevMonth));
        Regions.Add(new HitRegion(nextR, HitAction.NextMonth));
        if (showToday)
        {
            using (var path = RoundedRect(RectangleF.Inflate(todayR, 0f, -3f), 10f))
            using (var pen = new Pen(Mul(_plainAccent, Math.Max(0.5f, _op)), 1f))
                g.DrawPath(pen, path);
            DrawText(g, "Today", 12f, FontStyle.Regular, _accent, todayR, StringAlignment.Center);
            Regions.Add(new HitRegion(todayR, HitAction.GoToday));
        }

        // Weekday labels.
        float cellW = r.Width / 7f;
        float labelsTop = r.Y + headerH;
        const float labelsH = 18f;
        var labels = CalendarMath.WeekdayLabels(s.WeekStartsOnMonday);
        for (int i = 0; i < 7; i++)
            DrawText(g, labels[i], 11f, FontStyle.Regular, _dim,
                new RectangleF(r.X + i * cellW, labelsTop, cellW, labelsH), StringAlignment.Center);

        // Day grid: 6 rows.
        float gridTop = labelsTop + labelsH + 2f;
        float rowH = Math.Max(14f, (r.Bottom - gridTop) / 6f);
        rowH = Math.Min(rowH, cellW * 1.2f);
        float dayFont = Math.Clamp(Math.Min(rowH * 0.46f, cellW * 0.42f), 9f, 18f);
        float circle = Math.Min(cellW, rowH) * 0.86f;
        var grid = CalendarMath.BuildGrid(ViewYear, ViewMonth, s.WeekStartsOnMonday);

        for (int i = 0; i < 42; i++)
        {
            int col = i % 7, row = i / 7;
            var cell = new RectangleF(r.X + col * cellW, gridTop + row * rowH, cellW, rowH);
            DateTime d = grid[i];
            bool inMonth = d.Month == ViewMonth;
            bool isToday = d.Date == now.Date;
            bool isSelected = d.Date == Selected.Date;

            var c = new RectangleF(cell.X + (cell.Width - circle) / 2f, cell.Y + (cell.Height - circle) / 2f, circle, circle);
            Color textColor = inMonth ? _text : Mul(_dim, 0.55f);

            if (isToday)
            {
                using var fill = new SolidBrush(_accent);
                g.FillEllipse(fill, c);
                textColor = _accentText;
            }
            else if (isSelected)
            {
                using var pen = new Pen(_accent, 1.6f);
                g.DrawEllipse(pen, c.X + 0.8f, c.Y + 0.8f, c.Width - 1.6f, c.Height - 1.6f);
            }

            DrawText(g, d.Day.ToString(CultureInfo.InvariantCulture), dayFont,
                isToday ? FontStyle.Bold : FontStyle.Regular, textColor, cell, StringAlignment.Center);
            Regions.Add(new HitRegion(cell, HitAction.SelectDay, d.Date));
        }
    }

    private void DrawChevron(Graphics g, RectangleF b, bool left)
    {
        float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
        float dx = left ? 3f : -3f;
        using var pen = new Pen(_text, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[]
        {
            new PointF(cx + dx, cy - 6f),
            new PointF(cx - dx, cy),
            new PointF(cx + dx, cy + 6f)
        });
    }

    // ---- Countdown -----------------------------------------------------------------------

    private void DrawCountdown(Graphics g, RectangleF r, AppSettings s, DateTime now)
    {
        using (var path = RoundedRect(r, 14f))
        {
            using var fill = new SolidBrush(Color.FromArgb((int)(255 * (_isLight ? 0.07f : 0.08f) * _op),
                _isLight ? Color.Black : Color.White));
            g.FillPath(fill, path);
            using var pen = new Pen(Color.FromArgb((int)(255 * 0.10f * _op), _isLight ? Color.Black : Color.White), 1f);
            g.DrawPath(pen, path);
        }

        var result = CountdownMath.Compute(now, s.CountdownDate);
        const float p = 12f;

        if (!result.IsSet)
        {
            DrawText(g, "No countdown set", 15f, FontStyle.Bold, _text,
                new RectangleF(r.X + p, r.Y + 8f, r.Width - 2 * p, 26f));
            DrawText(g, "Click here to add an event and date", 12f, FontStyle.Regular, _dim,
                new RectangleF(r.X + p, r.Y + 36f, r.Width - 2 * p, Math.Max(18f, r.Height - 44f)));
            Regions.Add(new HitRegion(r, HitAction.EditCountdown));
            return;
        }

        string name = string.IsNullOrWhiteSpace(s.CountdownName) ? "Countdown" : s.CountdownName;
        const float iconBox = 28f;
        var deleteR = new RectangleF(r.Right - p - iconBox + 4f, r.Y + 6f, iconBox, iconBox);
        var editR = new RectangleF(deleteR.X - iconBox, r.Y + 6f, iconBox, iconBox);
        DrawText(g, name, 15f, FontStyle.Bold, _text,
            new RectangleF(r.X + p, r.Y + 6f, Math.Max(20f, editR.X - r.X - p), iconBox));
        DrawEditIcon(g, editR);
        DrawDeleteIcon(g, deleteR);
        Regions.Add(new HitRegion(editR, HitAction.EditCountdown));
        Regions.Add(new HitRegion(deleteR, HitAction.DeleteCountdown));

        var body = new RectangleF(r.X + p, r.Y + 6f + iconBox, r.Width - 2 * p, Math.Max(20f, r.Height - 6f - iconBox - 6f));

        if (result.IsComplete)
        {
            DrawText(g, "The wait is over.", 16f, FontStyle.Regular, _accent, body);
            return;
        }

        if (body.Width < 200f)
        {
            string line = $"{result.Days}d {result.Hours:00}h {result.Minutes:00}m";
            DrawText(g, line, Math.Clamp(body.Height * 0.6f, 12f, 22f), FontStyle.Regular, _accent, body);
            return;
        }

        float colW = body.Width / 3f;
        float numSize = Math.Clamp(Math.Min(body.Height * 0.56f, colW * 0.42f), 14f, 34f);
        string[] numbers =
        {
            result.Days.ToString(CultureInfo.InvariantCulture),
            result.Hours.ToString("00", CultureInfo.InvariantCulture),
            result.Minutes.ToString("00", CultureInfo.InvariantCulture)
        };
        string[] captions = { "DAYS", "HOURS", "MINUTES" };
        for (int i = 0; i < 3; i++)
        {
            var col = new RectangleF(body.X + i * colW, body.Y, colW, body.Height);
            DrawText(g, numbers[i], numSize, FontStyle.Regular, _accent,
                new RectangleF(col.X, col.Y, col.Width, col.Height * 0.64f), StringAlignment.Center, StringAlignment.Far);
            DrawText(g, captions[i], 10f, FontStyle.Regular, _dim,
                new RectangleF(col.X, col.Y + col.Height * 0.66f, col.Width, col.Height * 0.34f), StringAlignment.Center, StringAlignment.Near);
        }
    }

    private void DrawEditIcon(Graphics g, RectangleF b)
    {
        float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
        using var pen = new Pen(_dim, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, cx - 5f, cy + 5f, cx + 4f, cy - 4f);
        g.DrawLine(pen, cx - 5f, cy + 5f, cx - 5f, cy + 2f);
        g.DrawLine(pen, cx - 5f, cy + 5f, cx - 2f, cy + 5f);
        g.DrawLine(pen, cx + 2f, cy - 6f, cx + 6f, cy - 2f);
    }

    private void DrawDeleteIcon(Graphics g, RectangleF b)
    {
        float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
        using var pen = new Pen(_dim, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
        g.DrawLine(pen, cx - 4.5f, cy + 4.5f, cx + 4.5f, cy - 4.5f);
    }
}
