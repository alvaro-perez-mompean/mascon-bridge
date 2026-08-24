using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MasconBridge;

/// <summary>
/// The handle position on top of the game: the fifteen notches as a vertical strip
/// laid out like the lever itself, emergency at the top and full power at the bottom,
/// with the notch named beside it.
///
/// It never takes the focus and the mouse goes straight through it, so it cannot be
/// clicked by mistake while driving. Placing it is a deliberate mode: the panel
/// unlocks it, and then it can be dragged.
///
/// A plain topmost window only draws over a game that is windowed or borderless. A
/// game in exclusive fullscreen owns the display and nothing short of hooking its
/// renderer would appear over it, which this does not do.
/// </summary>
internal sealed class OverlayWindow : Form
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x8000000;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 2;

    private const byte AC_SRC_OVER = 0;
    private const byte AC_SRC_ALPHA = 1;
    private const int ULW_ALPHA = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
        ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey,
        ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr h);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly Font _nameFont;
    private readonly Font _tickFont;

    private readonly int _pad;
    private readonly int _nameWidth;
    private readonly int _stripWidth;
    private readonly int _cellHeight;
    private readonly int _cellGap;

    private int _index = -1;
    private bool _powerHeld;
    private bool _emergencyHeld;
    private bool _movable;

    public OverlayWindow()
    {
        _nameFont = Theme.Ui(19F, FontStyle.Bold);
        _tickFont = Theme.Ui(6.75F, FontStyle.Bold);

        _pad = LogicalToDeviceUnits(11);
        _nameWidth = LogicalToDeviceUnits(46);
        _stripWidth = LogicalToDeviceUnits(26);
        _cellHeight = LogicalToDeviceUnits(12);
        _cellGap = Math.Max(1, LogicalToDeviceUnits(2));

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        int count = Zuiki.Notches.Length;
        ClientSize = new Size(
            _pad * 2 + _nameWidth + LogicalToDeviceUnits(9) + _stripWidth,
            _pad * 2 + count * _cellHeight + (count - 1) * _cellGap);

    }

    /// <summary>Showing it must never pull the focus away from the game.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            return cp;
        }
    }

    /// <summary>
    /// While it is not movable the mouse passes through, so a click meant for the
    /// game is never eaten by the strip sitting on top of it.
    /// </summary>
    public bool Movable
    {
        get => _movable;
        set
        {
            if (_movable == value) return;
            _movable = value;
            ApplyClickThrough();
            Redraw();
        }
    }

    /// <summary>Raised when it has been dragged, so the new position can be stored.</summary>
    public event EventHandler? Moved;

    public void SetNotch(int index, bool powerHeld, bool emergencyHeld)
    {
        if (index == _index && powerHeld == _powerHeld && emergencyHeld == _emergencyHeld) return;

        _index = index;
        _powerHeld = powerHeld;
        _emergencyHeld = emergencyHeld;
        Redraw();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyClickThrough();
        Redraw();
    }

    /// <summary>
    /// The whole window, drawn onto transparency. Also what the screenshot harness
    /// asks for: a layered window cannot be read back with PrintWindow.
    /// </summary>
    internal Bitmap RenderToBitmap()
    {
        var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        PaintOverlay(g);
        return bmp;
    }

    /// <summary>
    /// Handed to Windows as a bitmap with an alpha channel rather than painted in
    /// answer to WM_PAINT. That is the only way the rounded corners are actually
    /// round: a plain window is a rectangle, and Opacity dims the whole of it evenly
    /// instead of cutting anything away.
    /// </summary>
    private void Redraw()
    {
        if (!IsHandleCreated) return;

        using var bmp = RenderToBitmap();

        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr memory = CreateCompatibleDC(screen);
        IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr previous = SelectObject(memory, hBitmap);

        try
        {
            var size = new Size(bmp.Width, bmp.Height);
            var source = Point.Empty;
            var position = new Point(Left, Top);
            var blend = new BlendFunction
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                // The overall softening that Opacity used to provide.
                SourceConstantAlpha = 230,
                AlphaFormat = AC_SRC_ALPHA,
            };

            UpdateLayeredWindow(Handle, screen, ref position, ref size, memory,
                ref source, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(hBitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private void ApplyClickThrough()
    {
        if (!IsHandleCreated) return;

        int style = GetWindowLong(Handle, GWL_EXSTYLE);
        style = _movable ? style & ~WS_EX_TRANSPARENT : style | WS_EX_TRANSPARENT;
        SetWindowLong(Handle, GWL_EXSTYLE, style);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_movable || e.Button != MouseButtons.Left) return;

        // Hand the drag to the window manager rather than tracking the mouse: it
        // gets snapping and multi-monitor edges right for free.
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        Moved?.Invoke(this, EventArgs.Empty);
    }

    private void PaintOverlay(Graphics g)
    {
        var frame = new RectangleF(0, 0, Width, Height);
        int radius = LogicalToDeviceUnits(13);
        Theme.FillRound(g, frame, radius, Theme.Panel);

        // Dashed while it can be dragged, so the one state where it eats clicks is
        // never a surprise.
        if (_movable)
        {
            using var pen = new Pen(Theme.Accent, LogicalToDeviceUnits(2))
            {
                DashStyle = DashStyle.Dash,
            };
            using var path = Theme.Round(RectangleF.Inflate(frame, -1.5f, -1.5f), radius);
            g.DrawPath(pen, path);
        }
        else
        {
            Theme.DrawRound(g, RectangleF.Inflate(frame, -0.5f, -0.5f), radius,
                Color.FromArgb(30, 255, 255, 255), 1f);
        }

        PaintName(g);
        PaintStrip(g);
    }

    private void PaintName(Graphics g)
    {
        bool known = _index >= 0 && _index < Zuiki.Notches.Length;
        string name = known ? Zuiki.Notches[_index].Name : "--";

        var box = new Rectangle(_pad, 0, _nameWidth, Height);
        TextRenderer.DrawText(g, name, _nameFont, box,
            known ? Theme.PanelInk : Theme.PanelMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (!_powerHeld && !_emergencyHeld) return;

        int glyph = LogicalToDeviceUnits(13);
        var below = new Rectangle(
            box.X + (box.Width - glyph) / 2,
            Height / 2 + Theme.LineHeight(_nameFont) / 2 + LogicalToDeviceUnits(4),
            glyph, glyph);
        PaintPadlock(g, below, Theme.Blend(Theme.Emergency, Theme.Panel, 0.2));
    }

    private void PaintStrip(Graphics g)
    {
        int count = Zuiki.Notches.Length;
        int left = Width - _pad - _stripWidth;
        float radius = LogicalToDeviceUnits(3);

        for (int row = 0; row < count; row++)
        {
            // Brake at the top, the way the lever itself is laid out: pushing it away
            // from you brakes, and away reads as up.
            int i = row;

            var r = new RectangleF(left, _pad + row * (_cellHeight + _cellGap),
                _stripWidth, _cellHeight);
            var colour = NotchColour(i);
            bool live = i == _index;
            bool withheld = (_powerHeld && i > Zuiki.NeutralIndex)
                            || (_emergencyHeld && i < Zuiki.FullServiceIndex);

            if (live) PaintLive(g, r, radius, colour);
            else if (!withheld) Theme.FillRound(g, r, radius, Theme.Blend(colour, Theme.Panel, 0.66));
            else
                Theme.DrawRound(g, RectangleF.Inflate(r, -0.5f, -0.5f), radius,
                    Theme.Blend(colour, Theme.Panel, 0.6), 1f);

            // Only the ends are labelled. Fifteen tiny labels would be unreadable at
            // this size and there is nothing to read them for while driving.
            if (i == 0 || i == count - 1 || i == Zuiki.NeutralIndex)
                TextRenderer.DrawText(g, Zuiki.Notches[i].Name, _tickFont,
                    new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height),
                    live ? Theme.PanelInk : Theme.Blend(colour, Color.White, 0.55),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPadding);
        }
    }

    private static Color NotchColour(int index) => Zuiki.Notches[index].Name switch
    {
        "EB" => Theme.Emergency,
        "N" => Theme.Neutral,
        ['B', ..] => Theme.Brake,
        _ => Theme.Power,
    };

    private static void PaintLive(Graphics g, RectangleF r, float radius, Color colour)
    {
        for (int ring = 2; ring >= 1; ring--)
            Theme.FillRound(g, RectangleF.Inflate(r, ring * 2f, ring * 2f), radius + ring * 2f,
                Color.FromArgb(26, colour));

        using var path = Theme.Round(r, radius);
        using var brush = new LinearGradientBrush(
            new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
            Theme.Blend(colour, Color.White, 0.34), colour, LinearGradientMode.Vertical);
        g.FillPath(brush, path);
    }

    private static void PaintPadlock(Graphics g, Rectangle r, Color colour)
    {
        float thickness = Math.Max(1.2f, r.Height / 9f);
        float bodyTop = r.Y + r.Height * 0.46f;
        var body = new RectangleF(r.X + r.Width * 0.12f, bodyTop, r.Width * 0.76f, r.Bottom - bodyTop);

        using var pen = new Pen(colour, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(pen, new RectangleF(r.X + r.Width * 0.27f, r.Y + thickness / 2f,
            r.Width * 0.46f, r.Height * 0.52f), 180f, 180f);

        Theme.FillRound(g, body, Math.Max(1f, r.Width * 0.14f), colour);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
            _tickFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
