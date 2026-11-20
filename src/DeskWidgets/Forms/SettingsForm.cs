using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using DeskWidgets.Controls;
using DeskWidgets.Models;
using DeskWidgets.Services;

namespace DeskWidgets.Forms;

/// <summary>The settings window. Every change is applied to the dashboard immediately and saved automatically.</summary>
internal sealed class SettingsForm : Form
{
    private readonly AppController _ctl;
    private bool _loading;

    private readonly List<Panel> _pages = new();
    private readonly List<Button> _navButtons = new();

    // Appearance
    private ComboBox _theme = null!, _font = null!;
    private Button _accentSwatch = null!, _backSwatch = null!;
    private TrackBar _opacity = null!, _radius = null!;
    private Label _opacityValue = null!, _radiusValue = null!;
    private CheckBox _shadow = null!;

    // Position and layout
    private ComboBox _preset = null!, _mode = null!;
    private NumericUpDown _x = null!, _y = null!, _margin = null!, _width = null!, _height = null!;
    private CheckBox _lock = null!, _edit = null!;

    // Clock
    private ComboBox _format = null!;
    private CheckBox _seconds = null!, _date = null!, _monday = null!;

    // Countdown / general
    private Label _countdownSummary = null!;
    private CheckBox _startup = null!;

    private static readonly string[] PresetLabels =
    {
        "Top-left", "Top-centre", "Top-right", "Middle-left", "Centre", "Middle-right",
        "Bottom-left", "Bottom-centre", "Bottom-right", "Custom position"
    };

    private static readonly string[] ModeLabels =
    {
        "Desktop (above icons, below windows)", "WorkerW (behind icons, experimental)",
        "Normal window", "Always on top"
    };

