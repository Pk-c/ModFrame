using System.Diagnostics;
using System.IO.Compression;
using ModFrame.Models;
using ModFrame.Services;
using ModFrame.Ui;

namespace ModFrame;

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(18, 15, 26);
    private static readonly Color SurfaceColor = Color.FromArgb(29, 24, 40);
    private static readonly Color SurfaceRaisedColor = Color.FromArgb(41, 33, 55);
    private static readonly Color AccentColor = Color.FromArgb(184, 65, 239);
    private static readonly Color AccentSoftColor = Color.FromArgb(107, 52, 139);
    private static readonly Color TextColor = Color.FromArgb(247, 242, 250);
    private static readonly Color MutedTextColor = Color.FromArgb(179, 169, 193);
    private static readonly Color SuccessColor = Color.FromArgb(72, 219, 146);
    private static readonly Color WarningColor = Color.FromArgb(255, 184, 77);
    private static readonly Color ErrorColor = Color.FromArgb(255, 91, 121);

    private readonly AdbService _adb = new();
    private readonly SteamLaunchOptionsService _steamOptions;
    private readonly SteamCdpService _steamCdp;
    private readonly CancellationTokenSource _lifetimeSource = new();
    private readonly System.Windows.Forms.Timer _deviceTimer = new() { Interval = 3500 };

    private readonly Label _connectionTitle = new();
    private readonly Label _connectionDetail = new();
    private readonly BufferedPanel _connectionDot = new();
    private readonly Button _scanButton = new();
    private readonly TextBox _pathBox = new();
    private readonly DataGridView _fileGrid = new();
    private readonly Label _emptyState = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Button _backButton = new();
    private readonly Button _homeButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _newFolderButton = new();
    private readonly Button _uploadFilesButton = new();
    private readonly Button _uploadFolderButton = new();
    private readonly Button _unzipButton = new();
    private readonly Button _downloadButton = new();
    private readonly Button _renameButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _launchOptionsButton = new();

    private FrameDevice _device = new(FrameConnectionState.Disconnected);
    private string _currentDirectory = "/home/steamos";
    private bool _isBusy;
    private bool _scanInProgress;

    public MainForm()
    {
        _steamOptions = new SteamLaunchOptionsService(_adb);
        _steamCdp = new SteamCdpService(_adb);
        Text = "ModFrame — Steam Frame Mod Manager";
        MinimumSize = new Size(1020, 680);
        Size = new Size(1260, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildInterface();
        WireEvents();
        UpdateConnectionUi();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _deviceTimer.Stop();
        _lifetimeSource.Cancel();
        _lifetimeSource.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundColor,
            ColumnCount = 1,
            RowCount = 4,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildConnectionBar(), 0, 1);
        root.Controls.Add(BuildBody(), 0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var header = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30, 14, 28, 10),
            BackColor = Color.FromArgb(37, 24, 52)
        };
        header.Paint += (_, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                header.ClientRectangle,
                Color.FromArgb(52, 27, 73),
                Color.FromArgb(25, 20, 38),
                0F);
            e.Graphics.FillRectangle(brush, header.ClientRectangle);
            using var accent = new Pen(AccentColor, 2F);
            e.Graphics.DrawLine(accent, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var brand = new Label
        {
            Text = "MODFRAME",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(28, 11),
            BackColor = Color.Transparent
        };

        var subtitle = new Label
        {
            Text = "Steam Frame Mod Manager  •  USB edition",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(204, 159, 231),
            Location = new Point(31, 53),
            BackColor = Color.Transparent
        };

        var usbBadge = new Label
        {
            Text = "  USB / ADB  ",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = TextColor,
            BackColor = AccentSoftColor,
            Padding = new Padding(8, 5, 8, 5),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(header.Width - 130, 22)
        };
        header.Resize += (_, _) => usbBadge.Left = header.ClientSize.Width - usbBadge.Width - 28;

        header.Controls.AddRange([brand, subtitle, usbBadge]);
        return header;
    }

    private Control BuildConnectionBar()
    {
        var wrapper = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 13, 24, 10),
            BackColor = BackgroundColor
        };

        var card = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(20, 13, 18, 10)
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(72, 59, 91));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        _connectionDot.Size = new Size(18, 18);
        _connectionDot.Location = new Point(20, 20);
        _connectionDot.BackColor = Color.Transparent;
        _connectionDot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var color = ConnectionColor();
            using var glow = new SolidBrush(Color.FromArgb(48, color));
            using var fill = new SolidBrush(color);
            e.Graphics.FillEllipse(glow, 0, 0, 18, 18);
            e.Graphics.FillEllipse(fill, 4, 4, 10, 10);
        };

        _connectionTitle.AutoSize = true;
        _connectionTitle.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        _connectionTitle.ForeColor = TextColor;
        _connectionTitle.Location = new Point(50, 12);

        _connectionDetail.AutoSize = true;
        _connectionDetail.Font = new Font("Segoe UI", 9F);
        _connectionDetail.ForeColor = MutedTextColor;
        _connectionDetail.Location = new Point(50, 39);

        StyleButton(_scanButton, "Scan USB", AccentSoftColor, 110);
        _scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _scanButton.Location = new Point(card.Width - 128, 16);
        card.Resize += (_, _) => _scanButton.Left = card.ClientSize.Width - _scanButton.Width - 18;

        card.Controls.AddRange([_connectionDot, _connectionTitle, _connectionDetail, _scanButton]);
        wrapper.Controls.Add(card);
        return wrapper;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 6, 24, 10),
            BackColor = BackgroundColor
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 218));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(BuildSidebar(), 0, 0);
        body.Controls.Add(BuildExplorer(), 1, 0);
        return body;
    }

    private Control BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(14)
        };

        var quickTitle = new Label
        {
            Text = "QUICK LOCATIONS",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(192, 139, 222),
            Location = new Point(15, 16)
        };
        sidebar.Controls.Add(quickTitle);

        var quickLocations = new[]
        {
            ("⌂   Home", "/home/steamos"),
            ("↓   Downloads", "/home/steamos/Downloads"),
            ("▣   Steam games", "/home/steamos/.local/share/Steam/steamapps/common"),
            ("◇   Compatibility data", "/home/steamos/.local/share/Steam/steamapps/compatdata"),
            ("/    File system", "/")
        };

        var top = 48;
        foreach (var (label, path) in quickLocations)
        {
            var button = new Button
            {
                Text = label,
                Tag = path,
                Location = new Point(10, top),
                Size = new Size(188, 38),
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = SurfaceRaisedColor },
                BackColor = SurfaceColor,
                ForeColor = TextColor,
                Cursor = Cursors.Hand
            };
            button.Click += async (_, _) => await NavigateAsync((string)button.Tag);
            sidebar.Controls.Add(button);
            top += 42;
        }

        StyleButton(_launchOptionsButton, "Launch options", AccentSoftColor, 188);
        SetButtonIcon(_launchOptionsButton, ButtonIcon.LaunchOptions);
        _launchOptionsButton.Location = new Point(10, top + 4);
        _launchOptionsButton.TextAlign = ContentAlignment.MiddleLeft;
        sidebar.Controls.Add(_launchOptionsButton);
        top += 50;

        PictureBox? mascot = null;
        var mascotPath = Path.Combine(AppContext.BaseDirectory, "Data", "Assets", "mascot.png");
        if (!File.Exists(mascotPath))
        {
            mascotPath = Path.Combine(AppContext.BaseDirectory, "Assets", "mascot.png");
        }
        if (File.Exists(mascotPath))
        {
            mascot = new PictureBox
            {
                Size = new Size(180, 180),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Location = new Point(13, top + 4),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            using var source = Image.FromFile(mascotPath);
            mascot.Image = new Bitmap(source);
            sidebar.Controls.Add(mascot);
        }

        var setupLabel = new Label
        {
            Text = "First connection?\nEnable Developer Mode on the headset, connect a USB data cable, then approve USB debugging.",
            AutoSize = false,
            Size = new Size(188, 86),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ForeColor = MutedTextColor,
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(14, sidebar.Height - 108)
        };
        sidebar.Resize += (_, _) =>
        {
            setupLabel.Top = sidebar.ClientSize.Height - 108;
            if (mascot is not null)
            {
                mascot.Top = setupLabel.Top - mascot.Height - 4;
            }
        };
        setupLabel.Top = sidebar.ClientSize.Height - 108;
        if (mascot is not null)
        {
            mascot.Top = setupLabel.Top - mascot.Height - 4;
        }
        sidebar.Controls.Add(setupLabel);

        return sidebar;
    }

    private Control BuildExplorer()
    {
        var explorer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = new Padding(1)
        };
        explorer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        explorer.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        explorer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        explorer.Controls.Add(BuildToolbar(), 0, 0);
        explorer.Controls.Add(BuildAddressBar(), 0, 1);
        explorer.Controls.Add(BuildFileView(), 0, 2);
        return explorer;
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 9, 8, 7),
            BackColor = SurfaceRaisedColor
        };

        StyleButton(_backButton, "Back", Color.FromArgb(67, 57, 80), 68);
        StyleButton(_homeButton, "Home", Color.FromArgb(67, 57, 80), 70);
        StyleButton(_refreshButton, "Refresh", Color.FromArgb(67, 57, 80), 76);
        StyleButton(_newFolderButton, "New folder", Color.FromArgb(67, 57, 80), 96);
        StyleButton(_uploadFilesButton, "Upload files", AccentSoftColor, 102);
        StyleButton(_uploadFolderButton, "Upload folder", AccentSoftColor, 112);
        StyleButton(_unzipButton, "Unzip here", Color.FromArgb(96, 49, 125), 104);
        StyleButton(_downloadButton, "Download", Color.FromArgb(67, 57, 80), 90);
        StyleButton(_renameButton, "Rename", Color.FromArgb(67, 57, 80), 78);
        StyleButton(_deleteButton, "Delete", Color.FromArgb(116, 43, 68), 72);

        SetButtonIcon(_backButton, ButtonIcon.Back);
        SetButtonIcon(_homeButton, ButtonIcon.Home);
        SetButtonIcon(_refreshButton, ButtonIcon.Refresh);
        SetButtonIcon(_newFolderButton, ButtonIcon.NewFolder);
        SetButtonIcon(_uploadFilesButton, ButtonIcon.UploadFile);
        SetButtonIcon(_uploadFolderButton, ButtonIcon.UploadFolder);
        SetButtonIcon(_unzipButton, ButtonIcon.Unzip);
        SetButtonIcon(_downloadButton, ButtonIcon.Download);
        SetButtonIcon(_renameButton, ButtonIcon.Rename);
        SetButtonIcon(_deleteButton, ButtonIcon.Delete);

        toolbar.Controls.AddRange([
            _backButton,
            _homeButton,
            _refreshButton,
            _newFolderButton,
            _uploadFilesButton,
            _uploadFolderButton,
            _unzipButton,
            _downloadButton,
            _renameButton,
            _deleteButton
        ]);
        return toolbar;
    }

    private Control BuildAddressBar()
    {
        var addressPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(12, 8, 12, 7)
        };

        _pathBox.Dock = DockStyle.Fill;
        _pathBox.Text = _currentDirectory;
        _pathBox.BackColor = Color.FromArgb(20, 17, 28);
        _pathBox.ForeColor = TextColor;
        _pathBox.BorderStyle = BorderStyle.FixedSingle;
        _pathBox.Font = new Font("Cascadia Mono", 9.5F);

        var goButton = new Button
        {
            Text = "Go",
            Dock = DockStyle.Right,
            Width = 58,
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentSoftColor,
            ForeColor = TextColor,
            Cursor = Cursors.Hand
        };
        goButton.FlatAppearance.BorderSize = 0;
        goButton.Click += async (_, _) => await NavigateFromAddressBarAsync();
        _pathBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await NavigateFromAddressBarAsync();
            }
        };

        addressPanel.Controls.Add(_pathBox);
        addressPanel.Controls.Add(goButton);
        return addressPanel;
    }

    private Control BuildFileView()
    {
        var fileView = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 18, 30),
            AllowDrop = true
        };

        _fileGrid.Dock = DockStyle.Fill;
        _fileGrid.BackgroundColor = Color.FromArgb(22, 18, 30);
        _fileGrid.BorderStyle = BorderStyle.None;
        _fileGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _fileGrid.GridColor = Color.FromArgb(48, 40, 61);
        _fileGrid.EnableHeadersVisualStyles = false;
        _fileGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _fileGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(31, 26, 42),
            ForeColor = Color.FromArgb(192, 178, 207),
            Font = new Font("Segoe UI Semibold", 9F),
            SelectionBackColor = Color.FromArgb(31, 26, 42),
            Padding = new Padding(7, 0, 0, 0)
        };
        _fileGrid.ColumnHeadersHeight = 38;
        _fileGrid.RowHeadersVisible = false;
        _fileGrid.RowTemplate.Height = 37;
        _fileGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(22, 18, 30),
            ForeColor = TextColor,
            SelectionBackColor = Color.FromArgb(91, 42, 121),
            SelectionForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(8, 0, 0, 0)
        };
        _fileGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(25, 21, 34),
            ForeColor = TextColor,
            SelectionBackColor = Color.FromArgb(91, 42, 121),
            SelectionForeColor = Color.White,
            Padding = new Padding(8, 0, 0, 0)
        };
        _fileGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _fileGrid.MultiSelect = true;
        _fileGrid.ReadOnly = true;
        _fileGrid.AllowUserToAddRows = false;
        _fileGrid.AllowUserToDeleteRows = false;
        _fileGrid.AllowUserToResizeRows = false;
        _fileGrid.AutoGenerateColumns = false;
        _fileGrid.AllowDrop = true;

        _fileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "NameColumn",
            HeaderText = "NAME",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 240
        });
        _fileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TypeColumn",
            HeaderText = "TYPE",
            Width = 90
        });
        _fileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "SizeColumn",
            HeaderText = "SIZE",
            Width = 100
        });
        _fileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ModifiedColumn",
            HeaderText = "MODIFIED",
            Width = 155
        });

        _emptyState.Text = "Connect your Steam Frame over USB to browse files.";
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.ForeColor = MutedTextColor;
        _emptyState.Font = new Font("Segoe UI", 11F);
        _emptyState.BackColor = Color.Transparent;

        fileView.Controls.Add(_fileGrid);
        fileView.Controls.Add(_emptyState);
        _emptyState.BringToFront();
        return fileView;
    }

    private Control BuildStatusBar()
    {
        var status = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 24, 40),
            Padding = new Padding(25, 8, 25, 6)
        };

        _statusLabel.Text = "Ready";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = MutedTextColor;
        _statusLabel.Dock = DockStyle.Left;

        _progressBar.Dock = DockStyle.Right;
        _progressBar.Width = 180;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 28;
        _progressBar.Visible = false;

        status.Controls.Add(_progressBar);
        status.Controls.Add(_statusLabel);
        return status;
    }

    private void WireEvents()
    {
        Shown += async (_, _) =>
        {
            await ScanForDeviceAsync(true);
            _deviceTimer.Start();
        };
        _deviceTimer.Tick += async (_, _) => await ScanForDeviceAsync(false);
        _scanButton.Click += async (_, _) => await ScanForDeviceAsync(true);
        _backButton.Click += async (_, _) => await NavigateAsync(AdbService.GetParentPath(_currentDirectory));
        _homeButton.Click += async (_, _) => await NavigateAsync("/home/steamos");
        _refreshButton.Click += async (_, _) => await RefreshDirectoryAsync();
        _newFolderButton.Click += async (_, _) => await CreateFolderAsync();
        _uploadFilesButton.Click += async (_, _) => await SelectAndUploadFilesAsync();
        _uploadFolderButton.Click += async (_, _) => await SelectAndUploadFolderAsync();
        _unzipButton.Click += async (_, _) => await SelectAndUnzipArchiveAsync();
        _downloadButton.Click += async (_, _) => await DownloadSelectionAsync();
        _renameButton.Click += async (_, _) => await RenameSelectionAsync();
        _deleteButton.Click += async (_, _) => await DeleteSelectionAsync();
        _launchOptionsButton.Click += async (_, _) => await ManageLaunchOptionsAsync();
        _fileGrid.CellDoubleClick += async (_, e) => await OpenRowAsync(e.RowIndex);
        _fileGrid.SelectionChanged += (_, _) => UpdateActionButtons();
        _fileGrid.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                await DeleteSelectionAsync();
            }
            else if (e.KeyCode == Keys.F2)
            {
                e.SuppressKeyPress = true;
                await RenameSelectionAsync();
            }
            else if (e.KeyCode == Keys.Back && !_isBusy)
            {
                e.SuppressKeyPress = true;
                await NavigateAsync(AdbService.GetParentPath(_currentDirectory));
            }
        };
        _fileGrid.DragEnter += HandleDragEnter;
        _fileGrid.DragDrop += async (_, e) => await HandleDropAsync(e);
    }

    private async Task ScanForDeviceAsync(bool userInitiated)
    {
        if (_scanInProgress || _isBusy)
        {
            return;
        }

        _scanInProgress = true;
        if (userInitiated)
        {
            _scanButton.Text = "Scanning…";
            _statusLabel.Text = "Scanning USB devices…";
        }

        try
        {
            var previousSerial = _device.Serial;
            var previousState = _device.State;
            _device = await _adb.DetectFrameAsync(_lifetimeSource.Token);
            UpdateConnectionUi();

            if (_device.State == FrameConnectionState.Connected &&
                (previousState != FrameConnectionState.Connected || previousSerial != _device.Serial))
            {
                _currentDirectory = "/home/steamos";
                await RefreshDirectoryAsync();
            }
            else if (_device.State != FrameConnectionState.Connected &&
                     previousState == FrameConnectionState.Connected)
            {
                ClearFileView();
            }
            else if (userInitiated)
            {
                _statusLabel.Text = _device.Message ?? "USB scan complete.";
            }
        }
        catch (OperationCanceledException) when (_lifetimeSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _device = new FrameDevice(FrameConnectionState.Error, Message: exception.Message);
            UpdateConnectionUi();
        }
        finally
        {
            _scanInProgress = false;
            _scanButton.Text = "Scan USB";
        }
    }

    private async Task<bool> RefreshDirectoryAsync()
    {
        if (!CanUseDevice())
        {
            return false;
        }

        return await RunBusyAsync("Loading files…", async token =>
        {
            var entries = await _adb.ListDirectoryAsync(_device.Serial!, _currentDirectory, token);
            PopulateFileGrid(entries);
            _pathBox.Text = _currentDirectory;
            _statusLabel.Text = $"{entries.Count} item{(entries.Count == 1 ? string.Empty : "s")}  •  {_currentDirectory}";
        });
    }

    private async Task NavigateAsync(string path)
    {
        if (!CanUseDevice() || _isBusy)
        {
            return;
        }

        var normalized = NormalizeRemotePath(path);
        var previousPath = _currentDirectory;
        _currentDirectory = normalized;

        if (!await RefreshDirectoryAsync())
        {
            _currentDirectory = previousPath;
            _pathBox.Text = previousPath;
        }
    }

    private async Task NavigateFromAddressBarAsync()
    {
        if (string.IsNullOrWhiteSpace(_pathBox.Text))
        {
            _pathBox.Text = _currentDirectory;
            return;
        }

        await NavigateAsync(_pathBox.Text);
    }

    private async Task OpenRowAsync(int rowIndex)
    {
        if (rowIndex < 0 || _fileGrid.Rows[rowIndex].Tag is not RemoteEntry entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            await NavigateAsync(entry.FullPath);
        }
        else
        {
            _fileGrid.ClearSelection();
            _fileGrid.Rows[rowIndex].Selected = true;
            await DownloadSelectionAsync();
        }
    }

    private async Task SelectAndUploadFilesAsync()
    {
        if (!CanUseDevice())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Select mod files to upload",
            Multiselect = true,
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await UploadPathsAsync(dialog.FileNames);
        }
    }

    private async Task SelectAndUploadFolderAsync()
    {
        if (!CanUseDevice())
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a mod folder to upload",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await UploadPathsAsync([dialog.SelectedPath]);
        }
    }

    private async Task SelectAndUnzipArchiveAsync()
    {
        if (!CanUseDevice())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = $"Select a ZIP archive to extract into {_currentDirectory}",
            Multiselect = false,
            Filter = "ZIP archives (*.zip)|*.zip"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var archiveName = Path.GetFileName(dialog.FileName);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "ModFrame",
            Guid.NewGuid().ToString("N"));

        var succeeded = await RunBusyAsync($"Extracting {archiveName}…", async token =>
        {
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                await Task.Run(
                    () => ZipFile.ExtractToDirectory(dialog.FileName, temporaryDirectory, true),
                    token);

                var extractedItems = Directory
                    .EnumerateFileSystemEntries(temporaryDirectory, "*", SearchOption.TopDirectoryOnly)
                    .ToList();

                if (extractedItems.Count == 0)
                {
                    throw new InvalidOperationException("The selected ZIP archive is empty.");
                }

                for (var index = 0; index < extractedItems.Count; index++)
                {
                    var item = extractedItems[index];
                    _statusLabel.Text =
                        $"Uploading {Path.GetFileName(item)} ({index + 1}/{extractedItems.Count}) to {_currentDirectory}…";

                    if (Directory.Exists(item))
                    {
                        await _adb.UploadDirectoryAsync(_device.Serial!, item, _currentDirectory, token);
                    }
                    else
                    {
                        await _adb.UploadFileAsync(_device.Serial!, item, _currentDirectory, token);
                    }
                }

                _statusLabel.Text = $"Extracted {archiveName} into {_currentDirectory}";
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
        });

        if (succeeded && CanUseDevice())
        {
            await RefreshDirectoryAsync();
        }
    }

    private async Task UploadPathsAsync(IEnumerable<string> paths)
    {
        var items = paths.Where(path => File.Exists(path) || Directory.Exists(path)).Distinct().ToList();
        if (items.Count == 0 || !CanUseDevice())
        {
            return;
        }

        await RunBusyAsync($"Uploading {items.Count} item{(items.Count == 1 ? string.Empty : "s")}…", async token =>
        {
            foreach (var path in items)
            {
                _statusLabel.Text = $"Uploading {Path.GetFileName(path)}…";
                if (Directory.Exists(path))
                {
                    await _adb.UploadDirectoryAsync(_device.Serial!, path, _currentDirectory, token);
                }
                else
                {
                    await _adb.UploadFileAsync(_device.Serial!, path, _currentDirectory, token);
                }
            }

            _statusLabel.Text = "Upload complete.";
        });

        if (CanUseDevice())
        {
            await RefreshDirectoryAsync();
        }
    }

    private async Task DownloadSelectionAsync()
    {
        var entries = SelectedEntries();
        if (entries.Count == 0 || !CanUseDevice())
        {
            return;
        }

        if (entries.Count == 1 && !entries[0].IsDirectory)
        {
            using var saveDialog = new SaveFileDialog
            {
                Title = "Download file",
                FileName = entries[0].Name,
                Filter = "All files (*.*)|*.*"
            };

            if (saveDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await RunBusyAsync($"Downloading {entries[0].Name}…", token =>
                _adb.DownloadAsync(_device.Serial!, entries[0].FullPath, saveDialog.FileName, token));
            _statusLabel.Text = $"Downloaded to {saveDialog.FileName}";
            return;
        }

        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Choose a folder for the downloaded items",
            UseDescriptionForTitle = true
        };
        if (folderDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await RunBusyAsync($"Downloading {entries.Count} item{(entries.Count == 1 ? string.Empty : "s")}…", async token =>
        {
            foreach (var entry in entries)
            {
                _statusLabel.Text = $"Downloading {entry.Name}…";
                await _adb.DownloadAsync(_device.Serial!, entry.FullPath, folderDialog.SelectedPath, token);
            }

            _statusLabel.Text = $"Downloaded to {folderDialog.SelectedPath}";
        });
    }

    private async Task CreateFolderAsync()
    {
        if (!CanUseDevice())
        {
            return;
        }

        using var dialog = new PromptDialog("Create folder", "Folder name");
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.InputText))
        {
            return;
        }

        if (dialog.InputText is "." or ".." || dialog.InputText.Contains('/'))
        {
            ShowWarning("Folder names cannot contain a forward slash.");
            return;
        }

        var destination = AdbService.CombineRemotePath(_currentDirectory, dialog.InputText);
        await RunBusyAsync("Creating folder…", token =>
            _adb.CreateDirectoryAsync(_device.Serial!, destination, token));
        await RefreshDirectoryAsync();
    }

    private async Task RenameSelectionAsync()
    {
        var entries = SelectedEntries();
        if (entries.Count != 1 || !CanUseDevice())
        {
            return;
        }

        var entry = entries[0];
        using var dialog = new PromptDialog("Rename item", "New name", entry.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK ||
            string.IsNullOrWhiteSpace(dialog.InputText) ||
            dialog.InputText == entry.Name)
        {
            return;
        }

        if (dialog.InputText is "." or ".." || dialog.InputText.Contains('/'))
        {
            ShowWarning("Names cannot contain a forward slash.");
            return;
        }

        var destination = AdbService.CombineRemotePath(_currentDirectory, dialog.InputText);
        await RunBusyAsync("Renaming item…", token =>
            _adb.RenameAsync(_device.Serial!, entry.FullPath, destination, token));
        await RefreshDirectoryAsync();
    }

    private async Task DeleteSelectionAsync()
    {
        var entries = SelectedEntries();
        if (entries.Count == 0 || !CanUseDevice())
        {
            return;
        }

        var names = string.Join(Environment.NewLine, entries.Take(6).Select(entry => $"• {entry.Name}"));
        if (entries.Count > 6)
        {
            names += $"{Environment.NewLine}• …and {entries.Count - 6} more";
        }

        var answer = MessageBox.Show(
            this,
            $"Permanently delete the selected item{(entries.Count == 1 ? string.Empty : "s")} from the Steam Frame?{Environment.NewLine}{Environment.NewLine}{names}",
            "Delete from Steam Frame",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        await RunBusyAsync($"Deleting {entries.Count} item{(entries.Count == 1 ? string.Empty : "s")}…", async token =>
        {
            foreach (var entry in entries)
            {
                _statusLabel.Text = $"Deleting {entry.Name}…";
                await _adb.DeleteAsync(_device.Serial!, entry.FullPath, token);
            }

            _statusLabel.Text = "Delete complete.";
        });
        await RefreshDirectoryAsync();
    }

    private async Task ManageLaunchOptionsAsync()
    {
        if (!CanUseDevice())
        {
            return;
        }

        SteamLibrarySession? session = null;
        var loaded = await RunBusyAsync("Reading installed games and Steam settings…", async token =>
        {
            session = await _steamOptions.LoadLibraryAsync(_device.Serial!, token);
            _statusLabel.Text = $"Found {session.Games.Count} installed Steam games.";
        });

        if (!loaded || session is null)
        {
            return;
        }

        using var dialog = new LaunchOptionsDialog(session);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedGame = dialog.SelectedGame;
        var desiredOptions = dialog.RequestedAction == LaunchOptionAction.Set
            ? dialog.LaunchOptions
            : null;

        var verb = dialog.RequestedAction == LaunchOptionAction.Set ? "Applying" : "Removing";
        var updated = await RunBusyAsync($"{verb} launch options for {selectedGame.Name}…", token =>
            _steamCdp.SetLaunchOptionsAsync(
                _device.Serial!,
                selectedGame.AppId,
                desiredOptions ?? string.Empty,
                token));

        if (!updated)
        {
            return;
        }

        var resultText = dialog.RequestedAction == LaunchOptionAction.Set
            ? $"Launch options were set for {selectedGame.Name}."
            : $"Launch options were removed from {selectedGame.Name}.";
        _statusLabel.Text = resultText;
        MessageBox.Show(
            this,
            resultText + Environment.NewLine + "Steam stayed open; no restart is required.",
            "Steam launch options updated",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task<bool> RunBusyAsync(string message, Func<CancellationToken, Task> action)
    {
        if (_isBusy)
        {
            return false;
        }

        _isBusy = true;
        _statusLabel.Text = message;
        _progressBar.Visible = true;
        UpdateActionButtons();
        UseWaitCursor = true;

        var succeeded = false;
        try
        {
            await action(_lifetimeSource.Token);
            succeeded = true;
        }
        catch (OperationCanceledException) when (_lifetimeSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _statusLabel.Text = exception.Message;
            MessageBox.Show(this, exception.Message, "ModFrame", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
            _progressBar.Visible = false;
            UseWaitCursor = false;
            UpdateActionButtons();
        }

        return succeeded;
    }

    private void PopulateFileGrid(IReadOnlyList<RemoteEntry> entries)
    {
        _fileGrid.SuspendLayout();
        _fileGrid.Rows.Clear();

        foreach (var entry in entries)
        {
            var icon = entry.IsDirectory ? "📁" : FileGlyph(entry.Name);
            var rowIndex = _fileGrid.Rows.Add(
                $"{icon}   {entry.Name}",
                entry.TypeText,
                entry.SizeText,
                entry.ModifiedText);
            _fileGrid.Rows[rowIndex].Tag = entry;
        }

        _fileGrid.ResumeLayout();
        _emptyState.Visible = entries.Count == 0;
        if (entries.Count == 0)
        {
            _emptyState.Text = "This folder is empty.\nDrop mod files here or use Upload files.";
            _emptyState.BringToFront();
        }
        else
        {
            _fileGrid.BringToFront();
        }

    }

    private void ClearFileView()
    {
        _fileGrid.Rows.Clear();
        _emptyState.Text = "Connect your Steam Frame over USB to browse files.";
        _emptyState.Visible = true;
        _emptyState.BringToFront();
        _statusLabel.Text = _device.Message ?? "Steam Frame disconnected.";
    }

    private void UpdateConnectionUi()
    {
        (_connectionTitle.Text, _connectionDetail.Text) = _device.State switch
        {
            FrameConnectionState.Connected =>
                ($"Steam Frame connected — {_device.HostName}", $"USB device {_device.Serial}  •  Ready for file transfers"),
            FrameConnectionState.Unauthorized =>
                ("Steam Frame authorization required", _device.Message ?? "Approve USB debugging in the headset."),
            FrameConnectionState.Offline =>
                ("Steam Frame is offline", _device.Message ?? "Reconnect the USB cable."),
            FrameConnectionState.AdbMissing =>
                ("ADB tools are missing", _device.Message ?? "Reinstall ModFrame."),
            FrameConnectionState.Error =>
                ("USB connection error", _device.Message ?? "ADB could not scan devices."),
            _ =>
                ("Steam Frame not detected", _device.Message ?? "Connect the headset with a USB data cable.")
        };

        _connectionTitle.ForeColor = ConnectionColor();
        _connectionDot.Invalidate();
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var connected = _device.State == FrameConnectionState.Connected && !_isBusy;
        var selectionCount = SelectedEntries().Count;

        _backButton.Enabled = connected && _currentDirectory != "/";
        _homeButton.Enabled = connected;
        _refreshButton.Enabled = connected;
        _newFolderButton.Enabled = connected;
        _uploadFilesButton.Enabled = connected;
        _uploadFolderButton.Enabled = connected;
        _unzipButton.Enabled = connected;
        _downloadButton.Enabled = connected && selectionCount > 0;
        _renameButton.Enabled = connected && selectionCount == 1;
        _deleteButton.Enabled = connected && selectionCount > 0;
        _launchOptionsButton.Enabled = connected;
        _pathBox.Enabled = connected;
    }

    private List<RemoteEntry> SelectedEntries()
    {
        return _fileGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.Tag)
            .OfType<RemoteEntry>()
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool CanUseDevice()
    {
        if (_device.State == FrameConnectionState.Connected && !string.IsNullOrWhiteSpace(_device.Serial))
        {
            return true;
        }

        _statusLabel.Text = "Connect a Steam Frame over USB first.";
        return false;
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = CanUseDevice() && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async Task HandleDropAsync(DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await UploadPathsAsync(paths);
        }
    }

    private Color ConnectionColor()
    {
        return _device.State switch
        {
            FrameConnectionState.Connected => SuccessColor,
            FrameConnectionState.Unauthorized or FrameConnectionState.Offline => WarningColor,
            FrameConnectionState.Error or FrameConnectionState.AdbMissing => ErrorColor,
            _ => Color.FromArgb(122, 112, 137)
        };
    }

    private static string NormalizeRemotePath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    private static string FileGlyph(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".zip" or ".7z" or ".rar" => "▰",
            ".dll" or ".so" => "⚙",
            ".json" or ".ini" or ".cfg" or ".xml" => "≡",
            ".png" or ".jpg" or ".jpeg" or ".webp" => "▧",
            _ => "□"
        };
    }

    private static void StyleButton(Button button, string text, Color backColor, int width)
    {
        button.Text = text;
        button.Size = new Size(width, 35);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Lighten(backColor, 18);
        button.FlatAppearance.MouseDownBackColor = Lighten(backColor, 8);
        button.BackColor = backColor;
        button.ForeColor = TextColor;
        button.Font = new Font("Segoe UI Semibold", 9F);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(0, 0, 6, 0);
    }

    private static void SetButtonIcon(Button button, ButtonIcon icon)
    {
        button.Image = ButtonIcons.Create(icon, TextColor);
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Padding = new Padding(5, 0, 5, 0);
    }

    private static Color Lighten(Color color, int amount)
    {
        return Color.FromArgb(
            color.A,
            Math.Min(255, color.R + amount),
            Math.Min(255, color.G + amount),
            Math.Min(255, color.B + amount));
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(this, message, "ModFrame", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
