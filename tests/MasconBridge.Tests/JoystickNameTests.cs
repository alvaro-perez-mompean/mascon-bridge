using MasconBridge;

namespace MasconBridge.Tests;

public class JoystickNameTests
{
    [Fact]
    public void The_key_is_named_after_the_ids_in_upper_case_hex()
    {
        Assert.Equal("VID_33DD&PID_0002", JoystickName.KeyName(0x33DD, 0x0002));
        Assert.Equal("VID_0F0D&PID_00C1", JoystickName.KeyName(0x0F0D, 0x00C1));
    }

    [Fact]
    public void The_path_is_the_one_the_game_controllers_panel_reads()
    {
        Assert.Equal(
            @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\VID_33DD&PID_0002",
            JoystickName.KeyPath(0x33DD, 0x0002));
    }

    [Fact]
    public void A_cache_that_agrees_is_left_alone()
        => Assert.False(JoystickName.IsStale("mascon-bridge", "mascon-bridge"));

    [Fact]
    public void A_cache_holding_another_name_is_stale()
        => Assert.True(JoystickName.IsStale("One Handle MasCon for Nintendo Switch", "mascon-bridge"));

    [Fact]
    public void Nothing_cached_is_nothing_to_clear()
        => Assert.False(JoystickName.IsStale(null, "mascon-bridge"));

    [Fact]
    public void Case_counts_because_the_panel_shows_it_verbatim()
        => Assert.True(JoystickName.IsStale("Mascon-Bridge", "mascon-bridge"));
}
