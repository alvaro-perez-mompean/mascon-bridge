using System.Drawing;
using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// Where the overlay is put. A stored position outlives the monitor it was stored on,
/// so the only real question is what happens when that monitor is gone: a window
/// parked off every screen is invisible with no way to get it back.
/// </summary>
public class OverlayPositionTests
{
    private static readonly Size Strip = new(120, 260);
    private static readonly Rectangle Laptop = new(0, 0, 1920, 1040);
    private static readonly Rectangle Second = new(1920, 0, 2560, 1400);

    [Fact]
    public void A_position_on_a_screen_is_left_alone()
    {
        var wanted = new Point(1700, 400);

        Assert.Equal(wanted, OverlayPlacement.Clamp(wanted, Strip, new[] { Laptop }));
    }

    [Fact]
    public void A_position_on_a_second_screen_is_left_alone()
    {
        var wanted = new Point(3000, 900);

        Assert.Equal(wanted, OverlayPlacement.Clamp(wanted, Strip, new[] { Laptop, Second }));
    }

    [Fact]
    public void Hanging_over_an_edge_still_counts_as_on_screen()
    {
        // Half off the right hand edge is a choice somebody can make; it is only
        // being wholly outside that cannot be undone with the mouse.
        var wanted = new Point(Laptop.Right - 20, 300);

        Assert.Equal(wanted, OverlayPlacement.Clamp(wanted, Strip, new[] { Laptop }));
    }

    [Fact]
    public void A_position_on_a_monitor_that_is_gone_comes_back()
    {
        // Stored while the second screen was plugged in, restored without it.
        var wanted = new Point(3000, 900);

        var actual = OverlayPlacement.Clamp(wanted, Strip, new[] { Laptop });

        Assert.NotEqual(wanted, actual);
        Assert.True(Laptop.IntersectsWith(new Rectangle(actual, Strip)));
    }

    [Fact]
    public void Negative_coordinates_off_every_screen_come_back_too()
    {
        var actual = OverlayPlacement.Clamp(new Point(-4000, -4000), Strip, new[] { Laptop });

        Assert.True(Laptop.IntersectsWith(new Rectangle(actual, Strip)));
    }

    [Fact]
    public void With_no_screens_at_all_the_position_is_returned_untouched()
    {
        // Nothing sensible to move it to, and inventing a position would be worse.
        var wanted = new Point(10, 20);

        Assert.Equal(wanted, OverlayPlacement.Clamp(wanted, Strip, Array.Empty<Rectangle>()));
    }

    [Fact]
    public void The_starting_position_sits_against_the_right_hand_edge()
    {
        var p = OverlayPlacement.Default(Strip, Laptop);
        var window = new Rectangle(p, Strip);

        Assert.True(Laptop.Contains(window), "the default position must be fully on screen");
        Assert.True(window.Right < Laptop.Right, "it must not touch the edge");
        Assert.InRange(window.Right, Laptop.Right - Laptop.Width / 8, Laptop.Right);
    }

    [Fact]
    public void The_starting_position_is_vertically_centred()
    {
        var p = OverlayPlacement.Default(Strip, Laptop);

        int above = p.Y - Laptop.Top;
        int below = Laptop.Bottom - (p.Y + Strip.Height);
        Assert.InRange(Math.Abs(above - below), 0, 1);
    }

    [Fact]
    public void The_starting_position_follows_the_screen_it_is_given()
    {
        var p = OverlayPlacement.Default(Strip, Second);

        Assert.True(Second.Contains(new Rectangle(p, Strip)));
    }
}
