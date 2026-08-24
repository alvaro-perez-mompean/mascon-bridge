using System.Drawing;
using System.Drawing.Drawing2D;

namespace MasconBridge;

/// <summary>
/// The handle position, drawn as the scale it actually is: EB, eight brake steps,
/// neutral and five power steps, lit one at a time.
///
/// A progress bar says how far along the travel the handle is, which is the one thing
/// about a mascon that does not matter — the notch is a named position, not a
/// percentage. Reading the whole scale at once also makes the shape of the mapping
/// visible, so a badly calibrated axis shows itself.
/// </summary>
internal sealed class NotchDisplay : Control
{
    private readonly Font _captionFont = Theme.Ui(8.25F, FontStyle.Bold);
    private readonly Font _nameFont = Theme.Ui(23F, FontStyle.Bold);
    private readonly Font _valueFont = Theme.Mono(13F, FontStyle.Bold);
    private readonly Font _tickFont = Theme.Ui(7.5F, FontStyle.Bold);
    private readonly Font _detailFont = Theme.Ui(8.25F);

    private readonly int _pad;
    private readonly int _radius;
    private readonly int _wellHeight;
    private readonly int _cellGap;
    private readonly int _leading;

    private int _index = -1;
    private bool _emergencyHeld;
    private bool _powerHeld;
    private string _detail = string.Empty;

    public NotchDisplay(string caption)
    {
        Caption = caption;
        _pad = LogicalToDeviceUnits(17);
        _radius = LogicalToDeviceUnits(14);
        _wellHeight = LogicalToDeviceUnits(30);
        _cellGap = Math.Max(1, LogicalToDeviceUnits(3));

        // The line box of 23pt type carries more air above the capitals than the
        // padding below the panel, which leaves it looking top heavy. Taken off the
        // top so the block sits optically centred.
        _leading = LogicalToDeviceUnits(10);

        Dock = DockStyle.Fill;
        AutoSize = true;
        Margin = new Padding(LogicalToDeviceUnits(14), LogicalToDeviceUnits(4),
                             LogicalToDeviceUnits(14), LogicalToDeviceUnits(10));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public string Caption { get; }

    /// <summary>Index into Zuiki.Notches, or -1 when there is no reading to show.</summary>
    public int Index
    {
        get => _index;
        set { if (_index == value) return; _index = value; Invalidate(); }
    }

    /// <summary>
    /// True while the catch is holding the handle at B8. EB keeps its place on the
    /// scale as an outline: it is still there, it is just not being allowed.
    /// </summary>
    public bool EmergencyHeld
    {
        get => _emergencyHeld;
        set { if (_emergencyHeld == value) return; _emergencyHeld = value; Invalidate(); }
    }

    /// <summary>
    /// True while the release button is holding the handle at N. The power steps go
    /// back to outlines, the same way EB does: the shape stays, so what is being
    /// withheld is still legible.
    /// </summary>
    public bool PowerHeld
    {
        get => _powerHeld;
        set { if (_powerHeld == value) return; _powerHeld = value; Invalidate(); }
    }

    public string Detail
    {
        get => _detail;
        set { if (_detail == value) return; _detail = value; Invalidate(); }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int height = _pad - _leading
            + Theme.LineHeight(_nameFont) + LogicalToDeviceUnits(13)
            + _wellHeight + LogicalToDeviceUnits(6)
            + Theme.LineHeight(_tickFont) + LogicalToDeviceUnits(13)
            + Theme.LineHeight(_detailFont)
            + _pad;

        return new Size(LogicalToDeviceUnits(420), height);
    }

    /// <summary>
    /// Red for the brake side, green for neutral, blue for power, as on the device.
    /// Taken from the notch's name rather than its position, so it cannot drift if the
    /// table is ever read in a different order.
    /// </summary>
    private static Color NotchColour(int index) => Zuiki.Notches[index].Name switch
    {
        "EB" => Theme.Emergency,
        "N" => Theme.Neutral,
        ['B', ..] => Theme.Brake,
        _ => Theme.Power,
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Page);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var frame = new RectangleF(0, 0, Width, Height);
        Theme.FillRound(g, frame, _radius, Theme.Panel);
        // Keeps the panel from reading as a hole cut in the page.
        Theme.DrawRound(g, RectangleF.Inflate(frame, -0.5f, -0.5f), _radius,
            Color.FromArgb(26, 255, 255, 255), 1f);

        int y = PaintHeadline(g, _pad - _leading);
        y = PaintScale(g, y);

        TextRenderer.DrawText(g, Detail, _detailFont, new Point(_pad, y),
            Theme.PanelMuted, TextFormatFlags.NoPadding);
    }

