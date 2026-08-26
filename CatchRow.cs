using System.Drawing;

namespace MasconBridge;

/// <summary>
/// One catch on the handle: whether it is on, and which button releases it.
///
/// The button is chosen by pressing it rather than by typing its number. The numbers
/// only exist in the "list" output, and at the moment of setting this up the hand is
/// already on the stick.
/// </summary>
internal sealed class CatchRow : FlowLayoutPanel
{
    private readonly CheckBox _check;
    private readonly FlatButton _learn = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonLearnRelease,
    };
    private readonly Label _binding = new() { AutoSize = true, ForeColor = Theme.Muted };

    private bool _learning;
    private bool _interactive = true;

    public CatchRow(string caption)
    {
        _check = new CheckBox { Text = caption, AutoSize = true };
        _check.CheckedChanged += OnCheckedChanged;
        _learn.Click += OnLearnClick;

        // One line: the checkbox names which end of the handle and the picker sits
        // beside it. What the catches actually are is said once, under both rows.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = false;
        Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(6));

        _check.Margin = new Padding(3, LogicalToDeviceUnits(8), LogicalToDeviceUnits(14), 3);
        _binding.Margin = new Padding(LogicalToDeviceUnits(10), LogicalToDeviceUnits(8), 0, 0);

        Controls.Add(_check);
        Controls.Add(_learn);
        Controls.Add(_binding);

        ShowBinding();
    }

    /// <summary>Raised when the catch is switched off or bound to a different button.</summary>
    public event EventHandler? Changed;

    /// <summary>-1 when the catch is off, which is also how it is stored.</summary>
    public int DeviceId { get; private set; } = -1;

    public int Button { get; private set; } = 1;

    public bool Learning => _learning;

    /// <summary>What the caption needs, so both rows can be given the widest.</summary>
    public int CaptionWidth => _check.PreferredSize.Width;

    /// <summary>
    /// Lines the pickers up under each other. Set once the display's real font is
    /// known, since a caption measured with the wrong one aligns to nothing.
    /// </summary>
    public void AlignCaption(int width) => _check.MinimumSize = new Size(width, 0);

    /// <summary>False while the bridge runs: the binding cannot be changed under it.</summary>
    public bool Interactive
    {
        get => _interactive;
        set
        {
            _interactive = value;
            if (!value) StopLearning();
            _check.Enabled = value;
            ShowBinding();
        }
    }

    public void SetBinding(int deviceId, int button)
    {
        DeviceId = deviceId;
        Button = button;

        _check.CheckedChanged -= OnCheckedChanged;
        _check.Checked = deviceId >= 0;
        _check.CheckedChanged += OnCheckedChanged;

        ShowBinding();
    }

    /// <summary>
    /// A joystick to leave out of the scan, as "33DD:0002", or null for none. The
    /// bridge's own mascon while it runs — see <see cref="DeviceMap.Ignoring"/>.
    /// </summary>
    public string? IgnoreDevice { get; set; }

    /// <summary>
    /// Watches every joystick, not just the one the handle is on: the release button
    /// is as likely to be on the stick as on the throttle.
    /// </summary>
    public void CaptureWhileLearning()
    {
        if (!_learning) return;

        foreach (var (id, _) in DeviceMap.Ignoring(Joystick.Enumerate(), IgnoreDevice))
        {
            if (!Joystick.TryRead(id, out var j)) continue;

            var pressed = Joystick.PressedButtons(j);
            if (pressed.Count == 0) continue;

            DeviceId = id;
            Button = pressed[0];
            StopLearning();
            ShowBinding();
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
    }

    private void OnCheckedChanged(object? sender, EventArgs e)
    {
        if (_check.Checked)
        {
            // Ticking the box with nothing bound goes straight to choosing a button,
            // rather than leaving a setting that is on and does nothing.
            if (DeviceId < 0) StartLearning();
        }
        else
        {
            StopLearning();
            DeviceId = -1;
        }

        ShowBinding();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnLearnClick(object? sender, EventArgs e)
    {
        if (_learning) StopLearning();
        else StartLearning();

        ShowBinding();
    }

    private void StartLearning()
    {
        _learning = true;
        _learn.Text = Strings.ButtonLearnCancel;
    }

    private void StopLearning()
    {
        _learning = false;
        _learn.Text = Strings.ButtonLearnRelease;
    }

    private void ShowBinding()
    {
        _binding.Text = _learning
            ? Strings.HintReleasePress
            : DeviceId >= 0 && _check.Checked
                ? string.Format(Strings.HintReleaseBinding, DeviceId, Button)
                : Strings.HintReleaseNotSet;

        _learn.Enabled = _interactive && _check.Checked;
    }
}
