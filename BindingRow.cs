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

    // Not AutoSize: the column is a fixed width shared by every row, so the captions
    // line up, and a label that sized itself would push past it into the next cell.
    private readonly Label _function = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true,
        ForeColor = Theme.Ink,
    };
    private readonly FlatButton _learn = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonLearnRelease,
    };
    private readonly FlatButton _clear = new(FlatButton.Look.Plain)
    {
        Text = Strings.ButtonClearBinding,
    };

    /// <summary>Buttons are numbered from 1, so nothing is ever button zero.</summary>
    private const int NoButton = 0;

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

        ColumnCount = 5;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(4));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(78)));
        // Zero until a game is chosen, which is what keeps the page looking exactly
        // as it did before there were games.
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _caption.Margin = new Padding(3, LogicalToDeviceUnits(8), LogicalToDeviceUnits(8), 3);
        _binding.Margin = new Padding(3, LogicalToDeviceUnits(8), LogicalToDeviceUnits(8), 3);
        _binding.Anchor = AnchorStyles.Left;
        _function.Margin = new Padding(3, LogicalToDeviceUnits(8), LogicalToDeviceUnits(8), 3);

        _learn.Click += (_, _) => { if (_learning) StopLearning(); else StartLearning(); };
        _clear.Click += (_, _) => { StopLearning(); Cleared?.Invoke(this, EventArgs.Empty); };

        Controls.Add(_caption, 0, 0);
        Controls.Add(_function, 1, 0);
        Controls.Add(_binding, 2, 0);
        Controls.Add(_learn, 3, 0);
        Controls.Add(_clear, 4, 0);
    }

    /// <summary>The mascon button this row stands for, or "Hat".</summary>
    public string Mascon { get; }

    /// <summary>
    /// A joystick to leave out of the scan, as "33DD:0002", or null for none. The
    /// bridge's own mascon while it runs — see <see cref="DeviceMap.Ignoring"/>.
    /// </summary>
    public string? IgnoreDevice { get; set; }

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

    /// <summary>
    /// What this button does in the chosen game. <paramref name="textWidth"/> is how
    /// wide the widest caption on the page is, which the caller measures because the
    /// number is shared: every row is a table of its own, so only the same width in
    /// all of them lines the captions up. Zero means no game, and no column at all.
    ///
    /// The margins are added here rather than by the caller: they belong to this
    /// label, and a column measured without them clips the longest caption.
    /// </summary>
    public void ShowFunction(GameProfile.Caption? caption, int textWidth)
    {
        _function.Text = caption?.Text ?? string.Empty;
        _function.ForeColor = caption?.Tone switch
        {
            GameProfile.Tone.Avoid => Theme.Brake,
            GameProfile.Tone.Unknown => Theme.Muted,
            _ => Theme.Ink,
        };
        ColumnStyles[1].Width = textWidth <= 0
            ? 0
            : textWidth + _function.Margin.Horizontal + LogicalToDeviceUnits(8);
    }

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

        foreach (var (id, _) in DeviceMap.Ignoring(Joystick.Enumerate(), IgnoreDevice))
        {
            if (!Joystick.TryRead(id, out var j)) continue;

            // A hat row learns a device rather than a button, so pushing the hat
            // answers the question as well as pressing a button does — and it is
            // what the hand reaches for. There is no button to report with it: hat
            // rows carry a device and nothing else.
            if (_hat && Joystick.IsHatPushed(j))
            {
                StopLearning();
                Captured?.Invoke(this, (id, NoButton));
                return;
            }

            var pressed = Joystick.PressedButtons(j);
            if (pressed.Count == 0) continue;

            StopLearning();
            Captured?.Invoke(this, (id, pressed[0]));
            return;
        }
    }
}
