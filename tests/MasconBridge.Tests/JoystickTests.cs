using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// The pure parts of the winmm layer. Enumerate and TryRead need real hardware and
/// are left out.
/// </summary>
public class JoystickTests
{
    private static Joystick.JoyInfoEx Sample() => new()
    {
        dwXpos = 1, dwYpos = 2, dwZpos = 3, dwRpos = 4, dwUpos = 5, dwVpos = 6,
        dwPOV = -1,
    };

    // --- Axes ----------------------------------------------------------------

    [Fact]
    public void Six_axes_are_exposed()
    {
        Assert.Equal(new[] { "X", "Y", "Z", "R", "U", "V" }, Joystick.AxisNames);
    }

    [Theory]
    [InlineData("X", 1)]
    [InlineData("Y", 2)]
    [InlineData("Z", 3)]
    [InlineData("R", 4)]
    [InlineData("U", 5)]
    [InlineData("V", 6)]
    public void Each_axis_name_reads_its_own_field(string name, int expected)
    {
        Assert.Equal(expected, Joystick.GetAxis(Sample(), name));
    }

    [Theory]
    [InlineData("z")]
    [InlineData("Z")]
    public void Axis_names_are_matched_regardless_of_case(string name)
    {
        Assert.Equal(3, Joystick.GetAxis(Sample(), name));
    }

    [Theory]
    [InlineData("W")]
    [InlineData("")]
    [InlineData("throttle")]
    public void An_unknown_axis_reports_minus_one(string name)
    {
        Assert.Equal(-1, Joystick.GetAxis(Sample(), name));
    }

    // --- Hat -----------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]          // up
    [InlineData(4500, 1)]       // up-right
    [InlineData(9000, 2)]       // right
    [InlineData(13500, 3)]
    [InlineData(18000, 4)]      // down
    [InlineData(22500, 5)]
    [InlineData(27000, 6)]      // left
    [InlineData(31500, 7)]
    public void Pov_angles_map_to_the_eight_hat_directions(int pov, int expected)
    {
        Assert.Equal((byte)expected, Joystick.PovToHat(pov));
    }

    [Theory]
    [InlineData(-1)]            // winmm reports -1 when the hat is centred
    [InlineData(65535)]         // and 65535 on some drivers
    [InlineData(36000)]
    [InlineData(99999)]
    public void An_idle_or_out_of_range_pov_is_centred(int pov)
    {
        Assert.Equal(Zuiki.HatCentered, Joystick.PovToHat(pov));
    }

    [Fact]
    public void A_pov_just_short_of_a_full_turn_wraps_back_to_up()
    {
        Assert.Equal(0, Joystick.PovToHat(35999));
    }

    [Fact]
    public void Every_hat_direction_fits_the_low_nibble()
    {
        for (int pov = 0; pov < 36000; pov += 100)
            Assert.InRange(Joystick.PovToHat(pov), 0, 0x0F);
    }

    // --- Buttons -------------------------------------------------------------

    [Fact]
    public void Button_numbers_start_at_one()
    {
        var j = new Joystick.JoyInfoEx { dwButtons = 0b0001 };

        Assert.True(Joystick.IsButtonDown(j, 1));
        Assert.False(Joystick.IsButtonDown(j, 2));
    }

    [Fact]
    public void Several_buttons_can_be_down_at_once()
    {
        var j = new Joystick.JoyInfoEx { dwButtons = 0b1000_0101 };

        Assert.Equal(new[] { 1, 3, 8 }, Joystick.PressedButtons(j));
        Assert.True(Joystick.IsButtonDown(j, 3));
        Assert.False(Joystick.IsButtonDown(j, 4));
    }

    [Fact]
    public void No_buttons_down_reports_an_empty_list()
    {
        Assert.Empty(Joystick.PressedButtons(new Joystick.JoyInfoEx { dwButtons = 0 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(33)]
    public void Button_numbers_outside_the_supported_range_are_not_down(int button)
    {
        var j = new Joystick.JoyInfoEx { dwButtons = unchecked((int)0xFFFFFFFF) };
        Assert.False(Joystick.IsButtonDown(j, button));
    }

    [Fact]
    public void All_thirty_two_buttons_can_be_reported()
    {
        var j = new Joystick.JoyInfoEx { dwButtons = unchecked((int)0xFFFFFFFF) };

        Assert.Equal(32, Joystick.PressedButtons(j).Count);
        Assert.True(Joystick.IsButtonDown(j, 32));
    }
}
