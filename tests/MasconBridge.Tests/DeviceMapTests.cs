using MasconBridge;

namespace MasconBridge.Tests;

public class DeviceMapTests
{
    private const string Twcs = "044F:B687";
    private const string Stick = "044F:B10A";
    private const string Pedals = "06A3:0763";

    private const string Mascon = "33DD:0002";

    private static Dictionary<int, string> Map(params (int Id, string Identity)[] entries)
        => entries.ToDictionary(e => e.Id, e => e.Identity);

    /// <summary>The shape Joystick.Enumerate hands back, without needing hardware.</summary>
    private static (int Id, Joystick.JoyCaps Caps) Device(int id, ushort vid, ushort pid)
        => (id, new Joystick.JoyCaps { wMid = vid, wPid = pid });

    private static (int Id, Joystick.JoyCaps Caps)[] Attached() =>
        new[]
        {
            Device(2, 0x044F, 0xB687),  // TWCS throttle
            Device(3, 0x044F, 0xB10A),  // T.16000M stick
            Device(4, 0x33DD, 0x0002),  // the mascon the bridge created
        };

    [Fact]
    public void Nothing_moves_when_the_numbers_still_hold_the_same_devices()
    {
        var plan = DeviceMap.Match(
            Map((2, Twcs), (3, Stick)),
            Map((2, Twcs), (3, Stick)));

        Assert.Empty(plan.Moves);
        Assert.Empty(plan.Missing);
        Assert.False(plan.Anything);
        Assert.Equal(2, plan.Apply(2));
    }

    [Fact]
    public void A_device_that_shifted_number_is_followed()
    {
        var plan = DeviceMap.Match(
            Map((2, Twcs), (3, Stick)),
            Map((1, Twcs), (2, Stick)));

        Assert.Equal(1, plan.Apply(2));
        Assert.Equal(2, plan.Apply(3));
        Assert.Empty(plan.Missing);
    }

    [Fact]
    public void A_device_still_at_its_own_number_keeps_it()
    {
        // The stick did not move; only the throttle did. Claiming its own number
        // first is what stops the throttle from being handed number 3.
        var plan = DeviceMap.Match(
            Map((2, Twcs), (3, Stick)),
            Map((3, Stick), (4, Twcs)));

        Assert.Equal(3, plan.Apply(3));
        Assert.Equal(4, plan.Apply(2));
    }

    [Fact]
    public void An_unplugged_device_is_reported_and_its_number_left_alone()
    {
        var plan = DeviceMap.Match(
            Map((2, Twcs), (3, Stick)),
            Map((2, Twcs)));

        Assert.Empty(plan.Moves);
        var gone = Assert.Single(plan.Missing);
        Assert.Equal(3, gone.Id);
        Assert.Equal(Stick, gone.Identity);
        Assert.Equal(3, plan.Apply(3));
    }

    [Fact]
    public void Two_identical_devices_are_not_given_the_same_number()
    {
        var plan = DeviceMap.Match(
            Map((0, Pedals), (1, Pedals)),
            Map((5, Pedals), (6, Pedals)));

        Assert.Equal(5, plan.Apply(0));
        Assert.Equal(6, plan.Apply(1));
    }

    [Fact]
    public void One_of_two_identical_devices_unplugged_leaves_exactly_one_missing()
    {
        var plan = DeviceMap.Match(
            Map((0, Pedals), (1, Pedals)),
            Map((0, Pedals)));

        Assert.Single(plan.Missing);
        Assert.Equal(0, plan.Apply(0));
    }

    [Fact]
    public void A_configuration_that_never_recorded_anything_is_left_untouched()
    {
        var plan = DeviceMap.Match(Map(), Map((0, Twcs)));

        Assert.False(plan.Anything);
        Assert.Equal(7, plan.Apply(7));
    }

    [Fact]
    public void Identity_is_the_four_digit_hex_pair()
        => Assert.Equal("044F:B687", DeviceMap.Identity(0x044F, 0xB687));

    // --- through the configuration ------------------------------------------
    [Fact]
    public void Relocating_rewrites_every_number_that_names_a_device()
    {
        var cfg = new Config
        {
            AxisDeviceId = 2,
            HatDeviceId = 3,
            PowerReleaseDeviceId = 3,
            EmergencyReleaseDeviceId = 3,
            Buttons = { new ButtonBinding { DeviceId = 3, Button = 1, Mascon = "A" } },
            Devices = Map((2, Twcs), (3, Stick)),
        };

        var plan = cfg.RelocateDevices(Map((0, Stick), (1, Twcs)));

        Assert.Equal(1, cfg.AxisDeviceId);
        Assert.Equal(0, cfg.HatDeviceId);
        Assert.Equal(0, cfg.PowerReleaseDeviceId);
        Assert.Equal(0, cfg.EmergencyReleaseDeviceId);
        Assert.Equal(0, cfg.Buttons[0].DeviceId);
        Assert.Equal(2, plan.Moves.Count);

        // The memory moves with them, or the next run would undo this one.
        Assert.Equal(Twcs, cfg.Devices[1]);
        Assert.Equal(Stick, cfg.Devices[0]);
    }

