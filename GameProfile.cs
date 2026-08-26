namespace MasconBridge;

/// <summary>
/// What each mascon button does inside a game.
///
/// This is a caption table and nothing else. It is kept apart from
/// <see cref="Bindings"/> on purpose: that one holds the mapping from physical
/// buttons to mascon buttons, and none of it may change because a game was picked
/// from a list. Choosing a game only changes what the bindings page reads like.
///
/// The captions come from the game's own manual, so they are its defaults: the
/// player is free to move them about in Steam's controller settings, and the page
/// says so.
/// </summary>
public static class GameProfile
{
    /// <summary>No game chosen. Every row shows the mascon's own button name alone.</summary>
    public const string None = "none";

    /// <summary>JR EAST Train Simulator, Steam appid 2111630.</summary>
    public const string JrEast = "jr-east";

    /// <summary>How much weight a caption carries, which is how it is coloured.</summary>
    public enum Tone
    {
        /// <summary>A function the game really has.</summary>
        Normal,

        /// <summary>The manual does not say. Shown anyway: a blank row only asks the question again.</summary>
        Unknown,

        /// <summary>The manual says to leave this one alone.</summary>
        Avoid,
    }

    /// <summary>One button's caption, ready to be shown.</summary>
    public readonly record struct Caption(string Text, Tone Tone);

    // Every name below is fetched through a function rather than stored: these
    // tables are built once for the life of the process, and the text comes from the
    // resources, which answer in whatever language is current when they are asked.
    private static readonly (string Id, Func<string> Display)[] Games =
    {
        (None, () => Strings.GameNone),
        (JrEast, () => Strings.GameJrEast),
    };

    private static readonly Dictionary<string, Func<Caption>> JrEastButtons =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Y"] = () => new(Strings.FunctionAtsConfirmation, Tone.Normal),
            ["B"] = () => new(Strings.FunctionHornLevel1, Tone.Normal),
            ["A"] = () => new(Strings.FunctionHornLevel2, Tone.Normal),
            ["X"] = () => new(Strings.FunctionDeadmanReset, Tone.Normal),
            ["L"] = () => new(Strings.FunctionAlarmStop, Tone.Normal),
            ["R"] = () => new(Strings.FunctionSpeedSuppression1, Tone.Normal),
            ["ZL"] = () => new(Strings.FunctionDoNotUse, Tone.Avoid),
            ["ZR"] = () => new(Strings.FunctionCruiseControl, Tone.Normal),
            ["Minus"] = () => new(Strings.FunctionSwitchCabinView, Tone.Normal),
            ["Plus"] = () => new(Strings.FunctionPauseGame, Tone.Normal),
            ["Home"] = () => new(Strings.FunctionNotInManual, Tone.Unknown),
            ["Capture"] = () => new(Strings.FunctionScreenshot, Tone.Normal),

            // The hat carries four functions at once, which is the thing about that
            // row people miss, so all four are named.
            [Bindings.Hat] = () => new(Strings.FunctionHat, Tone.Normal),
        };

    private static readonly Dictionary<string, Dictionary<string, Func<Caption>>> ByGame =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [JrEast] = JrEastButtons,
        };

    /// <summary>The games that can be chosen, "none" first.</summary>
    public static readonly IReadOnlyList<string> Supported =
        Games.Select(g => g.Id).ToArray();

    public static bool IsSupported(string? id) =>
        id is not null && Games.Any(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Anything unrecognised means no game, never a guess at one.</summary>
    public static string Normalise(string? id) =>
        IsSupported(id)
            ? Games.First(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Id
            : None;

    public static string DisplayName(string? id)
    {
        string normalised = Normalise(id);
        return Games.First(g => g.Id == normalised).Display();
    }

    /// <summary>
    /// What <paramref name="mascon"/> does in <paramref name="game"/>, or null if
    /// there is nothing to say: no game chosen, a game with no table, or a button
    /// the game never sees. EB is the last of those — it is the emergency notch
    /// reached as a button, not one of the buttons the game is given.
    /// </summary>
    public static Caption? FunctionOf(string? game, string mascon)
    {
        if (!ByGame.TryGetValue(Normalise(game), out var table)) return null;
        return table.TryGetValue(mascon, out var caption) ? caption() : null;
    }
}
