using System.Drawing;

namespace MasconBridge;

/// <summary>
/// Where the overlay goes. Separate from the window that draws it because a stored
/// position outlives the monitor it was stored on: put back on a machine with one
/// screen fewer, it would be invisible with no way to drag it back.
/// </summary>
public static class OverlayPlacement
{
    /// <summary>The stored position, or a usable one if it is off every screen.</summary>
    public static Point Clamp(Point wanted, Size size, IReadOnlyList<Rectangle> screens)
    {
        if (screens.Count == 0) return wanted;

        var window = new Rectangle(wanted, size);
        foreach (var screen in screens)
            if (screen.IntersectsWith(window))
                return wanted;

        return Default(size, screens[0]);
    }

    /// <summary>
    /// Against the right hand edge, vertically centred: out of the way of the track
    /// ahead and of the instruments, which sit low and centre in the cab view.
    /// </summary>
    public static Point Default(Size size, Rectangle screen) => new(
        screen.Right - size.Width - screen.Width / 24,
        screen.Top + (screen.Height - size.Height) / 2);
}
