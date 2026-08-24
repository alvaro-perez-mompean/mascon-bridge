using System.Drawing;
using System.Drawing.Drawing2D;

namespace MasconBridge;

/// <summary>
/// What the bridge is doing, as a tinted pill with a dot. It stays a Label so that
/// AutoSize keeps measuring the text; the dot and the padding around it are folded
/// into Padding, so the caption can never be squeezed by them.
/// </summary>
internal sealed class StatusPill : Label
{
    public enum Tone { Idle, Live, Busy, Warn }

    private readonly int _dot;
    private Tone _tone = Tone.Idle;

    public StatusPill()
    {
        _dot = LogicalToDeviceUnits(8);
        AutoSize = true;
        Font = Theme.Ui(9F);
        Padding = new Padding(_dot + LogicalToDeviceUnits(18), LogicalToDeviceUnits(6),
            LogicalToDeviceUnits(14), LogicalToDeviceUnits(6));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public void Show(string text, Tone tone)
    {
        _tone = tone;
        Text = text;
        Invalidate();
    }

    private Color Signal => _tone switch
    {
        Tone.Live => Color.FromArgb(0x25, 0x9E, 0x53),
        Tone.Busy => Theme.Accent,
        Tone.Warn => Theme.Brake,
        _ => Theme.Muted,
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Page);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var signal = Signal;
        var frame = new RectangleF(0, 0, Width, Height);
        Theme.FillRound(g, frame, Height / 2f, Theme.Blend(signal, Theme.Page, 0.88));

        float cy = (Height - _dot) / 2f;
        using (var brush = new SolidBrush(signal))
            g.FillEllipse(brush, LogicalToDeviceUnits(11), cy, _dot, _dot);

        TextRenderer.DrawText(g, Text, Font,
            new Rectangle(Padding.Left, 0, Width - Padding.Horizontal, Height),
            _tone == Tone.Idle ? Theme.Muted : Theme.Blend(signal, Color.Black, 0.25),
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}
