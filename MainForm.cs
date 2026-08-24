using System.Drawing;

namespace MasconBridge;

/// <summary>
/// Control panel for the bridge: pick device and axis, calibrate, invert, and
/// start or stop the bridge.
///
/// Layout is built with TableLayoutPanel and AutoSize on purpose. With absolute
/// coordinates the text overlaps as soon as Windows scales the display. Everything
/// hand painted sizes itself from its own font or through LogicalToDeviceUnits, for
/// the same reason.
/// </summary>
public sealed class MainForm : Form
{
    private sealed record DeviceItem(int Id, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record ModelItem(string Model, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record LanguageItem(string Code, string Display)
    {
        public override string ToString() => Display;
    }

    private readonly ComboBox _cboDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboAxis = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly FlatButton _btnRefresh = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonRefresh,
    };

    private readonly AxisRow[] _axisRow = new AxisRow[Joystick.AxisNames.Length];

    private readonly Label _lblMin = new() { AutoSize = true };
    private readonly Label _lblMax = new() { AutoSize = true };
    private readonly FlatButton _btnCalibrate = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonCalibrate,
    };
    private readonly CheckBox _chkInvert = new() { Text = Strings.CheckInvertAxis, AutoSize = true };
    private readonly CheckBox _chkEbInAxis = new()
    {
        Text = Strings.CheckEbOnHandle, AutoSize = true,
    };
    private readonly NumericUpDown _numHyst = new()
    {
        DecimalPlaces = 2, Increment = 0.05M, Minimum = 0M, Maximum = 0.49M, Width = 70,
    };

    private readonly ComboBox _cboModel = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly NotchDisplay _notch = new(Strings.GroupCurrentNotch);