    [Fact]
    public void Relocating_leaves_disabled_slots_disabled()
    {
        var cfg = new Config
        {
            AxisDeviceId = 2,
            HatDeviceId = -1,
            PowerReleaseDeviceId = -1,
            Devices = Map((2, Twcs)),
        };

        cfg.RelocateDevices(Map((5, Twcs)));

        Assert.Equal(5, cfg.AxisDeviceId);
        Assert.Equal(-1, cfg.HatDeviceId);
        Assert.Equal(-1, cfg.PowerReleaseDeviceId);
    }

    [Fact]
    public void Remembering_records_only_the_devices_the_configuration_uses()
    {
        var cfg = new Config
        {
            AxisDeviceId = 2,
            HatDeviceId = -1,
            PowerReleaseDeviceId = -1,
            EmergencyReleaseDeviceId = -1,
            Buttons = { new ButtonBinding { DeviceId = 3, Button = 1, Mascon = "A" } },
        };

        cfg.RememberDevices(Map((2, Twcs), (3, Stick), (4, Pedals)));

        Assert.Equal(Twcs, cfg.Devices[2]);
        Assert.Equal(Stick, cfg.Devices[3]);
        Assert.False(cfg.Devices.ContainsKey(4));
    }

    [Fact]
    public void Referenced_devices_are_listed_once_and_never_the_disabled_ones()
    {
        var cfg = new Config
        {
            AxisDeviceId = 2,
            HatDeviceId = 2,
            PowerReleaseDeviceId = -1,
            EmergencyReleaseDeviceId = 3,
            Buttons =
            {
                new ButtonBinding { DeviceId = 3, Button = 1, Mascon = "A" },
                new ButtonBinding { DeviceId = 2, Button = 2, Mascon = "B" },
            },
        };

        Assert.Equal(new[] { 2, 3 }, cfg.ReferencedDevices().OrderBy(i => i).ToArray());
    }

    // --- Keeping the bridge out of its own scan ------------------------------

    [Fact]
    public void With_the_bridge_stopped_every_joystick_is_listened_to()
    {
        // Nothing is excluded, so somebody with a real ZUIKI mascon can bind it.
        Assert.Equal(new[] { 2, 3, 4 },
            DeviceMap.Ignoring(Attached(), null).Select(d => d.Id));

        Assert.Equal(new[] { 2, 3, 4 },
            DeviceMap.Ignoring(Attached(), "").Select(d => d.Id));
    }

    [Fact]
    public void The_device_the_bridge_created_is_left_out()
    {
        Assert.Equal(new[] { 2, 3 },
            DeviceMap.Ignoring(Attached(), Mascon).Select(d => d.Id));
    }

    [Fact]
    public void The_hardware_is_still_listened_to()
    {
        // The point of the exclusion is that pressing a mapped button still reaches
        // the stick it is on, rather than the bridge echoing it back.
        var left = DeviceMap.Ignoring(Attached(), Mascon).ToList();

        Assert.Contains(left, d => DeviceMap.Identity(d.Caps.wMid, d.Caps.wPid) == Stick);
        Assert.Contains(left, d => DeviceMap.Identity(d.Caps.wMid, d.Caps.wPid) == Twcs);
    }

    [Fact]
    public void A_real_mascon_beside_the_bridge_is_hidden_too()
    {
        // The known cost of matching on the ids, and the reason the caller passes
        // null while the bridge is stopped: the two are indistinguishable, which is
        // exactly what makes the bridge work at all.
        var withReal = Attached().Append(Device(5, 0x33DD, 0x0002)).ToArray();

        Assert.Equal(new[] { 2, 3 },
            DeviceMap.Ignoring(withReal, Mascon).Select(d => d.Id));
    }

    [Fact]
    public void An_identity_written_in_either_case_still_matches()
    {
        Assert.Equal(new[] { 2, 3 },
            DeviceMap.Ignoring(Attached(), "33dd:0002").Select(d => d.Id));
    }

    [Fact]
    public void A_model_that_is_not_attached_hides_nothing()
    {
        Assert.Equal(new[] { 2, 3, 4 },
            DeviceMap.Ignoring(Attached(), "0F0D:00C1").Select(d => d.Id));
    }

    [Fact]
    public void What_the_bridge_reports_is_what_the_scan_matches_on()
    {
        // The two sides have to agree: BridgeRunner writes the identity with
        // DeviceMap.Identity, and Present reads the attached ones the same way.
        var (vid, pid, _) = new Config { Model = "ZKNS-002" }.ResolveDevice();

        Assert.Equal(Mascon, DeviceMap.Identity(vid, pid));
    }
}
