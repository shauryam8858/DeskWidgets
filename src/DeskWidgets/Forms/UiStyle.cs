using System.Drawing;

namespace DeskWidgets.Forms;

/// <summary>Shared colours and small control factories so the settings windows match the dashboard.</summary>
internal static class UiStyle
{
    public static readonly Color Back = Color.FromArgb(30, 34, 41);
    public static readonly Color Nav = Color.FromArgb(24, 27, 33);
    public static readonly Color Field = Color.FromArgb(46, 52, 63);
    public static readonly Color Text = Color.FromArgb(242, 244, 248);
    public static readonly Color Dim = Color.FromArgb(154, 163, 178);
    public static readonly Color Accent = Color.FromArgb(108, 142, 191);

    public static Button MakeButton(string text, EventHandler onClick, bool primary = false)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(72, 34),
            Padding = new Padding(8, 2, 8, 2),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = primary ? Accent : Field,
            ForeColor = primary ? Color.White : Text,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(70, 78, 92);
        b.FlatAppearance.BorderSize = 1;
        b.Click += onClick;
        return b;
    }

    public static Label MakeLabel(string text, bool dim = false, bool bold = false, int width = 0)
    {
        var l = new Label
        {
            Text = text,
            AutoSize = width == 0,
            ForeColor = dim ? Dim : Text,
            BackColor = Color.Transparent
        };
        if (width > 0) l.Width = width;
        if (bold) l.Font = new Font(l.Font, FontStyle.Bold);
        return l;
    }

    public static void StyleInput(Control c)
    {
        c.BackColor = Field;
        c.ForeColor = Text;
        if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
        if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
    }
}
