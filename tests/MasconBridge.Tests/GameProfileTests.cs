using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// The captions shown beside each mascon button. This is display only, so what
/// matters is that it covers every row, that it never invents a game, and above all
/// that the button the manual says to leave alone is marked as such.
/// </summary>
public class GameProfileTests
{
    [Fact]
    public void No_game_is_the_default()
    {
        Assert.Equal(GameProfile.None, new Config().Game);
        Assert.Equal(GameProfile.None, Config.Default().Game);
        Assert.Equal(GameProfile.None, GameProfile.Supported[0]);
    }

    [Fact]
    public void With_no_game_there_is_nothing_to_say_about_any_row()
    {
        foreach (var mascon in Bindings.Order.Append(Bindings.Hat))
            Assert.Null(GameProfile.FunctionOf(GameProfile.None, mascon));
    }

    [Fact]
    public void An_unknown_game_says_nothing_rather_than_guessing()
    {
        // A configuration edited by hand, or written by a later version, must not
        // put another game's names on the buttons.
        Assert.Null(GameProfile.FunctionOf("some-other-train-game", "Y"));
        Assert.Equal(GameProfile.None, GameProfile.Normalise("some-other-train-game"));
        Assert.Equal(GameProfile.None, GameProfile.Normalise(null));
        Assert.Equal(GameProfile.None, GameProfile.Normalise(""));
    }

    [Fact]
    public void The_game_is_recognised_whatever_the_case()
    {
        Assert.Equal(GameProfile.JrEast, GameProfile.Normalise("JR-EAST"));
        Assert.NotNull(GameProfile.FunctionOf("JR-East", "y"));
    }

    [Fact]
    public void Every_row_on_the_page_has_something_to_say()
    {
        // Every button the game is given, plus the hat. EB is the exception below.
        var covered = Bindings.Order
            .Where(m => m != Bindings.Emergency)
            .Append(Bindings.Hat);

        foreach (var mascon in covered)
            Assert.NotNull(GameProfile.FunctionOf(GameProfile.JrEast, mascon));
    }

    [Fact]
    public void The_emergency_notch_is_not_a_game_function()
    {
        // EB is a position of the handle reached as a button. The game never sees
        // a button for it, so there is no name to put beside it.
        Assert.Null(GameProfile.FunctionOf(GameProfile.JrEast, Bindings.Emergency));
    }

    [Fact]
    public void The_button_the_manual_warns_about_is_marked()
    {
        // The reason this page exists: the manual prints ZL in red and says not to
        // use it, and until now the window offered it like any other.
        var zl = GameProfile.FunctionOf(GameProfile.JrEast, "ZL");

        Assert.NotNull(zl);
        Assert.Equal(GameProfile.Tone.Avoid, zl!.Value.Tone);

        foreach (var mascon in new[] { "Y", "B", "A", "X", "L", "R", "ZR", "Minus", "Plus", "Capture" })
            Assert.Equal(GameProfile.Tone.Normal,
                GameProfile.FunctionOf(GameProfile.JrEast, mascon)!.Value.Tone);
    }

    [Fact]
    public void A_button_the_manual_does_not_cover_says_so()
    {
        var home = GameProfile.FunctionOf(GameProfile.JrEast, "Home");

        Assert.NotNull(home);
        Assert.Equal(GameProfile.Tone.Unknown, home!.Value.Tone);
    }

    [Fact]
    public void Every_caption_has_text()
    {
        foreach (var mascon in Bindings.Order.Append(Bindings.Hat))
        {
            var caption = GameProfile.FunctionOf(GameProfile.JrEast, mascon);
            if (caption is null) continue;

            Assert.False(string.IsNullOrWhiteSpace(caption.Value.Text));
            // A missing resource shows up as the key name.
            Assert.DoesNotContain("Function", caption.Value.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Games_are_named_by_their_own_names()
    {
        Assert.Equal("JR EAST Train Simulator", GameProfile.DisplayName(GameProfile.JrEast));
        Assert.NotEqual(GameProfile.DisplayName(GameProfile.None),
                        GameProfile.DisplayName(GameProfile.JrEast));
    }

    [Fact]
    public void The_choice_survives_a_configuration_round_trip()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            new Config { Game = GameProfile.JrEast }.Save(path);
            Assert.Equal(GameProfile.JrEast, Config.Load(path).Game);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Choosing_a_game_leaves_the_bindings_alone()
    {
        // The mapping from physical buttons to mascon buttons is Bindings' business,
        // and nothing here may reach into it.
        var before = Bindings.Order.ToArray();

        GameProfile.FunctionOf(GameProfile.JrEast, "Y");

        Assert.Equal(before, Bindings.Order);
    }
}
