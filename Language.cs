using System.Globalization;

namespace MasconBridge;

/// <summary>
/// The languages the program ships with. Kept in step with
/// SatelliteResourceLanguages in the csproj: adding one here without adding it
/// there gives a language whose resources are never packaged.
/// </summary>
public static class Language
{
    /// <summary>The game is Japanese, so that is what the program speaks by default.</summary>
    public const string Default = "ja";

    /// <summary>
    /// Display names stay in their own language, never translated: someone who
    /// opened the program in a language they cannot read still has to find theirs.
    /// </summary>
    public static readonly (string Code, string Display)[] Supported =
    {
        ("ja", "日本語"),
        ("en", "English"),
    };

    public static bool IsSupported(string? code) =>
        code is not null && Supported.Any(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Falls back to the default rather than to whatever Windows is set to.</summary>
    public static string Normalise(string? code) =>
        IsSupported(code)
            ? Supported.First(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).Code
            : Default;

    public static string DisplayName(string? code)
    {
        string normalised = Normalise(code);
        return Supported.First(l => l.Code == normalised).Display;
    }

    /// <summary>Applies the language to this thread and to every thread started later.</summary>
    public static void Apply(string? code)
    {
        var culture = new CultureInfo(Normalise(code));
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
