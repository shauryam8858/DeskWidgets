using System.Drawing;

namespace DeskWidgets.Forms;

/// <summary>Small dialog for entering a countdown event name and target date.</summary>
internal sealed class CountdownDialog : Form
{
    private readonly TextBox _name = new();
    private readonly DateTimePicker _date = new();

    public string EventName => _name.Text.Trim();
    public DateTime EventDate => _date.Value.Date;

    public CountdownDialog(string? currentName, DateTime? currentDate)
    {
        Text = "Countdown event";
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        BackColor = UiStyle.Back;
        ForeColor = UiStyle.Text;
        ClientSize = new Size(400, 220);

        var nameLabel = UiStyle.MakeLabel("Event name");
        nameLabel.Location = new Point(20, 18);
        _name.Location = new Point(20, 42);
        _name.Width = 360;
        _name.MaxLength = 60;
        _name.PlaceholderText = "For example: NEET 2027";
        _name.Text = currentName ?? "";
        UiStyle.StyleInput(_name);

        var dateLabel = UiStyle.MakeLabel("Target date");
        dateLabel.Location = new Point(20, 84);
        _date.Location = new Point(20, 108);
        _date.Width = 360;
        _date.Format = DateTimePickerFormat.Long;
        _date.MinDate = new DateTime(1900, 1, 1);
        _date.MaxDate = new DateTime(2200, 12, 31);
        _date.Value = (currentDate ?? DateTime.Today).Date;

        var save = UiStyle.MakeButton("Save", OnSave, true);
        save.Location = new Point(206, 164);
        var cancel = UiStyle.MakeButton("Cancel", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        cancel.Location = new Point(298, 164);

        Controls.AddRange(new Control[] { nameLabel, _name, dateLabel, _date, save, cancel });
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, "Please type a name for the event.", "DeskWidgets",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _name.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
