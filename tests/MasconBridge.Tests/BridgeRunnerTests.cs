using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// The axis maths: normalising the raw reading and splitting the travel into notches.
/// This is what makes the bridge work with any lever, so it carries the weight.
/// </summary>
public class BridgeRunnerTests
{
    private static Config Cfg(int min = 0, int max = 65535, bool invert = false) => new()
    {
        AxisMin = min,
        AxisMax = max,
        Invert = invert,
    };

    // --- AxisFraction --------------------------------------------------------

    [Fact]
    public void Fraction_maps_the_calibrated_range_onto_zero_to_one()
    {
        var cfg = Cfg();
        Assert.Equal(0.0, BridgeRunner.AxisFraction(0, cfg), 6);
        Assert.Equal(1.0, BridgeRunner.AxisFraction(65535, cfg), 6);
        Assert.Equal(0.5, BridgeRunner.AxisFraction(32767, cfg), 3);
    }

    [Fact]
    public void Fraction_honours_a_trimmed_calibration()
    {
        var cfg = Cfg(min: 1000, max: 5000);
        Assert.Equal(0.0, BridgeRunner.AxisFraction(1000, cfg), 6);
        Assert.Equal(1.0, BridgeRunner.AxisFraction(5000, cfg), 6);
        Assert.Equal(0.5, BridgeRunner.AxisFraction(3000, cfg), 6);
    }

    [Fact]
    public void Fraction_clamps_outside_the_calibrated_range()
    {
        var cfg = Cfg(min: 1000, max: 5000);
        Assert.Equal(0.0, BridgeRunner.AxisFraction(0, cfg), 6);
        Assert.Equal(0.0, BridgeRunner.AxisFraction(-500, cfg), 6);
        Assert.Equal(1.0, BridgeRunner.AxisFraction(99999, cfg), 6);
    }

    [Fact]
    public void Fraction_inverts_when_asked()
    {
        var cfg = Cfg(invert: true);
        Assert.Equal(1.0, BridgeRunner.AxisFraction(0, cfg), 6);
        Assert.Equal(0.0, BridgeRunner.AxisFraction(65535, cfg), 6);
        Assert.Equal(0.5, BridgeRunner.AxisFraction(32767, cfg), 3);
    }

    [Fact]
    public void Fraction_survives_a_degenerate_calibration()
    {
        // A failed calibration must not divide by zero.
        var cfg = Cfg(min: 500, max: 500);
        var p = BridgeRunner.AxisFraction(500, cfg);
        Assert.InRange(p, 0.0, 1.0);
    }

    // --- ZoneWithHysteresis --------------------------------------------------

    [Fact]
    public void Both_ends_of_the_travel_reach_the_end_zones()
    {
        Assert.Equal(0, BridgeRunner.ZoneWithHysteresis(0.0, 15, 7, 0));
        Assert.Equal(14, BridgeRunner.ZoneWithHysteresis(1.0, 15, 7, 0));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.1, 1)]
    [InlineData(0.5, 7)]
    [InlineData(0.9, 13)]
    [InlineData(1.0, 14)]
    public void Without_hysteresis_the_travel_splits_evenly(double p, int expected)
    {
        Assert.Equal(expected, BridgeRunner.ZoneWithHysteresis(p, 15, expected, 0));
    }

    [Fact]
    public void Zones_are_equal_width()
    {
        // With 15 zones each is 1/15 wide, so the centre of zone n is (n + 0.5)/15.
        for (int n = 0; n < 15; n++)
        {
            double centre = (n + 0.5) / 15.0;
            Assert.Equal(n, BridgeRunner.ZoneWithHysteresis(centre, 15, n, 0));
        }
    }

    [Fact]
    public void Hysteresis_holds_the_current_zone_just_past_the_boundary()
    {
        // Zone 5 of 15 spans [0.3333, 0.4). With 25% hysteresis the handle keeps
        // reporting 5 slightly beyond that, instead of chattering between notches.
        const int zones = 15;
        double justPast = 5.0 / zones + 1.0 / zones + 0.1 / zones;

        Assert.Equal(5, BridgeRunner.ZoneWithHysteresis(justPast, zones, current: 5, hysteresis: 0.25));
        Assert.Equal(6, BridgeRunner.ZoneWithHysteresis(justPast, zones, current: 6, hysteresis: 0.25));
    }

    [Fact]
    public void Hysteresis_gives_way_once_the_handle_moves_far_enough()
    {
        const int zones = 15;
        double wellInsideNextZone = 6.5 / zones;

        Assert.Equal(6, BridgeRunner.ZoneWithHysteresis(wellInsideNextZone, zones, current: 5, hysteresis: 0.25));
    }

    [Fact]
    public void Zero_hysteresis_switches_at_the_exact_boundary()
    {
        const int zones = 15;
        double boundary = 6.0 / zones;

        Assert.Equal(6, BridgeRunner.ZoneWithHysteresis(boundary + 1e-9, zones, current: 5, hysteresis: 0));
    }

    [Fact]
    public void Hysteresis_is_clamped_so_a_zone_can_always_be_left()
    {
        // Anything at or above 0.5 would make neighbouring bands overlap completely
        // and the handle would stick on the first zone it entered.
        const int zones = 15;
        double farAway = 12.5 / zones;

        Assert.Equal(12, BridgeRunner.ZoneWithHysteresis(farAway, zones, current: 0, hysteresis: 5.0));
    }

    [Fact]
    public void Result_always_stays_within_the_zone_count()
    {
        foreach (int zones in new[] { 14, 15 })
            for (double p = -0.5; p <= 1.5; p += 0.01)
            {
                int z = BridgeRunner.ZoneWithHysteresis(p, zones, 0, 0.25);
                Assert.InRange(z, 0, zones - 1);
            }
    }

    [Fact]
    public void Fourteen_zones_skip_the_emergency_notch()
    {
        // With IncludeEmergencyInAxis off the loop adds 1 to the zone index, so the
        // handle covers B8..P5 and never reaches EB.
        const int firstNotch = 1;
        int zones = Zuiki.Notches.Length - firstNotch;

        Assert.Equal(14, zones);
        Assert.Equal("B8", Zuiki.Notches[firstNotch + BridgeRunner.ZoneWithHysteresis(0.0, zones, 0, 0)].Name);
        Assert.Equal("P5", Zuiki.Notches[firstNotch + BridgeRunner.ZoneWithHysteresis(1.0, zones, 0, 0)].Name);
    }

    [Fact]
    public void Fifteen_zones_put_the_emergency_notch_at_the_end_of_travel()
    {
        const int firstNotch = 0;
        int zones = Zuiki.Notches.Length - firstNotch;

        Assert.Equal(15, zones);
        Assert.Equal("EB", Zuiki.Notches[firstNotch + BridgeRunner.ZoneWithHysteresis(0.0, zones, 5, 0)].Name);
        Assert.Equal("P5", Zuiki.Notches[firstNotch + BridgeRunner.ZoneWithHysteresis(1.0, zones, 5, 0)].Name);
    }
}
