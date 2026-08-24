using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace MasconBridge;

/// <summary>
/// The palette, the type scale and the drawing helpers the hand painted parts of
/// the window share.
///
/// The colours are the ones in assets/icon.svg. A mascon is recognised by its notch
/// scale, so red for the brake side, green for neutral and blue for power are the
/// only saturated colours in the window; everything else stays neutral so the scale
/// is the thing the eye lands on.
/// </summary>
internal static class Theme
{
    public static readonly Color Page = Color.FromArgb(0xF1, 0xF3, 0xF6);
    public static readonly Color Card = Color.FromArgb(0xFF, 0xFF, 0xFF);
    public static readonly Color CardEdge = Color.FromArgb(0xE1, 0xE4, 0xEB);
    public static readonly Color Ink = Color.FromArgb(0x1B, 0x1D, 0x22);
    public static readonly Color Muted = Color.FromArgb(0x6A, 0x72, 0x80);
    public static readonly Color Track = Color.FromArgb(0xE6, 0xE9, 0xEF);
    public static readonly Color TrackFill = Color.FromArgb(0xA6, 0xAE, 0xBB);

    public static readonly Color Accent = Color.FromArgb(0x3D, 0x82, 0xD8);
    public static readonly Color AccentDeep = Color.FromArgb(0x2F, 0x6B, 0xB8);
    public static readonly Color AccentWash = Color.FromArgb(0xEC, 0xF3, 0xFC);

    public static readonly Color Brake = Color.FromArgb(0xE2, 0x45, 0x3F);
    public static readonly Color BrakeDeep = Color.FromArgb(0xC2, 0x35, 0x30);
    public static readonly Color Emergency = Color.FromArgb(0xFF, 0x4B, 0x38);
    public static readonly Color Neutral = Color.FromArgb(0x35, 0xC2, 0x6A);
    public static readonly Color Power = Color.FromArgb(0x3D, 0x82, 0xD8);

    /// <summary>The instrument panel: the dark surround of the notch display.</summary>
    public static readonly Color Panel = Color.FromArgb(0x1E, 0x20, 0x26);
    public static readonly Color PanelWell = Color.FromArgb(0x0D, 0x0D, 0x10);
    public static readonly Color PanelInk = Color.FromArgb(0xF3, 0xF5, 0xF8);
    public static readonly Color PanelMuted = Color.FromArgb(0x8C, 0x96, 0xA4);

    public static readonly Color Disabled = Color.FromArgb(0xEC, 0xEE, 0xF2);
    public static readonly Color DisabledInk = Color.FromArgb(0xA4, 0xAB, 0xB6);

    // ------------------------------------------------------------------ fonts
    private static readonly Dictionary<(string, float, FontStyle), Font> Cached = new();

    private static readonly HashSet<string> Installed =
        new(FontFamily.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Segoe UI carries no kana, and the face Windows links in to cover them does not
    /// match the Latin around it. The Japanese interface therefore asks for a family
    /// that has both.
    /// </summary>
    private static string UiFamily => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja"
        ? Pick("Yu Gothic UI", "Meiryo UI", "Segoe UI")
        : Pick("Segoe UI", "Yu Gothic UI");

    private static string MonoFamily => Pick("Cascadia Mono", "Consolas", "Courier New");

    private static string Pick(params string[] names) =>
        Array.Find(names, Installed.Contains) ?? FontFamily.GenericSansSerif.Name;

    public static Font Ui(float size, FontStyle style = FontStyle.Regular)
        => Get(UiFamily, size, style);

    /// <summary>Readings and hex values, where digits lining up matters more than the face.</summary>
    public static Font Mono(float size, FontStyle style = FontStyle.Regular)
        => Get(MonoFamily, size, style);

    private static Font Get(string family, float size, FontStyle style)
    {
        // Held for the life of the process: a handful of fonts, shared by every
        // control, and none of them owns the instance it was handed.
        lock (Cached)
        {
            if (!Cached.TryGetValue((family, size, style), out var f))
                Cached[(family, size, style)] = f = new Font(family, size, style);
            return f;
        }
    }

    /// <summary>
    /// Measured through GDI rather than Font.Height, which is the only way to get the
    /// height the text will actually be drawn at on a scaled display.
    /// </summary>
    public static Size Measure(string text, Font font) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding);

    public static int LineHeight(Font font) => Measure("Ag", font).Height;

    // --------------------------------------------------------------- painting
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f) * 2f;
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRound(Graphics g, RectangleF r, float radius, Color colour)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using var path = Round(r, radius);
        using var brush = new SolidBrush(colour);
        g.FillPath(brush, path);
    }

    public static void DrawRound(Graphics g, RectangleF r, float radius, Color colour, float width)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using var path = Round(r, radius);
        using var pen = new Pen(colour, width);
        g.DrawPath(pen, path);
    }

    /// <summary>Mixes towards <paramref name="over"/>: 0 keeps the colour, 1 loses it.</summary>
    public static Color Blend(Color colour, Color over, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(colour.R + (over.R - colour.R) * amount),
            (int)Math.Round(colour.G + (over.G - colour.G) * amount),
            (int)Math.Round(colour.B + (over.B - colour.B) * amount));
    }
}
