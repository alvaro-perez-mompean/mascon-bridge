using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// The two catches on the handle, which are the only part of it with any memory.
/// Both reproduce a mechanism on the real lever: a thumb button between N and P1,
/// and a cam between B8 and EB. Guarding the crossing is the whole of it — staying
/// across is not guarded, because on the real handle the lever is simply there.
/// </summary>
public class NotchCatchTests
{
    private static int N => Zuiki.NeutralIndex;
    private static int B8 => Zuiki.FullServiceIndex;
    private static int Notch(string name) => Array.FindIndex(Zuiki.Notches, n => n.Name == name);

    // --- the power catch ------------------------------------------------------

    [Theory]
    [InlineData("EB")]
    [InlineData("B8")]
    [InlineData("B1")]
    [InlineData("N")]
    public void Power_lets_neutral_and_the_whole_brake_side_through(string notch)
    {
        var c = NotchCatch.Power();
        int i = Notch(notch);

        Assert.Equal(i, c.Apply(i, releasePressed: false));
        Assert.False(c.Held);
    }

    [Fact]
    public void Power_is_held_at_neutral_without_the_button()
    {
        var c = NotchCatch.Power();

        Assert.Equal(N, c.Apply(Notch("P1"), releasePressed: false));
        Assert.True(c.Held);
    }

    [Fact]
    public void Power_is_allowed_while_the_button_is_pressed()
    {
        var c = NotchCatch.Power();

        Assert.Equal(Notch("P2"), c.Apply(Notch("P2"), releasePressed: true));
        Assert.False(c.Held);
    }

    [Fact]
    public void Braking_is_never_held_back_even_after_the_power_catch_engaged()
    {
        var c = NotchCatch.Power();
        c.Apply(Notch("P3"), false);

        Assert.Equal(Notch("B5"), c.Apply(Notch("B5"), releasePressed: false));
        Assert.False(c.Held);
    }

    // --- the emergency catch --------------------------------------------------

    [Theory]
    [InlineData("B8")]
    [InlineData("B1")]
    [InlineData("N")]
    [InlineData("P5")]
    public void Emergency_lets_everything_from_full_service_upward_through(string notch)
    {
        var c = NotchCatch.Emergency();
        int i = Notch(notch);

        Assert.Equal(i, c.Apply(i, releasePressed: false));
        Assert.False(c.Held);
    }

    [Fact]
    public void Emergency_is_held_at_full_service_without_the_button()
    {
        // The end of the travel gives B8 until the catch is released.
        var c = NotchCatch.Emergency();

        Assert.Equal(B8, c.Apply(Notch("EB"), releasePressed: false));
        Assert.True(c.Held);
        Assert.Equal("B8", Zuiki.Notches[B8].Name);
    }

    [Fact]
    public void Emergency_is_allowed_while_the_button_is_pressed()
    {
        var c = NotchCatch.Emergency();

        Assert.Equal(Notch("EB"), c.Apply(Notch("EB"), releasePressed: true));
        Assert.False(c.Held);
    }

    [Fact]
    public void Power_is_never_held_back_by_the_emergency_catch()
    {
        var c = NotchCatch.Emergency();
        c.Apply(Notch("EB"), false);

        Assert.Equal(Notch("P5"), c.Apply(Notch("P5"), releasePressed: false));
        Assert.False(c.Held);
    }

    // --- shared behaviour -----------------------------------------------------

    public static TheoryData<string, string, string> Both => new()
    {
        // catch, the notch it guards, where the handle is held instead
        { "power", "P1", "N" },
        { "emergency", "EB", "B8" },
    };

    private static NotchCatch Make(string which) =>
        which == "power" ? NotchCatch.Power() : NotchCatch.Emergency();

    [Theory]
    [MemberData(nameof(Both))]
    public void The_button_can_be_let_go_once_across(string which, string guarded, string _)
    {
        var c = Make(which);
        c.Apply(Notch(guarded), releasePressed: true);

        // A mechanical stop, not a dead man's handle.
        Assert.Equal(Notch(guarded), c.Apply(Notch(guarded), releasePressed: false));
        Assert.False(c.Held);
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void Coming_back_over_the_boundary_sets_the_catch_again(
        string which, string guarded, string boundary)
    {
        var c = Make(which);
        c.Apply(Notch(guarded), releasePressed: true);
        c.Apply(Notch(boundary), releasePressed: false);

        Assert.Equal(Notch(boundary), c.Apply(Notch(guarded), releasePressed: false));
        Assert.True(c.Held);
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void Releasing_lands_on_the_notch_the_lever_is_already_at(
        string which, string guarded, string boundary)
    {
        var c = Make(which);

        Assert.Equal(Notch(boundary), c.Apply(Notch(guarded), false));
        Assert.Equal(Notch(guarded), c.Apply(Notch(guarded), releasePressed: true));
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void A_lever_already_past_the_catch_when_the_bridge_starts_is_held(
        string which, string guarded, string boundary)
    {
        // Nobody crossed the catch, so neither P5 nor EB may be handed to the game
        // the instant the bridge connects.
        var c = Make(which);

        Assert.Equal(Notch(boundary), c.Apply(Notch(guarded), releasePressed: false));
        Assert.True(c.Held);
    }

    [Theory]
    [MemberData(nameof(Both))]
    public void Every_notch_is_reachable_while_the_button_is_pressed(
        string which, string _, string __)
    {
        var c = Make(which);

        for (int i = 0; i < Zuiki.Notches.Length; i++)
            Assert.Equal(i, c.Apply(i, releasePressed: true));
    }

    // --- the boundaries -------------------------------------------------------

    [Fact]
    public void Only_the_power_notches_are_withheld_by_the_power_catch()
    {
        for (int i = 0; i < Zuiki.Notches.Length; i++)
        {
            var c = NotchCatch.Power();
            Assert.Equal(i <= N ? i : N, c.Apply(i, releasePressed: false));
        }
    }

    [Fact]
    public void Only_emergency_is_withheld_by_the_emergency_catch()
    {
        for (int i = 0; i < Zuiki.Notches.Length; i++)
        {
            var c = NotchCatch.Emergency();
            Assert.Equal(i >= B8 ? i : B8, c.Apply(i, releasePressed: false));
        }
    }

    [Fact]
    public void The_two_catches_together_leave_the_handle_between_B8_and_N()
    {
        // Both engaged: the lever can still ask for anything from full service to
        // neutral, which is every notch that needs no permission.
        for (int i = 0; i < Zuiki.Notches.Length; i++)
        {
            var power = NotchCatch.Power();
            var emergency = NotchCatch.Emergency();

            int allowed = power.Apply(emergency.Apply(i, false), false);
            Assert.InRange(allowed, B8, N);
        }
    }

    [Fact]
    public void The_boundaries_are_where_the_notch_table_says_they_are()
    {
        // The catches compare indices, so they would fail quietly if either moved.
        Assert.Equal("N", Zuiki.Notches[Zuiki.NeutralIndex].Name);
        Assert.Equal("B8", Zuiki.Notches[Zuiki.FullServiceIndex].Name);
        Assert.Equal("EB", Zuiki.Notches[0].Name);
        Assert.Equal(9, Zuiki.NeutralIndex);
        Assert.Equal(1, Zuiki.FullServiceIndex);
    }
}
