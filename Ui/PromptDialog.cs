namespace ModFrame.Ui;

internal sealed class PromptDialog : Form
{
    private readonly TextBox _input;

    public PromptDialog(string title, string label, string initialValue = "")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 154);
        BackColor = Color.FromArgb(24, 19, 34);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var prompt = new Label
        {
            Text = label,
            AutoSize = true,
            Location = new Point(20, 18),
            ForeColor = Color.FromArgb(210, 202, 222)
        };

        _input = new TextBox
        {
            Text = initialValue,
            Location = new Point(20, 48),
            Width = 400,
            BackColor = Color.FromArgb(43, 35, 58),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _input.SelectAll();

        var cancelButton = CreateButton("Cancel", DialogResult.Cancel, Color.FromArgb(65, 57, 78));
        cancelButton.Location = new Point(224, 99);

        var okButton = CreateButton("OK", DialogResult.OK, Color.FromArgb(151, 63, 210));
        okButton.Location = new Point(326, 99);

        Controls.AddRange([prompt, _input, cancelButton, okButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string InputText => _input.Text.Trim();

    private static Button CreateButton(string text, DialogResult result, Color color)
    {
        return new Button
        {
            Text = text,
            DialogResult = result,
            Size = new Size(94, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
    }
}