    private int PaintHeadline(Graphics g, int y)
    {
        int line = Theme.LineHeight(_nameFont);
        bool known = _index >= 0 && _index < Zuiki.Notches.Length;
        string name = known ? Zuiki.Notches[_index].Name : "--";

        var size = Theme.Measure(name, _nameFont);
        TextRenderer.DrawText(g, name, _nameFont, new Rectangle(_pad, y, size.Width, line),
            known ? Theme.PanelInk : Theme.PanelMuted,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);

        if (known)
        {
            // Sat on the same baseline as the name rather than centred on it: the hex
            // is a footnote to the notch, not a second headline.
            string hex = $"0x{Zuiki.Notches[_index].Value:X2}";
            int valueLine = Theme.LineHeight(_valueFont);
            TextRenderer.DrawText(g, hex, _valueFont,
                new Rectangle(_pad + size.Width + LogicalToDeviceUnits(16),
                    y + line - valueLine - LogicalToDeviceUnits(4),
                    Theme.Measure(hex, _valueFont).Width, valueLine),
                Theme.Blend(NotchColour(_index), Theme.Panel, 0.15),
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
        }

        // The caption rides on the headline rather than sitting above it: naming the
        // panel is worth a corner, not a line of its own.
        TextRenderer.DrawText(g, Caption, _captionFont,
            new Rectangle(_pad, y, Width - _pad * 2, line), Theme.PanelMuted,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

        if (_powerHeld || _emergencyHeld)
        {
            int glyph = Theme.LineHeight(_captionFont);
            int right = Width - _pad - Theme.Measure(Caption, _captionFont).Width
                        - LogicalToDeviceUnits(8);
            PaintPadlock(g, new Rectangle(right - glyph, y + (line - glyph) / 2, glyph, glyph),
                Theme.Blend(Theme.Emergency, Theme.Panel, 0.2));
        }

        return y + line + LogicalToDeviceUnits(13);
    }

    private int PaintScale(Graphics g, int y)
    {
        int count = Zuiki.Notches.Length;
        float left = _pad;
        float width = Width - _pad * 2;
        if (width <= 0) return y + _wellHeight;

        var well = new RectangleF(left - LogicalToDeviceUnits(6), y - LogicalToDeviceUnits(5),
            width + LogicalToDeviceUnits(12), _wellHeight + LogicalToDeviceUnits(10));
        Theme.FillRound(g, well, LogicalToDeviceUnits(11), Theme.PanelWell);

        float cell = (width - _cellGap * (count - 1)) / count;
        float radius = Math.Min(LogicalToDeviceUnits(4), cell / 2f);
        int labelTop = y + _wellHeight + LogicalToDeviceUnits(6);
        int labelLine = Theme.LineHeight(_tickFont);

        for (int i = 0; i < count; i++)
        {
            var r = new RectangleF(left + i * (cell + _cellGap), y, cell, _wellHeight);
            var colour = NotchColour(i);
            bool live = i == _index;
            bool onHandle = !(_emergencyHeld && i < Zuiki.FullServiceIndex)
                            && !(_powerHeld && i > Zuiki.NeutralIndex);

            if (live)
                PaintLive(g, r, radius, colour);
            else if (onHandle)
                Theme.FillRound(g, r, radius, Theme.Blend(colour, Theme.PanelWell, 0.68));
            else
                Theme.DrawRound(g, RectangleF.Inflate(r, -0.5f, -0.5f), radius,
                    Theme.Blend(colour, Theme.PanelWell, 0.62), 1f);

            TextRenderer.DrawText(g, Zuiki.Notches[i].Name, _tickFont,
                new Rectangle((int)r.X, labelTop, (int)Math.Ceiling(r.Width), labelLine),
                live ? Theme.PanelInk : Theme.Blend(colour, Theme.Panel, onHandle ? 0.5 : 0.72),
                TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter);
        }

        return labelTop + labelLine + LogicalToDeviceUnits(13);
    }

    /// <summary>
    /// Drawn rather than typed: the padlock characters render as colour emoji in
    /// some Windows fonts, and drawing keeps it sized from the caption's own font.
    /// </summary>
    private static void PaintPadlock(Graphics g, Rectangle r, Color colour)
    {
        float thickness = Math.Max(1.2f, r.Height / 9f);
        float bodyTop = r.Y + r.Height * 0.46f;
        var body = new RectangleF(r.X + r.Width * 0.12f, bodyTop,
            r.Width * 0.76f, r.Bottom - bodyTop);

        using var pen = new Pen(colour, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var shackle = new RectangleF(r.X + r.Width * 0.27f, r.Y + thickness / 2f,
            r.Width * 0.46f, r.Height * 0.52f);
        g.DrawArc(pen, shackle, 180f, 180f);

        Theme.FillRound(g, body, Math.Max(1f, r.Width * 0.14f), colour);
    }

    private void PaintLive(Graphics g, RectangleF r, float radius, Color colour)
    {
        for (int ring = 3; ring >= 1; ring--)
            Theme.FillRound(g, RectangleF.Inflate(r, ring * 2f, ring * 2f), radius + ring * 2f,
                Color.FromArgb(20, colour));

        using var path = Theme.Round(r, radius);
        using var brush = new LinearGradientBrush(
            new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
            Theme.Blend(colour, Color.White, 0.34), colour, LinearGradientMode.Vertical);
        g.FillPath(brush, path);

        // A hairline along the top edge: enough to make the lit notch read as raised.
        Theme.FillRound(g, new RectangleF(r.X + radius, r.Y + 1f, r.Width - radius * 2, 1.4f),
            0.7f, Color.FromArgb(150, Color.White));
    }
}
