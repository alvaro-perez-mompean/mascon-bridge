using System.Drawing;
using System.Drawing.Drawing2D;

namespace MasconBridge;

/// <summary>
/// A titled white card. Replaces GroupBox, whose etched frame and inset caption are
/// the strongest tell that a window was drawn in 2005.
///
/// The content sits inside the padding, so the rounded corners are never covered and
/// the child can stay opaque.
/// </summary>
internal sealed class Card : Panel
{
    private readonly Font _titleFont = Theme.Ui(10F, FontStyle.Bold);
    private readonly string _title;
    private readonly int _radius;
    private readonly int _titleTop;

    public Card(string title, Control content)
    {
        _title = title;
        _radius = LogicalToDeviceUnits(12);
        _titleTop = LogicalToDeviceUnits(12);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Fill;
        Margin = new Padding(LogicalToDeviceUnits(7), LogicalToDeviceUnits(4),
                             LogicalToDeviceUnits(7), LogicalToDeviceUnits(8));

        int side = LogicalToDeviceUnits(15);
        Padding = new Padding(side,
            _titleTop + Theme.LineHeight(_titleFont) + LogicalToDeviceUnits(8),
            side, LogicalToDeviceUnits(12));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        content.Dock = DockStyle.Fill;
        content.BackColor = Theme.Card;
        Controls.Add(content);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Page);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var frame = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Theme.FillRound(g, frame, _radius, Theme.Card);
        Theme.DrawRound(g, frame, _radius, Theme.CardEdge, 1f);

        TextRenderer.DrawText(g, _title, _titleFont, new Point(Padding.Left, _titleTop),
            Theme.Ink, TextFormatFlags.NoPadding);

        base.OnPaint(e);
    }
}
