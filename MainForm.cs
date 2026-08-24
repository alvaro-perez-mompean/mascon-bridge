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
    private readonly Button _btnRefresh = new() { Text = Strings.ButtonRefresh, AutoSize = true };

    private readonly Label[] _axisValue = new Label[Joystick.AxisNames.Length];
    private readonly ProgressBar[] _axisBar = new ProgressBar[Joystick.AxisNames.Length];

    private readonly Label _lblMin = new() { AutoSize = true };
    private readonly Label _lblMax = new() { AutoSize = true };
    private readonly Button _btnCalibrate = new() { Text = Strings.ButtonCalibrate, AutoSize = true };
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

    private readonly Label _lblNotch = new() { AutoSize = true };
    private readonly Label _lblRaw = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly ProgressBar _barNotch = new()
    {
        Minimum = 0, Maximum = 14, Height = 20, MinimumSize = new Size(300, 20),
    };

    private readonly Button _btnStartStop = new() { AutoSize = true };
    private Bitmap? _playGlyph;
    private Bitmap? _stopGlyph;
    private readonly Button _btnSave = new() { Text = Strings.ButtonSaveConfiguration, AutoSize = true };
    private readonly Label _lblStatus = new() { AutoSize = true, Text = Strings.StatusStopped };
    private readonly Label _lblPath = new() { AutoSize = true, ForeColor = SystemColors.GrayText };

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

        // --- Language, first thing in the window -------------------------------
        foreach (var (code, display) in Language.Supported)
            _cboLanguage.Items.Add(new LanguageItem(code, display));
        _cboLanguage.Anchor = AnchorStyles.Left;
        _cboLanguage.Margin = new Padding(3, 3, 12, 3);
        _cboLanguage.SelectedIndexChanged += OnLanguageChanged;

        var langRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(14, 8, 14, 2),
        };
        langRow.Controls.Add(Cap(Strings.LabelLanguage));
        langRow.Controls.Add(_cboLanguage);

        var langHint = new Label
        {
            Text = Strings.HintLanguageRestart,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 7, 3, 3),
        };
        _hints.Add(langHint);
        langRow.Controls.Add(langHint);

        root.Controls.Add(langRow);

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

        gd.Controls.Add(Cap(Strings.LabelDevice), 0, 0);
        gd.Controls.Add(_cboDevice, 1, 0);
        gd.SetColumnSpan(_cboDevice, 2);
        gd.Controls.Add(_btnRefresh, 3, 0);

        _cboAxis.Items.AddRange(Joystick.AxisNames);
        _cboAxis.Width = 80;
        _cboAxis.Anchor = AnchorStyles.Left;
        _cboAxis.SelectedIndexChanged += (_, _) => _previewZone = 0;

        var hint = new Label
        {
            Text = Strings.HintAxis,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(12, 7, 3, 3),
        };
        _hints.Add(hint);

        gd.Controls.Add(Cap(Strings.LabelAxis), 0, 1);
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

        root.Controls.Add(Group(Strings.GroupDeviceAndAxis, gd));

        // --- Calibration -------------------------------------------------------
        var gc = Grid(5);
        for (int i = 0; i < 4; i++) gc.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _btnCalibrate.Click += OnCalibrateClick;
        _btnCalibrate.Anchor = AnchorStyles.Left;
        _btnCalibrate.Margin = new Padding(20, 3, 3, 3);

        _lblMin.Margin = new Padding(3, 6, 24, 6);
        _lblMax.Margin = new Padding(3, 6, 12, 6);

        gc.Controls.Add(Cap(Strings.LabelMinimum), 0, 0);
        gc.Controls.Add(_lblMin, 1, 0);
        gc.Controls.Add(Cap(Strings.LabelMaximum), 2, 0);
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

        var capHyst = Cap(Strings.LabelHysteresis);
        capHyst.Margin = new Padding(3, 12, 8, 6);
        gc.Controls.Add(capHyst, 0, 3);
        gc.Controls.Add(_numHyst, 1, 3);

        root.Controls.Add(Group(Strings.GroupCalibration, gc));

        // --- Virtual device ----------------------------------------------------
        // Both columns AutoSize: a control spanning into a Percent column is not
        // measured properly, and the hint below spans the pair.
        var gm = Grid(2);
        gm.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gm.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        foreach (var m in Zuiki.KnownModels)
            _cboModel.Items.Add(new ModelItem(m.Model, $"{m.Model}   ·   {m.Vid:X4}:{m.Pid:X4}"));

        // Width is set in OnLoad, once the display's real font is known.
        _cboModel.Anchor = AnchorStyles.Left;
        _cboModel.Margin = new Padding(3, 3, 12, 3);
        _cboModel.SelectedIndexChanged += (_, _) => _cfg.Model = SelectedModel();

        gm.Controls.Add(Cap(Strings.LabelModel), 0, 0);
        gm.Controls.Add(_cboModel, 1, 0);

        var modelHint = new Label
        {
            Text = string.Format(Strings.HintModel, Zuiki.DefaultModel),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 8, 3, 3),
        };
        _hints.Add(modelHint);
        gm.Controls.Add(modelHint, 0, 1);
        gm.SetColumnSpan(modelHint, 2);

        root.Controls.Add(Group(Strings.GroupVirtualDevice, gm));

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

        root.Controls.Add(Group(Strings.GroupCurrentNotch, gr));

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
        _btnStartStop.ImageAlign = ContentAlignment.MiddleLeft;
        _btnStartStop.TextAlign = ContentAlignment.MiddleRight;
        _btnStartStop.TextImageRelation = TextImageRelation.ImageBeforeText;

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
            string text = string.Format(Strings.DeviceItem,
                id, $"{caps.wMid:X4}:{caps.wPid:X4}", caps.wNumAxes, caps.wNumButtons);
            _cboDevice.Items.Add(new DeviceItem(id, text));
        }

        if (_cboDevice.Items.Count == 0)
        {
            _lblStatus.Text = Strings.StatusNoJoystick;
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

    private void ConfigToUi()
    {
        SelectDevice(_cfg.AxisDeviceId);
        _cboAxis.SelectedItem = _cfg.AxisName;
        if (_cboAxis.SelectedIndex < 0) _cboAxis.SelectedIndex = 0;

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
        _numHyst.Value = (decimal)Math.Clamp(_cfg.Hysteresis, 0, 0.49);
        _lblPath.Text = string.Format(Strings.HintButtonsInFile, _configPath);
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
            _lblRaw.Text = string.Format(Strings.NotchSending, s.RawAxis, s.Fraction * 100);
            _barNotch.Maximum = Zuiki.Notches.Length - 1;
            _barNotch.Value = Math.Clamp(s.NotchIndex, 0, _barNotch.Maximum);
            return;
        }

        if (raw < 0)
        {
            _lblNotch.Text = "--";
            _lblRaw.Text = Strings.NotchAxisMissing;
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
        _lblRaw.Text = string.Format(Strings.NotchPreview, raw, p * 100);
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
            _btnCalibrate.Text = Strings.ButtonCalibrateFinish;
            _lblStatus.Text = Strings.StatusCalibrating;
            return;
        }

        _calibrating = false;
        _btnCalibrate.Text = Strings.ButtonCalibrate;

        if (_calMin >= _calMax)
        {
            _lblStatus.Text = Strings.StatusNoMovement;
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
        _lblStatus.Text = _runner is { IsRunning: true } ? Strings.StatusBridgeRunning : Strings.StatusStopped;
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
            ShowStartStop(running: false);
            _lblStatus.Text = Strings.StatusStopped;
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
            _lblStatus.Text = Strings.StatusFailedToStart;
            return;
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        SetEditingEnabled(false);
        ShowStartStop(running: true);
        _lblStatus.Text = Strings.StatusBridgeRunning;
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
    }

    // Drawn rather than taken from a font: the obvious characters for this, U+25B6
    // and U+25A0, come out as colour emoji in some Windows fonts, and the Japanese
    // interface makes that more likely. Drawing also scales with the display.
    private static Bitmap PlayGlyph(int size, Color colour)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        float inset = size * 0.18f;
        var triangle = new[]
        {
            new PointF(inset, inset * 0.7f),
            new PointF(inset, size - inset * 0.7f),
            new PointF(size - inset, size / 2f),
        };
        using var brush = new SolidBrush(colour);
        g.FillPolygon(brush, triangle);
        return bmp;
    }

    private static Bitmap StopGlyph(int size, Color colour)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        float inset = size * 0.22f;
        using var brush = new SolidBrush(colour);
        g.FillRectangle(brush, inset, inset, size - inset * 2, size - inset * 2);
        return bmp;
    }

    /// <summary>Caption and glyph always change together, so they cannot disagree.</summary>
    private void ShowStartStop(bool running)
    {
        _btnStartStop.Text = running ? Strings.ButtonStopBridge : Strings.ButtonStartBridge;
        _btnStartStop.Image = running ? _stopGlyph : _playGlyph;
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
            _lblStatus.Text = Strings.StatusConfigurationSaved;
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

        // Cap the explanatory text so a long line wraps rather than widening the
        // whole window. Without this, editing one sentence resizes the panel.
        int cap = LogicalToDeviceUnits(640);
        // Sized from the button's own font, so it tracks the display scaling.
        int glyph = (int)Math.Round(_btnStartStop.Font.GetHeight() * 0.95);
        _playGlyph = PlayGlyph(glyph, Color.FromArgb(0x2E, 0x7D, 0x32));
        _stopGlyph = StopGlyph(glyph, Color.FromArgb(0xC6, 0x28, 0x28));
        ShowStartStop(_runner is { IsRunning: true });

        _lblPath.MaximumSize = new Size(LogicalToDeviceUnits(640), 0);
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

        ClientSize = new Size(Math.Max(pref.Width, LogicalToDeviceUnits(660)), pref.Height);
        MinimumSize = Size;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        _playGlyph?.Dispose();
        _stopGlyph?.Dispose();
        _runner?.Stop();
        _runner?.Dispose();
        base.OnFormClosing(e);
    }
}
