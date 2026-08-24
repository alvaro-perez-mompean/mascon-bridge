using System.Drawing;
using System.Drawing.Drawing2D;

namespace MasconBridge;

/// <summary>
/// A flat, rounded button. The system button is drawn by the theme and cannot be
/// given a fill, so the one control the window is built around would otherwise look
/// exactly like the two beside it.
/// </summary>
internal sealed class FlatButton : Button
{
    public enum Look
    {
        /// <summary>White with a hairline border: everything that is not the main action.</summary>
        Plain,

        /// <summary>Filled with the accent: the one action the window exists for.</summary>
        Primary,

        /// <summary>Filled red: the same button once it undoes what it did.</summary>
        Danger,
    }

    /// <summary>
    /// Drawn rather than taken from a font: U+25B6 and U+25A0 come out as colour
    /// emoji in some Windows fonts, which the Japanese interface makes likelier
    /// still, and drawing sizes them from the button's own font.
    /// </summary>
    public enum Mark { None, Play, Stop, Save, Buttons }

    private readonly int _radius;
    private readonly int _gap;
    private Look _look;
    private bool _hot;
    private bool _held;

    public FlatButton(Look look)
    {
        _look = look;
        _radius = LogicalToDeviceUnits(7);
        _gap = LogicalToDeviceUnits(9);

        AutoSize = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        bool plain = look == Look.Plain;
        Font = plain ? Theme.Ui(9F) : Theme.Ui(9.75F, FontStyle.Bold);
        Padding = new Padding(LogicalToDeviceUnits(plain ? 14 : 18), LogicalToDeviceUnits(plain ? 6 : 9),
            LogicalToDeviceUnits(plain ? 14 : 18), LogicalToDeviceUnits(plain ? 6 : 9));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    private Mark _glyph;

    /// <summary>Only ever moves between Primary and Danger, which share their metrics.</summary>
    public Look Fill
    {
        get => _look;
        set { if (_look == value) return; _look = value; Invalidate(); }
    }

    public Mark Glyph
    {
        get => _glyph;
        set
        {
            if (_glyph == value) return;
            _glyph = value;
            PerformLayout();
            Invalidate();
        }
    }

    /// <summary>
    /// The play and stop marks are solid shapes and read at anything. The disk and
    /// the pad buttons have detail inside them, so they get more room: at the size
    /// of the triangle, the disk's shutter and label ate the body and it came out
    /// looking like a letter H.
    /// </summary>
    private int GlyphSize => (int)Math.Round(
        Theme.LineHeight(Font) * (Glyph is Mark.Save or Mark.Buttons ? 0.78 : 0.60));

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = Theme.Measure(Text, Font);
        int width = text.Width + Padding.Horizontal;
        if (Glyph != Mark.None) width += GlyphSize + _gap;

        return new Size(width, Math.Max(text.Height, GlyphSize) + Padding.Vertical);
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; _held = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _held = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _held = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { PerformLayout(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Page);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var (fill, edge, ink) = Palette();
        var frame = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

        Theme.FillRound(g, frame, _radius, fill);
        if (edge.A > 0) Theme.DrawRound(g, frame, _radius, edge, 1f);

        if (Focused && ShowFocusCues)
            Theme.DrawRound(g, RectangleF.Inflate(frame, -2f, -2f), _radius - 2,
                _look == Look.Plain ? Theme.Accent : Theme.Blend(fill, Color.White, 0.75), 1.5f);

        var text = Theme.Measure(Text, Font);
        int glyph = Glyph == Mark.None ? 0 : GlyphSize;
        int content = text.Width + (glyph == 0 ? 0 : glyph + _gap);
        int x = (Width - content) / 2;

        if (glyph > 0)
        {
            DrawGlyph(g, new RectangleF(x, (Height - glyph) / 2f, glyph, glyph), ink, fill);
            x += glyph + _gap;
        }

        TextRenderer.DrawText(g, Text, Font,
            new Rectangle(x, (Height - text.Height) / 2, text.Width, text.Height),
            ink, TextFormatFlags.NoPadding);
    }

    private (Color Fill, Color Edge, Color Ink) Palette()
    {
        if (!Enabled) return (Theme.Disabled, Color.Transparent, Theme.DisabledInk);

        double press = _held ? 1.0 : _hot ? 0.5 : 0;

        return _look switch
        {
            Look.Primary => (Theme.Blend(Theme.Accent, Theme.AccentDeep, press),
                Color.Transparent, Color.White),
            Look.Danger => (Theme.Blend(Theme.Brake, Theme.BrakeDeep, press),
                Color.Transparent, Color.White),
            _ => (Theme.Blend(Theme.Card, Theme.Track, press * 0.7),
                Theme.Blend(Theme.CardEdge, Theme.Muted, _hot ? 0.35 : 0), Theme.Ink),
        };
    }

    /// <summary>
    /// Drawn, never typed. The cut outs are painted in the button's own fill rather
    /// than left empty, so a glyph reads the same over white, over the accent and
    /// over the disabled grey.
    /// </summary>
    private void DrawGlyph(Graphics g, RectangleF r, Color colour, Color fill)
    {
        using var brush = new SolidBrush(colour);

        if (Glyph == Mark.Save)
        {
            // A floppy disk: still the one shape everybody reads as "save". Three
            // things make it legible at this size, and it was unrecognisable without
            // them: the clipped top right corner, the shutter running off the top
            // edge, and the label running off the bottom one. Cut outs that float in
            // the middle of the body read as a battery, not a disk.
            float s = r.Height;
            float chamfer = s * 0.26f;
            float radius = s * 0.11f;

            using var body = new GraphicsPath();
            body.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            body.AddLine(r.X + radius, r.Y, r.Right - chamfer, r.Y);
            body.AddLine(r.Right - chamfer, r.Y, r.Right, r.Y + chamfer);
            body.AddLine(r.Right, r.Y + chamfer, r.Right, r.Bottom - radius);
            body.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            body.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            body.CloseFigure();
            g.FillPath(brush, body);

            using var cut = new SolidBrush(fill);
            // The shutter floats rather than running off the top edge. Cutting it to
            // the edge splits the top of the body into two shoulders, and at this
            // size two shoulders over a wide label read as the letter H.
            g.FillRectangle(cut, r.X + s * 0.32f, r.Y + s * 0.13f, s * 0.30f, s * 0.26f);
            // The label does meet the bottom edge, which is what makes it a label
            // rather than another slot.
            g.FillRectangle(cut, r.X + s * 0.24f, r.Y + s * 0.58f, s * 0.52f, s * 0.42f + 1f);
            return;
        }

        if (Glyph == Mark.Buttons)
        {
            // Two buttons on a pad, side by side, one pressed and one not: the thing
            // being assigned, and not a keyboard, which this program never touches.
            // Stacked on the diagonal they touched at the corners and read as one
            // blob; in a row they stay two.
            float d = r.Height * 0.48f;
            float y = r.Y + (r.Height - d) / 2f;
            g.FillEllipse(brush, r.X, y, d, d);

            using var pen = new Pen(colour, Math.Max(1.2f, r.Height * 0.12f));
            g.DrawEllipse(pen, r.Right - d + pen.Width / 2f, y + pen.Width / 2f,
                d - pen.Width, d - pen.Width);
            return;
        }

        if (Glyph == Mark.Play)
        {
            float inset = r.Height * 0.06f;
            g.FillPolygon(brush, new[]
            {
                new PointF(r.X, r.Y + inset),
                new PointF(r.X, r.Bottom - inset),
                new PointF(r.Right, r.Y + r.Height / 2f),
            });
            return;
        }

        float side = r.Height * 0.86f;
        Theme.FillRound(g, new RectangleF(r.X + (r.Width - side) / 2f, r.Y + (r.Height - side) / 2f,
            side, side), side * 0.16f, colour);
    }
}
