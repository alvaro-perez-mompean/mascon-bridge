using System.Drawing;

namespace MasconBridge;

/// <summary>
/// Control panel for the bridge: pick device and axis, calibrate, invert, and
/// start or stop the bridge.
///
/// Layout is built with TableLayoutPanel and AutoSize on purpose. With absolute
/// coordinates the text overlaps as soon as Windows scales the display.
/// </summary>
public sealed class MainForm : Form
{
    private sealed record DeviceItem(int Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly ComboBox _cboDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboAxis = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnRefresh = new() { Text = "Refresh", AutoSize = true };

    private readonly Label[] _axisValue = new Label[Joystick.AxisNames.Length];
    private readonly ProgressBar[] _axisBar = new ProgressBar[Joystick.AxisNames.Length];

    private readonly Label _lblMin = new() { AutoSize = true };
    private readonly Label _lblMax = new() { AutoSize = true };
    private readonly Button _btnCalibrate = new() { Text = "Calibrate", AutoSize = true };
    private readonly CheckBox _chkInvert = new() { Text = "Invert axis", AutoSize = true };
    private readonly CheckBox _chkEbInAxis = new()
    {
        Text = "EB on the handle (15 notches instead of 14)", AutoSize = true,
    };
    private readonly NumericUpDown _numHyst = new()
    {
        DecimalPlaces = 2, Increment = 0.05M, Minimum = 0M, Maximum = 0.49M, Width = 70,
    };

    private readonly Label _lblNotch = new() { AutoSize = true };
    private readonly Label _lblRaw = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly ProgressBar _barNotch = new()
    {
        Minimum = 0, Maximum = 14, Height = 20, MinimumSize = new Size(300, 20),
    };

    private readonly Button _btnStartStop = new() { Text = "Start bridge", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Save configuration", AutoSize = true };
    private readonly Label _lblStatus = new() { AutoSize = true, Text = "Stopped" };
    private readonly Label _lblPath = new() { AutoSize = true, ForeColor = SystemColors.GrayText };

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };

    private TableLayoutPanel _root = null!;

    private readonly string _configPath;
    private readonly Config _cfg;
    private BridgeRunner? _runner;

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
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 6),
    };

    private static TableLayoutPanel Grid(int columns) => new()
    {
        ColumnCount = columns,
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(8, 6, 8, 8),
    };

    private static GroupBox Group(string text, Control inner) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(6, 4, 6, 6),
        Margin = new Padding(10, 6, 10, 6),
        Controls = { inner },
    };

    private void BuildUi()
    {
        Text = "Mascon Bridge";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;

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
            Padding = new Padding(4),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root = root;

        // --- Device and axis ---------------------------------------------------
        var gd = Grid(4);
        gd.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gd.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gd.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gd.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _cboDevice.Dock = DockStyle.Fill;
        _cboDevice.Margin = new Padding(3, 3, 8, 3);
        _cboDevice.SelectedIndexChanged += (_, _) => _previewZone = 0;

        _btnRefresh.Click += (_, _) => LoadDevices();

        gd.Controls.Add(Cap("Device:"), 0, 0);
        gd.Controls.Add(_cboDevice, 1, 0);
        gd.SetColumnSpan(_cboDevice, 2);
        gd.Controls.Add(_btnRefresh, 3, 0);

        _cboAxis.Items.AddRange(Joystick.AxisNames);
        _cboAxis.Width = 80;
        _cboAxis.Anchor = AnchorStyles.Left;
        _cboAxis.SelectedIndexChanged += (_, _) => _previewZone = 0;

        var hint = new Label
        {
            Text = "Move the handle and watch which of the six responds.",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(12, 7, 3, 3),
        };

        gd.Controls.Add(Cap("Axis:"), 0, 1);
        gd.Controls.Add(_cboAxis, 1, 1);
        gd.Controls.Add(hint, 2, 1);
        gd.SetColumnSpan(hint, 2);

        var axes = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = Joystick.AxisNames.Length,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(3, 8, 3, 3),
        };
        axes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        axes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        axes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < Joystick.AxisNames.Length; i++)
        {
            axes.Controls.Add(new Label
            {
                Text = Joystick.AxisNames[i],
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 4, 10, 4),
            }, 0, i);

            _axisValue[i] = new Label
            {
                Text = "-",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                MinimumSize = new Size(52, 0),
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(3, 4, 12, 4),
            };
            axes.Controls.Add(_axisValue[i], 1, i);

            _axisBar[i] = new ProgressBar
            {
                Minimum = 0, Maximum = 65535,
                Dock = DockStyle.Fill,
                Height = 16,
                // Without a minimum, a percent column contributes almost nothing to
                // the preferred size and the window opens with the bars squashed.
                MinimumSize = new Size(240, 16),
                Margin = new Padding(3, 4, 3, 4),
            };
            axes.Controls.Add(_axisBar[i], 2, i);
        }

        gd.Controls.Add(axes, 0, 2);
        gd.SetColumnSpan(axes, 4);

        root.Controls.Add(Group("Device and axis", gd));

        // --- Calibration -------------------------------------------------------
        var gc = Grid(5);
        for (int i = 0; i < 4; i++) gc.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _btnCalibrate.Click += OnCalibrateClick;
        _btnCalibrate.Anchor = AnchorStyles.Left;
        _btnCalibrate.Margin = new Padding(20, 3, 3, 3);

        _lblMin.Margin = new Padding(3, 6, 24, 6);
        _lblMax.Margin = new Padding(3, 6, 12, 6);

        gc.Controls.Add(Cap("Minimum:"), 0, 0);
        gc.Controls.Add(_lblMin, 1, 0);
        gc.Controls.Add(Cap("Maximum:"), 2, 0);
        gc.Controls.Add(_lblMax, 3, 0);
        gc.Controls.Add(_btnCalibrate, 4, 0);

        _chkInvert.CheckedChanged += (_, _) => { _cfg.Invert = _chkInvert.Checked; _previewZone = 0; };
        _chkInvert.Margin = new Padding(3, 10, 3, 3);
        gc.Controls.Add(_chkInvert, 0, 1);
        gc.SetColumnSpan(_chkInvert, 5);

        _chkEbInAxis.CheckedChanged += (_, _) => { _cfg.IncludeEmergencyInAxis = _chkEbInAxis.Checked; _previewZone = 0; };
        _chkEbInAxis.Margin = new Padding(3, 6, 3, 3);
        gc.Controls.Add(_chkEbInAxis, 0, 2);
        gc.SetColumnSpan(_chkEbInAxis, 5);

        _numHyst.ValueChanged += (_, _) => _cfg.Hysteresis = (double)_numHyst.Value;
        _numHyst.Anchor = AnchorStyles.Left;
        _numHyst.Margin = new Padding(3, 8, 3, 3);

        var capHyst = Cap("Hysteresis:");
        capHyst.Margin = new Padding(3, 12, 8, 6);
        gc.Controls.Add(capHyst, 0, 3);
        gc.Controls.Add(_numHyst, 1, 3);

        root.Controls.Add(Group("Calibration", gc));

        // --- Current notch -----------------------------------------------------
        var gr = Grid(1);
        gr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _lblNotch.Font = new Font(Font.FontFamily, 22F, FontStyle.Bold);
        _lblNotch.Text = "--";
        _lblNotch.Margin = new Padding(3, 2, 3, 2);
        gr.Controls.Add(_lblNotch, 0, 0);

        _lblRaw.Margin = new Padding(3, 2, 3, 8);
        gr.Controls.Add(_lblRaw, 0, 1);

        _barNotch.Dock = DockStyle.Fill;
        gr.Controls.Add(_barNotch, 0, 2);

        root.Controls.Add(Group("Current notch", gr));

        // --- Actions -----------------------------------------------------------
        var ga = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 6, 14, 4),
        };
        ga.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ga.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ga.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _btnStartStop.Click += OnStartStopClick;
        _btnStartStop.Padding = new Padding(14, 8, 14, 8);

        _btnSave.Click += OnSaveClick;
        _btnSave.Padding = new Padding(14, 8, 14, 8);

        _lblStatus.Anchor = AnchorStyles.Left;
        _lblStatus.Margin = new Padding(16, 12, 16, 3);

        ga.Controls.Add(_btnStartStop, 0, 0);
        ga.Controls.Add(_lblStatus, 1, 0);
        ga.Controls.Add(_btnSave, 2, 0);

        _lblPath.Margin = new Padding(3, 10, 3, 3);
        ga.Controls.Add(_lblPath, 0, 1);
        ga.SetColumnSpan(_lblPath, 3);

        root.Controls.Add(ga);

        Controls.Add(root);
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
            string text = $"{id} - {caps.wMid:X4}:{caps.wPid:X4} - {caps.wNumAxes} axes, {caps.wNumButtons} buttons";
            _cboDevice.Items.Add(new DeviceItem(id, text));
        }

        if (_cboDevice.Items.Count == 0)
        {
            _lblStatus.Text = "No joystick detected";
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

    private void ConfigToUi()
    {
        SelectDevice(_cfg.AxisDeviceId);
        _cboAxis.SelectedItem = _cfg.AxisName;
        if (_cboAxis.SelectedIndex < 0) _cboAxis.SelectedIndex = 0;

        _lblMin.Text = _cfg.AxisMin.ToString();
        _lblMax.Text = _cfg.AxisMax.ToString();
        _chkInvert.Checked = _cfg.Invert;
        _chkEbInAxis.Checked = _cfg.IncludeEmergencyInAxis;
        _numHyst.Value = (decimal)Math.Clamp(_cfg.Hysteresis, 0, 0.49);
        _lblPath.Text = $"Model: {_cfg.Model}   ·   buttons and hat are edited in the file\n{_configPath}";
    }

    private void UiToConfig()
    {
        int id = SelectedDeviceId();
        if (id >= 0) _cfg.AxisDeviceId = id;
        _cfg.AxisName = SelectedAxis();
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
            foreach (var lbl in _axisValue) lbl.Text = "-";
            return;
        }

        for (int i = 0; i < Joystick.AxisNames.Length; i++)
        {
            int v = Joystick.GetAxis(j, Joystick.AxisNames[i]);
            _axisValue[i].Text = v >= 0 ? v.ToString() : "-";
            _axisBar[i].Value = Math.Clamp(v, 0, 65535);
        }

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
            _lblNotch.Text = $"{s.NotchName}   0x{s.NotchValue:X2}";
            _lblRaw.Text = $"axis {s.RawAxis}   ·   {s.Fraction * 100:F1}% of travel   ·   sending to the game";
            _barNotch.Maximum = Zuiki.Notches.Length - 1;
            _barNotch.Value = Math.Clamp(s.NotchIndex, 0, _barNotch.Maximum);
            return;
        }

        if (raw < 0)
        {
            _lblNotch.Text = "--";
            _lblRaw.Text = "the selected axis does not exist on this device";
            return;
        }

        UiToConfig();
        int firstNotch = _cfg.IncludeEmergencyInAxis ? 0 : 1;
        int zones = Zuiki.Notches.Length - firstNotch;

        double p = BridgeRunner.AxisFraction(raw, _cfg);
        _previewZone = BridgeRunner.ZoneWithHysteresis(p, zones, _previewZone, _cfg.Hysteresis);

        int idx = firstNotch + _previewZone;
        var (name, value) = Zuiki.Notches[idx];

        _lblNotch.Text = $"{name}   0x{value:X2}";
        _lblRaw.Text = $"axis {raw}   ·   {p * 100:F1}% of travel   ·   preview, the bridge is stopped";
        _barNotch.Maximum = Zuiki.Notches.Length - 1;
        _barNotch.Value = Math.Clamp(idx, 0, _barNotch.Maximum);
    }

    // -------------------------------------------------------------------------
    private void OnCalibrateClick(object? sender, EventArgs e)
    {
        if (!_calibrating)
        {
            _calibrating = true;
            _calMin = int.MaxValue;
            _calMax = int.MinValue;
            _btnCalibrate.Text = "Finish";
            _lblStatus.Text = "Calibrating: move the handle end to end";
            return;
        }

        _calibrating = false;
        _btnCalibrate.Text = "Calibrate";

        if (_calMin >= _calMax)
        {
            _lblStatus.Text = "No movement seen";
            MessageBox.Show(this,
                "No movement was seen on the selected axis.\n\n" +
                "Check the device and the axis: move the handle and watch which of " +
                "the six values changes.",
                "Calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ConfigToUi();
            return;
        }

        _cfg.AxisMin = _calMin;
        _cfg.AxisMax = _calMax;
        _lblMin.Text = _calMin.ToString();
        _lblMax.Text = _calMax.ToString();
        _lblStatus.Text = _runner is { IsRunning: true } ? "Bridge running" : "Stopped";
        _previewZone = 0;
    }

    private void OnStartStopClick(object? sender, EventArgs e)
    {
        if (_runner is { IsRunning: true })
        {
            _runner.Stop();
            _runner.Dispose();
            _runner = null;

            SetEditingEnabled(true);
            _btnStartStop.Text = "Start bridge";
            _lblStatus.Text = "Stopped";
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
                $"Could not create the virtual mascon.\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                "If this is a permissions problem, run the program as administrator.",
                "Start bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "Failed to start";
            return;
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        SetEditingEnabled(false);
        _btnStartStop.Text = "Stop bridge";
        _lblStatus.Text = "Bridge running";
    }

    private void SetEditingEnabled(bool on)
    {
        // The running loop has already fixed the number of zones, so editing the
        // configuration halfway through would leave the window lying.
        _cboDevice.Enabled = on;
        _cboAxis.Enabled = on;
        _btnRefresh.Enabled = on;
        _btnCalibrate.Enabled = on;
        _chkInvert.Enabled = on;
        _chkEbInAxis.Enabled = on;
        _numHyst.Enabled = on;
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        UiToConfig();
        try
        {
            _cfg.Save(_configPath);
            _lblStatus.Text = "Configuration saved";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save:\n\n{ex.Message}", "Save",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _lblPath.MaximumSize = new Size(LogicalToDeviceUnits(640), 0);

        // Size the window to what the layout engine actually measured, with this
        // display's font and scaling already applied.
        PerformLayout();
        var pref = _root.PreferredSize;

        ClientSize = new Size(Math.Max(pref.Width, LogicalToDeviceUnits(660)), pref.Height);
        MinimumSize = Size;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        _runner?.Stop();
        _runner?.Dispose();
        base.OnFormClosing(e);
    }
}
