using MasconBridge;

namespace MasconBridge.Tests;

public class BindingsTests
{
    private static List<ButtonBinding> Some() => new()
    {
        new ButtonBinding { DeviceId = 3, Button = 1, Mascon = "A" },
        new ButtonBinding { DeviceId = 3, Button = 2, Mascon = "B" },
        new ButtonBinding { DeviceId = 2, Button = 7, Mascon = "A" },
    };

    [Fact]
    public void The_order_covers_every_button_the_device_has_plus_the_emergency_brake()
    {
        // The window offers exactly what the bridge can send: the twelve real
        // buttons and EB, which is a notch reachable as one.
        Assert.Equal(
            Zuiki.ButtonBits.Keys.OrderBy(k => k, StringComparer.Ordinal),
            Bindings.Order.Where(o => o != Bindings.Emergency)
                          .OrderBy(k => k, StringComparer.Ordinal));

        Assert.Contains(Bindings.Emergency, Bindings.Order);
        Assert.Equal(Bindings.Order.Length, Bindings.Order.Distinct().Count());
    }

    [Fact]
    public void Several_physical_buttons_can_reach_the_same_mascon_button()
    {
        var all = Some();

        Assert.Equal(2, Bindings.For(all, "A").Count);
        Assert.Single(Bindings.For(all, "B"));
        Assert.Empty(Bindings.For(all, "ZL"));
    }

    [Fact]
    public void Adding_keeps_what_was_already_there()
    {
        var all = Some();

        Assert.True(Bindings.Add(all, "A", 4, 9));
        Assert.Equal(3, Bindings.For(all, "A").Count);
        Assert.Equal(4, all.Count(b => b.Mascon == "A" || b.Mascon == "B"));
    }

    [Fact]
    public void Pressing_the_same_button_twice_changes_nothing()
    {
        var all = Some();

        Assert.False(Bindings.Add(all, "A", 3, 1));
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void A_nonsense_binding_is_refused_rather_than_stored()
    {
        var all = Some();

        Assert.False(Bindings.Add(all, "A", -1, 3));
        Assert.False(Bindings.Add(all, "A", 2, 0));
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void Clearing_drops_every_button_bound_to_that_one_and_no_other()
    {
        var all = Some();

        Assert.Equal(2, Bindings.Clear(all, "A"));
        Assert.Empty(Bindings.For(all, "A"));
        Assert.Single(Bindings.For(all, "B"));
    }

    [Fact]
    public void Case_does_not_decide_which_mascon_button_is_meant()
    {
        var all = Some();

        Assert.Equal(2, Bindings.For(all, "a").Count);
        Assert.False(Bindings.Add(all, "a", 3, 1));
        Assert.Equal(2, Bindings.Clear(all, "a"));
    }

    [Fact]
    public void A_binding_the_window_does_not_know_is_left_alone()
    {
        // A configuration edited by hand may name something this build has never
        // heard of. Showing less is better than deleting somebody's work.
        var all = Some();
        all.Add(new ButtonBinding { DeviceId = 3, Button = 8, Mascon = "Turbo" });

        var unknown = Assert.Single(Bindings.Unknown(all));
        Assert.Equal("Turbo", unknown.Mascon);
        Assert.Empty(Bindings.Unknown(Some()));
    }

    [Fact]
    public void Emergency_is_bound_like_any_other_button()
    {
        var all = new List<ButtonBinding>();

        Assert.True(Bindings.Add(all, Bindings.Emergency, 3, 6));
        var eb = Assert.Single(Bindings.For(all, "EB"));
        Assert.Equal(3, eb.DeviceId);
        Assert.Equal(6, eb.Button);
    }
}