    private readonly FlatButton _btnStartStop = new(FlatButton.Look.Primary);
    private readonly FlatButton _btnSave = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonSaveConfiguration,
    };
    private readonly StatusPill _status = new();
    private readonly Label _lblPath = new() { ForeColor = Theme.Muted };
    private readonly ToolTip _tips = new();

    // Explanatory labels. Their width is capped in OnLoad so that a long line
    // wraps instead of stretching the whole window.
    private readonly List<Label> _hints = new();

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };

    private TableLayoutPanel _root = null!;

    private readonly string _configPath;
    private readonly Config _cfg;
    private BridgeRunner? _runner;

    /// <summary>Set when the window closed only so it can reopen in another language.</summary>
    public bool LanguageChanged { get; private set; }

    private bool _suppressLanguageEvent;
    private bool _calibrating;
    private int _calMin = int.MaxValue;
    private int _calMax = int.MinValue;
    private int _previewZone;

    public MainForm()
    {
        _configPath = ResolveConfigPath();
        _cfg = Config.Load(_configPath);

        BuildUi();
        LoadDevices();
        ConfigToUi();

        _timer.Tick += OnTick;
        _timer.Start();
    }

    // -------------------------------------------------------------------------
    private static string ResolveConfigPath()
    {
        // Config.DefaultPath is relative to the working directory, which can be
        // anything when the window is opened by double click. Fall back to the
        // file sitting next to the executable.
        var here = Path.GetFullPath(Config.DefaultPath);
        if (File.Exists(here)) return here;

        var beside = Path.Combine(AppContext.BaseDirectory, Config.DefaultPath);
        return File.Exists(beside) ? beside : here;
    }

    private static Label Cap(string text) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left,
        ForeColor = Theme.Ink, Margin = new Padding(3, 7, 8, 6),
    };

    private Label Hint(string text)
    {
        var label = new Label
        {
            Text = text, AutoSize = true, Font = Theme.Ui(8.25F), ForeColor = Theme.Muted,
        };
        _hints.Add(label);
        return label;
    }

    private static TableLayoutPanel Grid(int columns) => new()
    {
        ColumnCount = columns,
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
    };

    private void BuildUi()
    {
        Text = "Mascon Bridge";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = Theme.Ui(9F);
        BackColor = Theme.Page;
        ForeColor = Theme.Ink;

        // Reuse the icon already embedded in the executable by ApplicationIcon,
        // rather than shipping a second copy as a resource.
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { /* the window just keeps the default icon */ }

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(4, 4, 4, 8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root = root;

        root.Controls.Add(BuildHeader());
        root.Controls.Add(_notch);
        root.Controls.Add(BuildBody());
        root.Controls.Add(BuildActions());

        Controls.Add(root);
    }

    /// <summary>
    /// Two columns, because one stack of five full width cards is most of a metre of
    /// window on a scaled display. The axis readings need the width; calibration and
    /// the model do not, so they share the other side.
    /// </summary>
    private Control BuildBody()
    {
        var right = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // The slack goes to the last card, so both columns end on the same line.
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(new Card(Strings.GroupCalibration, BuildCalibration()), 0, 0);
        right.Controls.Add(new Card(Strings.GroupVirtualDevice, BuildVirtualDevice()), 0, 1);

        var body = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(LogicalToDeviceUnits(7), 0, LogicalToDeviceUnits(7), 0),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.Controls.Add(new Card(Strings.GroupDeviceAndAxis, BuildDeviceAndAxis()), 0, 0);
        body.Controls.Add(right, 1, 0);
        return body;
    }

    /// <summary>
    /// The language sits in the top corner, away from the settings that describe the
    /// hardware: it is chrome, not part of the bridge.
    /// </summary>
    private Control BuildHeader()
    {
        foreach (var (code, display) in Language.Supported)
            _cboLanguage.Items.Add(new LanguageItem(code, display));
        _cboLanguage.Margin = new Padding(3, 0, 0, 0);
        _cboLanguage.SelectedIndexChanged += OnLanguageChanged;

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
        };

        var hint = Hint(Strings.HintLanguageRestart);
        hint.Margin = new Padding(3, 7, 14, 3);
        row.Controls.Add(hint);

        var caption = Cap(Strings.LabelLanguage);
        caption.Margin = new Padding(3, 6, 8, 3);
        row.Controls.Add(caption);
        row.Controls.Add(_cboLanguage);

        var header = Grid(1);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Margin = new Padding(14, 8, 14, 4);
        header.Controls.Add(row, 0, 0);
        return header;
    }

    private Control BuildDeviceAndAxis()
    {
        var grid = Grid(4);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _cboDevice.Dock = DockStyle.Fill;
        _cboDevice.Margin = new Padding(3, 3, 10, 3);
        _cboDevice.SelectedIndexChanged += (_, _) => _previewZone = 0;

        _btnRefresh.Click += (_, _) => LoadDevices();
        _btnRefresh.Margin = new Padding(3, 0, 0, 0);

        grid.Controls.Add(Cap(Strings.LabelDevice), 0, 0);
        grid.Controls.Add(_cboDevice, 1, 0);
        grid.SetColumnSpan(_cboDevice, 2);
        grid.Controls.Add(_btnRefresh, 3, 0);

        _cboAxis.Items.AddRange(Joystick.AxisNames);
        _cboAxis.Width = 80;
        _cboAxis.Anchor = AnchorStyles.Left;
        _cboAxis.Margin = new Padding(3, 8, 3, 3);
        _cboAxis.SelectedIndexChanged += OnAxisChanged;

        var hint = Hint(Strings.HintAxis);
        hint.Anchor = AnchorStyles.Left;
        hint.Margin = new Padding(14, 13, 3, 3);

        var axisCaption = Cap(Strings.LabelAxis);
        axisCaption.Margin = new Padding(3, 12, 8, 6);

        grid.Controls.Add(axisCaption, 0, 1);
        grid.Controls.Add(_cboAxis, 1, 1);
        grid.Controls.Add(hint, 2, 1);
        grid.SetColumnSpan(hint, 2);

        var rows = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = Joystick.AxisNames.Length,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 10, 0, 0),
        };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < Joystick.AxisNames.Length; i++)
        {
            var row = new AxisRow(Joystick.AxisNames[i]);
            row.Chosen += (s, _) => _cboAxis.SelectedItem = ((AxisRow)s!).Axis;
            _axisRow[i] = row;
            rows.Controls.Add(row, 0, i);
        }

        grid.Controls.Add(rows, 0, 2);
        grid.SetColumnSpan(rows, 4);
        return grid;
    }

    private Control BuildCalibration()
    {
        var grid = Grid(5);
        for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _btnCalibrate.Click += OnCalibrateClick;
        _btnCalibrate.Anchor = AnchorStyles.Left;
        _btnCalibrate.Margin = new Padding(20, 0, 3, 0);

        _lblMin.Font = Theme.Mono(9F);
        _lblMax.Font = Theme.Mono(9F);
        _lblMin.Margin = new Padding(3, 7, 26, 6);
        _lblMax.Margin = new Padding(3, 7, 14, 6);

        grid.Controls.Add(Cap(Strings.LabelMinimum), 0, 0);
        grid.Controls.Add(_lblMin, 1, 0);
        grid.Controls.Add(Cap(Strings.LabelMaximum), 2, 0);
        grid.Controls.Add(_lblMax, 3, 0);
        grid.Controls.Add(_btnCalibrate, 4, 0);

        _chkInvert.CheckedChanged += (_, _) => { _cfg.Invert = _chkInvert.Checked; _previewZone = 0; };
        _chkInvert.Margin = new Padding(3, 14, 3, 3);
        grid.Controls.Add(_chkInvert, 0, 1);
        grid.SetColumnSpan(_chkInvert, 5);

        _chkEbInAxis.CheckedChanged += OnEbOnHandleChanged;
        _chkEbInAxis.Margin = new Padding(3, 8, 3, 3);
        grid.Controls.Add(_chkEbInAxis, 0, 2);
        grid.SetColumnSpan(_chkEbInAxis, 5);

        _numHyst.ValueChanged += (_, _) => _cfg.Hysteresis = (double)_numHyst.Value;
        _numHyst.Anchor = AnchorStyles.Left;
        _numHyst.Margin = new Padding(3, 10, 3, 3);
        _numHyst.BorderStyle = BorderStyle.FixedSingle;

        var capHyst = Cap(Strings.LabelHysteresis);
        capHyst.Margin = new Padding(3, 14, 8, 6);
        grid.Controls.Add(capHyst, 0, 3);
        grid.Controls.Add(_numHyst, 1, 3);
        return grid;
    }

    private Control BuildVirtualDevice()
    {
        // Both columns AutoSize: a control spanning into a Percent column is not
        // measured properly, and the hint below spans the pair.
        var grid = Grid(2);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        foreach (var m in Zuiki.KnownModels)
            _cboModel.Items.Add(new ModelItem(m.Model, $"{m.Model}   ·   {m.Vid:X4}:{m.Pid:X4}"));

        // Width is set in OnLoad, once the display's real font is known.
        _cboModel.Anchor = AnchorStyles.Left;
        _cboModel.Margin = new Padding(3, 3, 12, 3);
        _cboModel.SelectedIndexChanged += (_, _) => _cfg.Model = SelectedModel();

        grid.Controls.Add(Cap(Strings.LabelModel), 0, 0);
        grid.Controls.Add(_cboModel, 1, 0);

        var hint = Hint(string.Format(Strings.HintModel, Zuiki.DefaultModel));
        hint.Margin = new Padding(3, 10, 3, 0);
        grid.Controls.Add(hint, 0, 1);
        grid.SetColumnSpan(hint, 2);
        return grid;
    }

    private Control BuildActions()
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(14, 8, 14, 0),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _btnStartStop.Click += OnStartStopClick;
        _btnStartStop.Margin = new Padding(0);

        _btnSave.Click += OnSaveClick;
        _btnSave.Anchor = AnchorStyles.Right;

        _status.Anchor = AnchorStyles.Left;
        _status.Margin = new Padding(14, 6, 14, 3);
        _status.Show(Strings.StatusStopped, StatusPill.Tone.Idle);

        grid.Controls.Add(_btnStartStop, 0, 0);
        grid.Controls.Add(_status, 1, 0);
        grid.Controls.Add(_btnSave, 2, 0);

        var pathRow = BuildPathRow();
        grid.Controls.Add(pathRow, 0, 1);
        grid.SetColumnSpan(pathRow, 3);
        return grid;
    }

    /// <summary>
    /// The path is one unbreakable word, so wrapping it cost three lines and still
    /// broke it mid-directory. It gets one line and an ellipsis instead, with the
    /// whole thing on the tooltip.
    /// </summary>
    private Control BuildPathRow()
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, LogicalToDeviceUnits(14), 0, 0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var caption = new Label
        {
            Text = Strings.HintButtonsInFile,
            AutoSize = true,
            Font = Theme.Ui(8.25F),
            ForeColor = Theme.Muted,
            Margin = new Padding(3, 0, 8, 0),
        };

        _lblPath.AutoSize = false;
        _lblPath.AutoEllipsis = true;
        _lblPath.Dock = DockStyle.Fill;
        _lblPath.Font = Theme.Mono(8.25F);
        _lblPath.TextAlign = ContentAlignment.TopLeft;
        _lblPath.Height = Math.Max(Theme.LineHeight(caption.Font), Theme.LineHeight(_lblPath.Font));
        _lblPath.Margin = new Padding(0);

        row.Controls.Add(caption, 0, 0);
        row.Controls.Add(_lblPath, 1, 0);
        return row;
    }

    // -------------------------------------------------------------------------
    private void LoadDevices()
    {
        int previous = SelectedDeviceId();

        _cboDevice.Items.Clear();
        foreach (var (id, caps) in Joystick.Enumerate())
        {
            // szPname is usually "Microsoft PC-joystick driver" on every device, so
            // it identifies nothing. VID/PID and the axis count do.
            string text = string.Format(Strings.DeviceItem,
                id, $"{caps.wMid:X4}:{caps.wPid:X4}", caps.wNumAxes, caps.wNumButtons);
            _cboDevice.Items.Add(new DeviceItem(id, text));
        }

        if (_cboDevice.Items.Count == 0)
        {
            _status.Show(Strings.StatusNoJoystick, StatusPill.Tone.Warn);
            return;
        }

        SelectDevice(previous >= 0 ? previous : _cfg.AxisDeviceId);
    }

    private void SelectDevice(int id)
    {
        for (int i = 0; i < _cboDevice.Items.Count; i++)
            if (_cboDevice.Items[i] is DeviceItem d && d.Id == id)
            {
                _cboDevice.SelectedIndex = i;
                return;
            }

        if (_cboDevice.Items.Count > 0) _cboDevice.SelectedIndex = 0;
    }

    private int SelectedDeviceId() => _cboDevice.SelectedItem is DeviceItem d ? d.Id : -1;

    private string SelectedAxis() => _cboAxis.SelectedItem as string ?? "Z";

    private string SelectedModel() =>
        _cboModel.SelectedItem is ModelItem m ? m.Model : Zuiki.DefaultModel;

    private void SelectModel(string model)
    {
        for (int i = 0; i < _cboModel.Items.Count; i++)
            if (_cboModel.Items[i] is ModelItem m
                && string.Equals(m.Model, model, StringComparison.OrdinalIgnoreCase))
            {
                _cboModel.SelectedIndex = i;
                return;
            }

        // Guard the recursion: if the default is missing too, take whatever is first.
        if (!string.Equals(model, Zuiki.DefaultModel, StringComparison.OrdinalIgnoreCase))
            SelectModel(Zuiki.DefaultModel);
        else if (_cboModel.Items.Count > 0)
            _cboModel.SelectedIndex = 0;
    }

    private void OnAxisChanged(object? sender, EventArgs e)
    {
        _previewZone = 0;
        string axis = SelectedAxis();
        foreach (var row in _axisRow) row.Active = row.Axis == axis;
    }

    private void OnEbOnHandleChanged(object? sender, EventArgs e)
    {
        _cfg.IncludeEmergencyInAxis = _chkEbInAxis.Checked;
        _notch.EbOnHandle = _chkEbInAxis.Checked;
        _previewZone = 0;
    }

    private void ConfigToUi()
    {
        SelectDevice(_cfg.AxisDeviceId);
        _cboAxis.SelectedItem = _cfg.AxisName;
        if (_cboAxis.SelectedIndex < 0) _cboAxis.SelectedIndex = 0;
        OnAxisChanged(this, EventArgs.Empty);

        SelectModel(_cfg.Model);

        _suppressLanguageEvent = true;
        for (int i = 0; i < _cboLanguage.Items.Count; i++)
            if (_cboLanguage.Items[i] is LanguageItem l
                && l.Code == Language.Normalise(_cfg.Language))
            {
                _cboLanguage.SelectedIndex = i;
                break;
            }
        _suppressLanguageEvent = false;

        _lblMin.Text = _cfg.AxisMin.ToString();
        _lblMax.Text = _cfg.AxisMax.ToString();
        _chkInvert.Checked = _cfg.Invert;
        _chkEbInAxis.Checked = _cfg.IncludeEmergencyInAxis;
        _notch.EbOnHandle = _cfg.IncludeEmergencyInAxis;
        _numHyst.Value = (decimal)Math.Clamp(_cfg.Hysteresis, 0, 0.49);
        _lblPath.Text = _configPath;
        _tips.SetToolTip(_lblPath, _configPath);
    }

    private void UiToConfig()
    {
        int id = SelectedDeviceId();
        if (id >= 0) _cfg.AxisDeviceId = id;
        _cfg.AxisName = SelectedAxis();
        _cfg.Model = SelectedModel();
        _cfg.Invert = _chkInvert.Checked;
        _cfg.IncludeEmergencyInAxis = _chkEbInAxis.Checked;
        _cfg.Hysteresis = (double)_numHyst.Value;
    }

    // -------------------------------------------------------------------------
    private void OnTick(object? sender, EventArgs e)
    {
        int devId = SelectedDeviceId();
        if (devId < 0 || !Joystick.TryRead(devId, out var j))
        {
            foreach (var row in _axisRow) row.Reading = -1;
            return;
        }

        for (int i = 0; i < Joystick.AxisNames.Length; i++)
            _axisRow[i].Reading = Joystick.GetAxis(j, Joystick.AxisNames[i]);

        int raw = Joystick.GetAxis(j, SelectedAxis());

        if (_calibrating && raw >= 0)
        {
            _calMin = Math.Min(_calMin, raw);
            _calMax = Math.Max(_calMax, raw);
            _lblMin.Text = _calMin.ToString();
            _lblMax.Text = _calMax.ToString();
        }

        UpdateNotch(raw);
    }

    private void UpdateNotch(int raw)
    {
        // While the bridge runs its own state wins. Stopped, the same maths runs as
        // a preview so calibration and inversion can be checked without starting it.
        if (_runner is { IsRunning: true })
        {
            var s = _runner.State;
            _notch.Index = s.NotchIndex;
            _notch.Detail = string.Format(Strings.NotchSending, s.RawAxis, s.Fraction * 100);
            return;
        }

        if (raw < 0)
        {
            _notch.Index = -1;
            _notch.Detail = Strings.NotchAxisMissing;
            return;
        }

        UiToConfig();
        int firstNotch = _cfg.IncludeEmergencyInAxis ? 0 : 1;
        int zones = Zuiki.Notches.Length - firstNotch;

        double p = BridgeRunner.AxisFraction(raw, _cfg);
        _previewZone = BridgeRunner.ZoneWithHysteresis(p, zones, _previewZone, _cfg.Hysteresis);

        _notch.Index = firstNotch + _previewZone;
        _notch.Detail = string.Format(Strings.NotchPreview, raw, p * 100);
    }

    // -------------------------------------------------------------------------
    private void OnCalibrateClick(object? sender, EventArgs e)
    {
        if (!_calibrating)
        {
            _calibrating = true;
            _calMin = int.MaxValue;
            _calMax = int.MinValue;
            _btnCalibrate.Text = Strings.ButtonCalibrateFinish;
            _status.Show(Strings.StatusCalibrating, StatusPill.Tone.Busy);
            return;
        }

        _calibrating = false;
        _btnCalibrate.Text = Strings.ButtonCalibrate;

        if (_calMin >= _calMax)
        {
            _status.Show(Strings.StatusNoMovement, StatusPill.Tone.Warn);
            MessageBox.Show(this,
                Strings.DialogCalibrationNoMovement,
                Strings.DialogCalibrationTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ConfigToUi();
            return;
        }

        _cfg.AxisMin = _calMin;
        _cfg.AxisMax = _calMax;
        _lblMin.Text = _calMin.ToString();
        _lblMax.Text = _calMax.ToString();
        ShowIdleStatus();
        _previewZone = 0;
    }

    private void ShowIdleStatus()
    {
        if (_runner is { IsRunning: true })
            _status.Show(Strings.StatusBridgeRunning, StatusPill.Tone.Live);
        else
            _status.Show(Strings.StatusStopped, StatusPill.Tone.Idle);
    }

    private void OnStartStopClick(object? sender, EventArgs e)
    {
        if (_runner is { IsRunning: true })
        {
            _runner.Stop();
            _runner.Dispose();
            _runner = null;

            SetEditingEnabled(true);
            ShowStartStop(running: false);
            _status.Show(Strings.StatusStopped, StatusPill.Tone.Idle);
            _previewZone = 0;
            return;
        }

        UiToConfig();

        try
        {
            Cursor = Cursors.WaitCursor;
            _runner = new BridgeRunner(_cfg);
            _runner.Start();
        }
        catch (Exception ex)
        {
            _runner = null;
            MessageBox.Show(this,
                string.Format(Strings.DialogStartFailed, ex.GetType().Name, ex.Message),
                Strings.DialogStartTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Show(Strings.StatusFailedToStart, StatusPill.Tone.Warn);
            return;
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        SetEditingEnabled(false);
        ShowStartStop(running: true);
        _status.Show(Strings.StatusBridgeRunning, StatusPill.Tone.Live);
    }

    private void SetEditingEnabled(bool on)
    {
        // The running loop has already fixed the number of zones, so editing the
        // configuration halfway through would leave the window lying.
        _cboDevice.Enabled = on;
        _cboAxis.Enabled = on;
        _cboModel.Enabled = on;
        _cboLanguage.Enabled = on;
        _btnRefresh.Enabled = on;
        _btnCalibrate.Enabled = on;
        _chkInvert.Enabled = on;
        _chkEbInAxis.Enabled = on;
        _numHyst.Enabled = on;

        // Left readable rather than greyed: the readings stay live while the bridge
        // runs, it is only picking a different axis that has to wait.
        foreach (var row in _axisRow) row.Interactive = on;
    }

    /// <summary>Caption and glyph always change together, so they cannot disagree.</summary>
    private void ShowStartStop(bool running)
    {
        _btnStartStop.Fill = running ? FlatButton.Look.Danger : FlatButton.Look.Primary;
        _btnStartStop.Glyph = running ? FlatButton.Mark.Stop : FlatButton.Mark.Play;
        _btnStartStop.Text = running ? Strings.ButtonStopBridge : Strings.ButtonStartBridge;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // ConfigToUi sets the selection while loading; that is not the user choosing.
        if (_suppressLanguageEvent) return;
        if (_cboLanguage.SelectedItem is not LanguageItem chosen) return;
        if (chosen.Code == Language.Normalise(_cfg.Language)) return;

        UiToConfig();
        _cfg.Language = chosen.Code;

        // Saved before reopening: the new window reads the language back from disk.
        try { _cfg.Save(_configPath); }
        catch (Exception ex)
        {
            MessageBox.Show(this, string.Format(Strings.DialogSaveFailed, ex.Message),
                Strings.DialogSaveTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Language.Apply(chosen.Code);
        LanguageChanged = true;
        Close();
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        UiToConfig();
        try
        {
            _cfg.Save(_configPath);
            _status.Show(Strings.StatusConfigurationSaved, StatusPill.Tone.Live);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, string.Format(Strings.DialogSaveFailed, ex.Message),
                Strings.DialogSaveTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        ShowStartStop(_runner is { IsRunning: true });

        // Cap the explanatory text so a long line wraps rather than widening the
        // whole window. Without this, editing one sentence resizes the panel.
        int cap = LogicalToDeviceUnits(400);
        foreach (var h in _hints) h.MaximumSize = new Size(cap, 0);

        // Width the model list from its widest entry rather than a fixed number,
        // which would get clipped as soon as the display scaling or a model name
        // changes. Measured here, not while building, so the font is the one this
        // display actually renders with.
        int widestItem = _cboModel.Items.Cast<object>()
            .Select(i => TextRenderer.MeasureText(i.ToString(), _cboModel.Font).Width)
            .DefaultIfEmpty(LogicalToDeviceUnits(160))
            .Max();
        _cboModel.Width = widestItem + SystemInformation.VerticalScrollBarWidth + LogicalToDeviceUnits(24);

        // Size the window to what the layout engine actually measured, with this
        // display's font and scaling already applied.
        PerformLayout();
        var pref = _root.PreferredSize;

        ClientSize = new Size(Math.Max(pref.Width, LogicalToDeviceUnits(820)), pref.Height);
        MinimumSize = Size;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        _tips.Dispose();
        _runner?.Stop();
        _runner?.Dispose();
        base.OnFormClosing(e);
    }
}
