using System.Drawing;

namespace MasconBridge;

/// <summary>
/// One mascon button, or the hat, and what is bound to it. The physical button is
/// chosen by pressing it: the numbers only exist in the "list" output, and the hand
/// is already on the stick when this is being set up.
/// </summary>
internal sealed class BindingRow : TableLayoutPanel
{
    private readonly Label _caption;
    private readonly Label _binding = new() { AutoSize = true, ForeColor = Theme.Muted };
    private readonly FlatButton _learn = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonLearnRelease,
    };
    private readonly FlatButton _clear = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonClearBinding,
    };

    private readonly bool _hat;
    private bool _learning;

    /// <summary>A hat row learns a device; a button row learns a device and a button.</summary>
    public BindingRow(string mascon, string caption, bool hat = false)
    {
        Mascon = mascon;
        _hat = hat;

        _caption = new Label
        {
            Text = caption,
            AutoSize = true,
            Font = Theme.Ui(9.75F, FontStyle.Bold),
            ForeColor = Theme.Ink,
            Anchor = AnchorStyles.Left,
        };

        ColumnCount = 4;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(4));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(78)));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _caption.Margin = new Padding(3, LogicalToDeviceUnits(8), LogicalToDeviceUnits(8), 3);
        _binding.Margin = new Padding(3, LogicalToDeviceUnits(8), LogicalToDeviceUnits(8), 3);
        _binding.Anchor = AnchorStyles.Left;

        _learn.Click += (_, _) => { if (_learning) StopLearning(); else StartLearning(); };
        _clear.Click += (_, _) => { StopLearning(); Cleared?.Invoke(this, EventArgs.Empty); };

        Controls.Add(_caption, 0, 0);
        Controls.Add(_binding, 1, 0);
        Controls.Add(_learn, 2, 0);
        Controls.Add(_clear, 3, 0);
    }

    /// <summary>The mascon button this row stands for, or "Hat".</summary>
    public string Mascon { get; }

    public bool Learning => _learning;

    /// <summary>A physical button was pressed while this row was listening.</summary>
    public event EventHandler<(int DeviceId, int Button)>? Captured;

    /// <summary>The clear button was pressed.</summary>
    public event EventHandler? Cleared;

    /// <summary>Raised when this row starts listening, so the others stop.</summary>
    public event EventHandler? LearningStarted;

    /// <summary>What is bound now, already worded for the display.</summary>
    public void Show(string? bound)
    {
        _bound = bound;
        _binding.Text = _learning
            ? _hat ? Strings.HintHatPress : Strings.HintReleasePress
            : string.IsNullOrEmpty(bound) ? Strings.HintBindingNone : bound;
        _clear.Enabled = !string.IsNullOrEmpty(bound);
    }

    private string? _bound;

    public void StopLearning()
    {
        if (!_learning) return;
        _learning = false;
        _learn.Text = Strings.ButtonLearnRelease;
        Show(_bound);
    }

    private void StartLearning()
    {
        _learning = true;
        _learn.Text = Strings.ButtonLearnCancel;
        LearningStarted?.Invoke(this, EventArgs.Empty);
        Show(_bound);
    }

    /// <summary>
    /// Watches every joystick, not only the one the handle is on: the buttons are as
    /// likely to live on the stick as on the throttle.
    /// </summary>
    public void CaptureWhileLearning()
    {
        if (!_learning) return;

        foreach (var (id, _) in Joystick.Enumerate())
        {
            if (!Joystick.TryRead(id, out var j)) continue;

            var pressed = Joystick.PressedButtons(j);
            if (pressed.Count == 0) continue;

            StopLearning();
            Captured?.Invoke(this, (id, pressed[0]));
            return;
        }
    }
}
