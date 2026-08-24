using System.Globalization;
using System.Text.RegularExpressions;
using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// Checks the translations are complete and usable. A missing key shows the key
/// name in the window; a placeholder that does not match its English original
/// throws FormatException at the moment the text is shown.
/// </summary>
public class LocalisationTests
{
    private static readonly CultureInfo[] Cultures =
        Language.Supported.Select(l => new CultureInfo(l.Code)).ToArray();

    private static string Read(string key, CultureInfo culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            return typeof(Strings).GetProperty(key)!.GetValue(null) as string ?? "";
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    // --- completeness --------------------------------------------------------

    [Fact]
    public void Every_key_has_text_in_every_language()
    {
        var missing = new List<string>();

        foreach (var culture in Cultures)
            foreach (var key in Strings.Keys)
            {
                string text = Read(key, culture);
                // Falling back to the key name is what a missing resource looks like.
                if (string.IsNullOrWhiteSpace(text) || text == key)
                    missing.Add($"{culture.Name}: {key}");
            }

        Assert.True(missing.Count == 0, "Missing translations:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Every_property_on_Strings_is_listed_in_Keys()
    {
        var properties = typeof(Strings)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n);

        Assert.Equal(properties, Strings.Keys.OrderBy(k => k));
    }

    // --- placeholders --------------------------------------------------------

    [Fact]
    public void Translations_use_the_same_placeholders_as_the_english()
    {
        // {0}, {1,6}, {1:F1} all count as the same slot. A translation that drops
        // or invents one throws when the text is formatted.
        static SortedSet<int> Slots(string s) =>
            new(Regex.Matches(s, @"\{(\d+)")
                     .Select(m => int.Parse(m.Groups[1].Value)));

        var wrong = new List<string>();
        var english = new CultureInfo("en");

        foreach (var key in Strings.Keys)
        {
            var expected = Slots(Read(key, english));
            foreach (var culture in Cultures)
            {
                var actual = Slots(Read(key, culture));
                if (!expected.SetEquals(actual))
                    wrong.Add($"{key} in {culture.Name}: expected {{{string.Join(",", expected)}}}, "
                              + $"found {{{string.Join(",", actual)}}}");
            }
        }

        Assert.True(wrong.Count == 0, "Placeholder mismatch:\n" + string.Join("\n", wrong));
    }

    [Fact]
    public void Japanese_is_actually_different_from_english()
    {
        // Guards against the satellite assembly silently not being loaded, which
        // would quietly serve English everywhere.
        var en = new CultureInfo("en");
        var ja = new CultureInfo("ja");

        int different = Strings.Keys.Count(k => Read(k, en) != Read(k, ja));

        Assert.True(different > Strings.Keys.Length / 2,
            $"only {different} of {Strings.Keys.Length} strings differ between en and ja");
    }

    [Fact]
    public void Japanese_text_contains_japanese_characters()
    {
        var ja = new CultureInfo("ja");
        string sample = Read(nameof(Strings.ButtonStartBridge), ja);

        Assert.Matches(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]", sample);
    }

    // --- language selection --------------------------------------------------

    [Fact]
    public void The_default_language_is_japanese()
    {
        Assert.Equal("ja", Language.Default);
        Assert.Equal("ja", new Config().Language);
        Assert.Equal("ja", Config.Default().Language);
    }

    [Fact]
    public void Exactly_two_languages_ship()
    {
        Assert.Equal(2, Language.Supported.Length);
        Assert.Contains(Language.Supported, l => l.Code == "en");
        Assert.Contains(Language.Supported, l => l.Code == "ja");
    }

    [Fact]
    public void Display_names_are_written_in_their_own_language()
    {
        // Someone who opened the program in a language they cannot read still has
        // to be able to find theirs in the list.
        Assert.Equal("English", Language.DisplayName("en"));
        Assert.Equal("日本語", Language.DisplayName("ja"));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("ja", "ja")]
    [InlineData("es", "ja")]
    [InlineData("", "ja")]
    [InlineData(null, "ja")]
    public void An_unsupported_language_falls_back_to_the_default(string? given, string expected)
    {
        Assert.Equal(expected, Language.Normalise(given));
    }

    [Fact]
    public void Applying_a_language_sets_the_ui_culture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            Language.Apply("en");
            Assert.Equal("en", CultureInfo.CurrentUICulture.Name);

            Language.Apply("nonsense");
            Assert.Equal("ja", CultureInfo.CurrentUICulture.Name);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void The_language_survives_a_configuration_round_trip()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            new Config { Language = "en" }.Save(path);
            Assert.Equal("en", Config.Load(path).Language);
        }
        finally { File.Delete(path); }
    }
}
