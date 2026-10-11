using ModFrame.Models;

namespace ModFrame.Ui;

internal sealed class LaunchOptionsDialog : Form
{
    public const string WinHttpPreset = "WINEDLLOVERRIDES=\"winhttp.dll=n,b\" %command%";

    private readonly ComboBox _gameBox = new();
    private readonly TextBox _optionBox = new();
    private readonly Button _applyButton = new();
    private readonly Button _removeButton = new();

    public LaunchOptionsDialog(SteamLibrarySession session)
    {
        Text = "Steam launch options";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(650, 414);
        BackColor = Color.FromArgb(24, 19, 34);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        BuildInterface(session);
        _gameBox.SelectedIndexChanged += (_, _) => LoadSelectedGame();
        _optionBox.TextChanged += (_, _) => UpdateButtons();
        _gameBox.SelectedIndex = 0;
    }

    public SteamGame SelectedGame => (SteamGame)_gameBox.SelectedItem!;

    public string LaunchOptions => _optionBox.Text.Trim();

    public LaunchOptionAction RequestedAction { get; private set; }

    private void BuildInterface(SteamLibrarySession session)
    {
        var title = new Label
        {
            Text = "Per-game launch options",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(24, 18)
        };

        var account = new Label
        {
            Text = $"Steam account folder: {session.AccountId}",
            AutoSize = true,
            ForeColor = Color.FromArgb(178, 165, 193),
            Location = new Point(27, 55)
        };

        var gameLabel = CreateLabel("GAME", 26, 92);
        _gameBox.Location = new Point(26, 116);
        _gameBox.Size = new Size(598, 30);
        _gameBox.DropDownStyle = ComboBoxStyle.DropDown;
        _gameBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _gameBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        _gameBox.BackColor = Color.FromArgb(43, 35, 58);
        _gameBox.ForeColor = Color.White;
        _gameBox.DisplayMember = nameof(SteamGame.DisplayName);
        foreach (var game in session.Games)
        {
            _gameBox.Items.Add(game);
        }

        var optionLabel = CreateLabel("LAUNCH OPTIONS", 26, 163);
        _optionBox.Location = new Point(26, 188);
        _optionBox.Size = new Size(598, 62);
        _optionBox.Multiline = true;
        _optionBox.ScrollBars = ScrollBars.Vertical;
        _optionBox.BackColor = Color.FromArgb(43, 35, 58);
        _optionBox.ForeColor = Color.White;
        _optionBox.BorderStyle = BorderStyle.FixedSingle;
        _optionBox.Font = new Font("Cascadia Mono", 9.5F);

        var presetButton = CreateButton("Use winhttp preset", Color.FromArgb(78, 47, 101), 162);
        presetButton.Location = new Point(26, 263);
        presetButton.Click += (_, _) => _optionBox.Text = WinHttpPreset;

        var warning = new Label
        {
            Text = "ModFrame applies this through the running Steam client over USB. Steam stays open and no restart is required.",
            AutoSize = false,
            Size = new Size(598, 42),
            Location = new Point(26, 303),
            ForeColor = Color.FromArgb(212, 187, 125)
        };

        var cancelButton = CreateButton("Cancel", Color.FromArgb(65, 57, 78), 88);
        cancelButton.Location = new Point(536, 366);
        cancelButton.DialogResult = DialogResult.Cancel;

        _removeButton.Text = "Remove";
        StyleActionButton(_removeButton, Color.FromArgb(116, 43, 68), 92);
        _removeButton.Location = new Point(336, 366);
        _removeButton.Click += (_, _) =>
        {
            RequestedAction = LaunchOptionAction.Remove;
            DialogResult = DialogResult.OK;
        };

        _applyButton.Text = "Apply";
        StyleActionButton(_applyButton, Color.FromArgb(151, 63, 210), 92);
        _applyButton.Location = new Point(436, 366);
        _applyButton.Click += (_, _) =>
        {
            RequestedAction = LaunchOptionAction.Set;
            DialogResult = DialogResult.OK;
        };

        Controls.AddRange([
            title,
            account,
            gameLabel,
            _gameBox,
            optionLabel,
            _optionBox,
            presetButton,
            warning,
            _removeButton,
            _applyButton,
            cancelButton
        ]);
        AcceptButton = _applyButton;
        CancelButton = cancelButton;
    }

    private void LoadSelectedGame()
    {
        if (_gameBox.SelectedItem is SteamGame game)
        {
            _optionBox.Text = game.LaunchOptions;
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var gameSelected = _gameBox.SelectedItem is SteamGame;
        _applyButton.Enabled = gameSelected && !string.IsNullOrWhiteSpace(_optionBox.Text);
        _removeButton.Enabled = gameSelected;
    }

    private static Label CreateLabel(string text, int left, int top)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(192, 139, 222),
            Location = new Point(left, top)
        };
    }

    private static Button CreateButton(string text, Color color, int width)
    {
        var button = new Button { Text = text };
        StyleActionButton(button, color, width);
        return button;
    }

    private static void StyleActionButton(Button button, Color color, int width)
    {
        button.Size = new Size(width, 34);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Cursor = Cursors.Hand;
    }
}
