using System.Drawing;
using System.Drawing.Drawing2D;

namespace MasconBridge;

/// <summary>
/// One live axis reading: its letter, its raw value and a slim track. Six of these
/// are how the handle's axis gets identified, so the chosen one is highlighted and
/// the row itself selects it — the same answer the dropdown gives, at the moment the
/// lever moves and the eye is already on the row.
/// </summary>
internal sealed class AxisRow : Control
{
    private readonly Font _letterFont = Theme.Ui(9F, FontStyle.Bold);
    private readonly Font _valueFont = Theme.Mono(9F);
    private readonly int _pad;
    private readonly int _letterWidth;
    private readonly int _valueWidth;
    private readonly int _trackHeight;
    private readonly int _radius;
    private readonly int _rowHeight;

    private bool _hot;
    private bool _active;
    private bool _interactive = true;
    private int _reading = -1;

    public AxisRow(string axis)
    {
        Axis = axis;
        _pad = LogicalToDeviceUnits(10);
        _radius = LogicalToDeviceUnits(6);
        _trackHeight = LogicalToDeviceUnits(7);
        _letterWidth = Theme.Measure("W", _letterFont).Width + LogicalToDeviceUnits(12);
        _valueWidth = Theme.Measure("65535", _valueFont).Width + LogicalToDeviceUnits(14);

        Dock = DockStyle.Fill;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, LogicalToDeviceUnits(1), 0, LogicalToDeviceUnits(1));
        _rowHeight = Theme.LineHeight(_letterFont) + LogicalToDeviceUnits(8);
        Height = _rowHeight;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public string Axis { get; }

    public bool Active
    {
        get => _active;
        set { if (_active == value) return; _active = value; Invalidate(); }
    }

    /// <summary>Raw winmm reading, or -1 when the device does not report this axis.</summary>
    public int Reading
    {
        get => _reading;
        set { if (_reading == value) return; _reading = value; Invalidate(); }
    }

    /// <summary>Cleared while the bridge runs, when the axis can no longer be changed.</summary>
    public bool Interactive
    {
        get => _interactive;
        set
        {
            if (_interactive == value) return;
            _interactive = value;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            _hot = false;
            Invalidate();
        }
    }

    public event EventHandler? Chosen;

    public override Size GetPreferredSize(Size proposedSize) =>
        new(LogicalToDeviceUnits(240), _rowHeight);

    protected override void OnMouseEnter(EventArgs e) { _hot = _interactive; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var frame = new RectangleF(0, 0, Width, Height);
        if (_active)
            Theme.FillRound(g, frame, _radius, Theme.AccentWash);
        else if (_hot)
            Theme.FillRound(g, frame, _radius, Theme.Blend(Theme.Track, Theme.Card, 0.45));

        TextRenderer.DrawText(g, Axis, _letterFont,
            new Rectangle(_pad, 0, _letterWidth, Height),
            _active ? Theme.AccentDeep : Theme.Ink,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        TextRenderer.DrawText(g, _reading >= 0 ? _reading.ToString() : "-", _valueFont,
            new Rectangle(_pad + _letterWidth, 0, _valueWidth, Height),
            _active ? Theme.AccentDeep : Theme.Muted,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

        float left = _pad + _letterWidth + _valueWidth + LogicalToDeviceUnits(6);
        float width = Width - _pad - left;
        if (width <= 0) return;

        var track = new RectangleF(left, (Height - _trackHeight) / 2f, width, _trackHeight);
        Theme.FillRound(g, track, _trackHeight / 2f, Theme.Track);

        if (_reading < 0) return;

        float filled = width * Math.Clamp(_reading / 65535f, 0f, 1f);
        if (filled < 1f) return;

        // A rounded cap needs at least its own diameter to draw as anything but a smear.
        filled = Math.Min(width, Math.Max(filled, _trackHeight));

        Theme.FillRound(g, new RectangleF(track.X, track.Y, filled, track.Height),
            _trackHeight / 2f,
            _active ? Theme.Accent : Theme.Blend(Theme.TrackFill, Theme.Track, 0.4));
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (_interactive) Chosen?.Invoke(this, EventArgs.Empty);
    }
}