    public SettingsForm(AppController controller)
    {
        _ctl = controller;
        Text = "DeskWidgets Settings";
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiStyle.Back;
        ForeColor = UiStyle.Text;
        ClientSize = new Size(680, 470);

        // The Fill-docked pages must be added before the Left-docked navigation panel.
        _pages.Add(BuildAppearancePage());
        _pages.Add(BuildPositionPage());
        _pages.Add(BuildClockPage());
        _pages.Add(BuildCountdownPage());
        _pages.Add(BuildGeneralPage());
        foreach (var p in _pages) Controls.Add(p);
        BuildNavigation();
        ShowPage(0);

        RefreshValues();
        _ctl.SettingsRefreshed += RefreshValues;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _ctl.SettingsRefreshed -= RefreshValues;
        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private AppSettings S => _ctl.Settings;

    // ---- layout helpers -------------------------------------------------------------------

    private void BuildNavigation()
    {
        var nav = new Panel { Dock = DockStyle.Left, Width = 170, BackColor = UiStyle.Nav };
        string[] names = { "Appearance", "Position and layout", "Clock and calendar", "Countdown", "General" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var b = new Button
            {
                Text = names[i],
                Location = new Point(0, 12 + i * 46),
                Width = 170,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = UiStyle.Nav,
                ForeColor = UiStyle.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => ShowPage(index);
            _navButtons.Add(b);
            nav.Controls.Add(b);
        }
        Controls.Add(nav);
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            _pages[i].Visible = i == index;
            _navButtons[i].BackColor = i == index ? UiStyle.Back : UiStyle.Nav;
            _navButtons[i].ForeColor = i == index ? UiStyle.Accent : UiStyle.Text;
        }
    }

    private static Panel NewPage(out FlowLayoutPanel flow)
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = UiStyle.Back };
        flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(18, 14, 8, 14)
        };
        page.Controls.Add(flow);
        return page;
    }

    private static FlowLayoutPanel Row(string label, params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Width = 470,
            Margin = new Padding(0, 0, 0, 4)
        };
        if (label.Length > 0)
        {
            row.Controls.Add(new Label
            {
                Text = label,
                Width = 140,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiStyle.Text,
                Margin = new Padding(0, 2, 6, 2)
            });
        }
        foreach (var c in controls)
        {
            c.Margin = new Padding(0, 2, 8, 2);
            row.Controls.Add(c);
        }
        return row;
    }

    private static Label Heading(string text)
    {
        var l = UiStyle.MakeLabel(text, false, true);
        l.ForeColor = UiStyle.Accent;
        l.Margin = new Padding(0, 6, 0, 8);
        return l;
    }

    private static Label Note(string text)
    {
        var l = UiStyle.MakeLabel(text, true);
        l.AutoSize = false;
        l.Width = 460;
        l.Height = 64;
        return l;
    }

    private static ComboBox Combo(string[] items, int width = 250)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
        c.Items.AddRange(items);
        UiStyle.StyleInput(c);
        return c;
    }

    private static NumericUpDown Number(int min, int max, int width = 90)
    {
        var n = new NumericUpDown { Minimum = min, Maximum = max, Width = width };
        UiStyle.StyleInput(n);
        return n;
    }

    private static CheckBox Check(string text, int width = 440)
        => new() { Text = text, Width = width, Height = 30, ForeColor = UiStyle.Text, FlatStyle = FlatStyle.Standard };

    // ---- pages ----------------------------------------------------------------------------

    private Panel BuildAppearancePage()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Heading("Appearance"));

        _theme = Combo(new[] { "Dark glass", "Light glass", "Midnight blue" });
        _theme.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _theme.SelectedIndex < 0) return;
            S.Theme = ThemeNames.All[_theme.SelectedIndex];
            _ctl.Changed();
            RefreshValues();
        };
        flow.Controls.Add(Row("Theme", _theme));

        _accentSwatch = UiStyle.MakeButton("Change...", (_, _) => PickColour(true));
        var accentReset = UiStyle.MakeButton("Theme default", (_, _) => { S.UseCustomAccent = false; _ctl.Changed(); RefreshValues(); });
        flow.Controls.Add(Row("Accent colour", _accentSwatch, accentReset));

        _backSwatch = UiStyle.MakeButton("Change...", (_, _) => PickColour(false));
        var backReset = UiStyle.MakeButton("Theme default", (_, _) => { S.UseCustomBackground = false; _ctl.Changed(); RefreshValues(); });
        flow.Controls.Add(Row("Background colour", _backSwatch, backReset));

        _opacity = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 25, SmallChange = 1, LargeChange = 5, Width = 230, AutoSize = false, Height = 40 };
        _opacityValue = UiStyle.MakeLabel("85%", false, true, 56);
        _opacityValue.TextAlign = ContentAlignment.MiddleLeft;
        _opacityValue.Height = 32;
        _opacity.ValueChanged += (_, _) =>
        {
            _opacityValue.Text = _opacity.Value + "%";
            if (_loading) return;
            _ctl.SetOpacity(_opacity.Value);
        };
        flow.Controls.Add(Row("Transparency", _opacity, _opacityValue));

        var presets = new List<Control>();
        foreach (int v in new[] { 25, 50, 75, 100 })
        {
            int value = v;
            presets.Add(UiStyle.MakeButton(value + "%", (_, _) => { _ctl.SetOpacity(value); RefreshValues(); }));
        }
        flow.Controls.Add(Row("Presets", presets.ToArray()));
        flow.Controls.Add(Note("0% hides the dashboard completely. Use the tray icon (Show dashboard) or press Ctrl+Alt+D to bring it back."));

        _radius = new TrackBar { Minimum = 0, Maximum = Limits.MaxCornerRadius, TickFrequency = 10, LargeChange = 4, Width = 230, AutoSize = false, Height = 40 };
        _radiusValue = UiStyle.MakeLabel("20 px", false, true, 56);
        _radiusValue.TextAlign = ContentAlignment.MiddleLeft;
        _radiusValue.Height = 32;
        _radius.ValueChanged += (_, _) =>
        {
            _radiusValue.Text = _radius.Value + " px";
            if (_loading) return;
            S.CornerRadius = _radius.Value;
            _ctl.Changed();
        };
        flow.Controls.Add(Row("Corner radius", _radius, _radiusValue));

        _shadow = Check("Soft shadow");
        _shadow.CheckedChanged += (_, _) => { if (_loading) return; S.Shadow = _shadow.Checked; _ctl.Changed(); };
        flow.Controls.Add(Row("", _shadow));

        _font = Combo(InstalledFonts());
        _font.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _font.SelectedItem is not string name) return;
            S.FontFamily = name;
            _ctl.Changed();
        };
        flow.Controls.Add(Row("Font", _font));
        return page;
    }

    private Panel BuildPositionPage()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Heading("Position and layout"));

        _preset = Combo(PresetLabels);
        _preset.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _preset.SelectedIndex < 0) return;
            _ctl.ApplyPreset(PositionPresets.All[_preset.SelectedIndex]);
        };
        flow.Controls.Add(Row("Position preset", _preset));

        _x = Number(Limits.MinCoordinate, Limits.MaxCoordinate);
        _y = Number(Limits.MinCoordinate, Limits.MaxCoordinate);
        var move = UiStyle.MakeButton("Move", (_, _) => _ctl.SetCustomPosition((int)_x.Value, (int)_y.Value));
        flow.Controls.Add(Row("Custom X / Y", _x, _y, move));

        _margin = Number(0, Limits.MaxMargin, 70);
        _margin.ValueChanged += (_, _) => { if (!_loading) _ctl.SetMargin((int)_margin.Value); };
        flow.Controls.Add(Row("Screen margin (px)", _margin));

        _lock = Check("Lock position (no moving or resizing)");
        _lock.CheckedChanged += (_, _) => { if (!_loading) _ctl.SetLock(_lock.Checked); };
        flow.Controls.Add(Row("", _lock));

        _edit = Check("Drag mode: allow moving and resizing even when locked");
        _edit.CheckedChanged += (_, _) => { if (!_loading) _ctl.SetEditMode(_edit.Checked); };
        flow.Controls.Add(Row("", _edit));

        flow.Controls.Add(Heading("Size"));
        _width = Number(Limits.MinWidth, Limits.MaxWidth, 80);
        _height = Number(Limits.MinHeight, Limits.MaxHeight, 80);
        var apply = UiStyle.MakeButton("Apply size", (_, _) => _ctl.SetSize((int)_width.Value, (int)_height.Value));
        flow.Controls.Add(Row("Width / Height", _width, _height, apply));
        flow.Controls.Add(Row("Size presets",
            UiStyle.MakeButton("Compact", (_, _) => _ctl.SetSize(300, 470)),
            UiStyle.MakeButton("Normal", (_, _) => _ctl.SetSize(360, 560)),
            UiStyle.MakeButton("Large", (_, _) => _ctl.SetSize(460, 680))));
        flow.Controls.Add(Note("You can also drag the edges or corners of the dashboard. Use Drag mode if it is locked."));

        flow.Controls.Add(Heading("Desktop mode"));
        _mode = Combo(ModeLabels, 300);
        _mode.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _mode.SelectedIndex < 0) return;
            _ctl.SetDesktopMode(DesktopModes.All[_mode.SelectedIndex]);
        };
        flow.Controls.Add(Row("", _mode));
        flow.Controls.Add(Note("If the dashboard misbehaves, choose \"Desktop\" or \"Normal window\". WorkerW is experimental and falls back to Desktop automatically if it fails."));

        flow.Controls.Add(Row("",
            UiStyle.MakeButton("Reset position", (_, _) => _ctl.ResetPosition()),
            UiStyle.MakeButton("Restore default layout", (_, _) => _ctl.RestoreDefaultLayout())));
        return page;
    }

    private Panel BuildClockPage()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Heading("Clock and calendar"));

        _format = Combo(new[] { "12-hour", "24-hour" }, 160);
        _format.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _format.SelectedIndex < 0) return;
            S.Use24Hour = _format.SelectedIndex == 1;
            _ctl.Changed();
        };
        flow.Controls.Add(Row("Time format", _format));

        _seconds = Check("Show seconds");
        _seconds.CheckedChanged += (_, _) => { if (_loading) return; S.ShowSeconds = _seconds.Checked; _ctl.Changed(); };
        flow.Controls.Add(Row("", _seconds));

        _date = Check("Show date (weekday, day, month, year)");
        _date.CheckedChanged += (_, _) => { if (_loading) return; S.ShowDate = _date.Checked; _ctl.Changed(); };
        flow.Controls.Add(Row("", _date));

        _monday = Check("Calendar weeks start on Monday");
        _monday.CheckedChanged += (_, _) => { if (_loading) return; S.WeekStartsOnMonday = _monday.Checked; _ctl.Changed(); };
        flow.Controls.Add(Row("", _monday));
        return page;
    }

    private Panel BuildCountdownPage()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Heading("Countdown"));
        _countdownSummary = UiStyle.MakeLabel("No countdown set.");
        _countdownSummary.AutoSize = false;
        _countdownSummary.Width = 460;
        _countdownSummary.Height = 48;
        flow.Controls.Add(_countdownSummary);
        flow.Controls.Add(Row("",
            UiStyle.MakeButton("Add / edit event...", (_, _) => _ctl.EditCountdown(), true),
            UiStyle.MakeButton("Delete event", (_, _) => _ctl.DeleteCountdown())));
        flow.Controls.Add(Note("The countdown runs until the start (00:00) of the date you choose. You can change the name and date at any time."));
        return page;
    }

    private Panel BuildGeneralPage()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Heading("General"));

        _startup = Check("Start DeskWidgets with Windows");
        _startup.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            if (!_ctl.SetStartWithWindows(_startup.Checked))
                MessageBox.Show(this, "Windows did not allow changing the startup entry.", "DeskWidgets",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshValues();
        };
        flow.Controls.Add(Row("", _startup));

        flow.Controls.Add(Row("Dashboard",
            UiStyle.MakeButton("Show dashboard", (_, _) => _ctl.ShowDashboard(true)),
            UiStyle.MakeButton("Hide dashboard", (_, _) => _ctl.HideDashboard())));

        flow.Controls.Add(Row("Reset",
            UiStyle.MakeButton("Restore default layout", (_, _) => _ctl.RestoreDefaultLayout()),
            UiStyle.MakeButton("Reset all settings", (_, _) => ConfirmResetAll())));

        flow.Controls.Add(Row("Settings file",
            UiStyle.MakeButton("Open settings folder", (_, _) => OpenSettingsFolder())));
        flow.Controls.Add(Note("DeskWidgets 1.0.0 - works fully offline. Settings are saved automatically in " + _ctl.Store.Folder));
        return page;
    }

    // ---- behaviour ------------------------------------------------------------------------

    private static string[] InstalledFonts()
    {
        var wanted = new[] { "Segoe UI", "Segoe UI Semibold", "Calibri", "Arial", "Verdana", "Tahoma", "Consolas", "Georgia" };
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in FontFamily.Families)
        {
            installed.Add(family.Name);
            family.Dispose();
        }
        var list = wanted.Where(installed.Contains).ToList();
        if (list.Count == 0) list.Add("Segoe UI");
        return list.ToArray();
    }

    private void PickColour(bool accent)
    {
        var pal = ThemePalette.Resolve(S);
        using var dlg = new ColorDialog { FullOpen = true, Color = accent ? pal.Accent : pal.Background };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (accent)
        {
            S.AccentColor = HexColor.ToHex(dlg.Color);
            S.UseCustomAccent = true;
        }
        else
        {
            S.BackgroundColor = HexColor.ToHex(dlg.Color);
            S.UseCustomBackground = true;
        }
        _ctl.Changed();
        RefreshValues();
    }

    private void ConfirmResetAll()
    {
        var answer = MessageBox.Show(this,
            "Reset ALL settings to their defaults? This also removes your countdown and turns off Start with Windows.",
            "DeskWidgets", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer == DialogResult.Yes) _ctl.ResetAllSettings();
    }

    private void OpenSettingsFolder()
    {
        try
        {
            Directory.CreateDirectory(_ctl.Store.Folder);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _ctl.Store.Folder + "\"") { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(this, "Could not open the folder:\n" + _ctl.Store.Folder, "DeskWidgets");
        }
    }

    private static void SetNumber(NumericUpDown n, int value)
        => n.Value = Math.Clamp(value, (int)n.Minimum, (int)n.Maximum);

    /// <summary>Copies the current settings into the controls without triggering any change handlers.</summary>
    private void RefreshValues()
    {
        if (IsDisposed) return;
        _loading = true;
        try
        {
            var s = S;
            var pal = ThemePalette.Resolve(s);

            _theme.SelectedIndex = Math.Max(0, Array.IndexOf(ThemeNames.All, s.Theme));
            _accentSwatch.BackColor = pal.Accent;
            _accentSwatch.ForeColor = pal.AccentText;
            _backSwatch.BackColor = pal.Background;
            _backSwatch.ForeColor = pal.Text;
            _opacity.Value = Math.Clamp(s.OpacityPercent, 0, 100);
            _opacityValue.Text = _opacity.Value + "%";
            _radius.Value = Math.Clamp(s.CornerRadius, 0, Limits.MaxCornerRadius);
            _radiusValue.Text = _radius.Value + " px";
            _shadow.Checked = s.Shadow;
            int fontIndex = _font.Items.IndexOf(s.FontFamily);
            _font.SelectedIndex = fontIndex >= 0 ? fontIndex : 0;

            _preset.SelectedIndex = Math.Max(0, Array.IndexOf(PositionPresets.All, s.PositionPreset));
            SetNumber(_x, s.X ?? 0);
            SetNumber(_y, s.Y ?? 0);
            SetNumber(_margin, s.Margin);
            SetNumber(_width, s.Width);
            SetNumber(_height, s.Height);
            _lock.Checked = s.LockPosition;
            _edit.Checked = _ctl.EditMode;
            _mode.SelectedIndex = Math.Max(0, Array.IndexOf(DesktopModes.All, s.DesktopMode));

            _format.SelectedIndex = s.Use24Hour ? 1 : 0;
            _seconds.Checked = s.ShowSeconds;
            _date.Checked = s.ShowDate;
            _monday.Checked = s.WeekStartsOnMonday;

            if (s.CountdownDate is DateTime d)
            {
                string name = string.IsNullOrWhiteSpace(s.CountdownName) ? "Countdown" : s.CountdownName;
                _countdownSummary.Text = name + "\n" + d.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
            }
            else
            {
                _countdownSummary.Text = "No countdown set.";
            }

            _startup.Checked = s.StartWithWindows;
        }
        finally
        {
            _loading = false;
        }
    }
}
