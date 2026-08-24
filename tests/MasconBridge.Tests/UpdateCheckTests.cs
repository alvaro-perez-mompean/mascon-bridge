using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// Reading the published tag and deciding whether it is worth mentioning. Nothing
/// here talks to the network: the parts that can be got wrong are the two pure ones,
/// and both fail in the same direction — say nothing rather than nag.
/// </summary>
public class UpdateCheckTests
{
    // --- reading the tag out of the redirect ---------------------------------

    [Theory]
    [InlineData("https://github.com/owner/repo/releases/tag/v0.2.0", "v0.2.0")]
    [InlineData("https://github.com/owner/repo/releases/tag/v1.10.3", "v1.10.3")]
    [InlineData("/owner/repo/releases/tag/v0.1.0", "v0.1.0")]
    [InlineData("https://github.com/owner/repo/releases/tag/v0.2.0/", "v0.2.0")]
    public void The_tag_comes_out_of_the_redirect_target(string location, string expected)
    {
        Assert.Equal(expected, UpdateCheck.TagFromLocation(location));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://github.com/owner/repo/releases")]
    [InlineData("https://github.com/login?return_to=%2Fowner%2Frepo")]
    [InlineData("https://github.com/owner/repo/releases/tag/")]
    public void Anything_that_is_not_a_tag_url_reads_as_nothing(string? location)
    {
        // A rate limit page and a sign-in page both land here, and neither is news.
        Assert.Null(UpdateCheck.TagFromLocation(location));
    }

    // --- deciding whether to say anything ------------------------------------

    [Theory]
    [InlineData("v0.2.0", "0.1.0")]
    [InlineData("v1.0.0", "0.9.9")]
    [InlineData("v0.1.1", "0.1.0")]
    [InlineData("0.2.0", "0.1.0")]
    public void A_later_release_is_worth_mentioning(string tag, string current)
    {
        Assert.True(UpdateCheck.IsNewer(tag, current));
    }

    [Theory]
    [InlineData("v0.1.0", "0.1.0")]
    [InlineData("v0.1.0", "0.2.0")]
    [InlineData("v0.1.0", "1.0.0")]
    [InlineData("v0.9.9", "0.10.0")]
    public void The_same_or_an_older_release_is_not(string tag, string current)
    {
        Assert.False(UpdateCheck.IsNewer(tag, current));
    }

    [Theory]
    [InlineData(null, "0.1.0")]
    [InlineData("", "0.1.0")]
    [InlineData("nightly", "0.1.0")]
    [InlineData("v", "0.1.0")]
    [InlineData("v0.2.0", null)]
    [InlineData("v0.2.0", "not a version")]
    public void Anything_that_cannot_be_compared_says_nothing(string? tag, string? current)
    {
        // Telling somebody they are out of date on the strength of a tag nobody can
        // order is worse than staying quiet.
        Assert.False(UpdateCheck.IsNewer(tag, current));
    }

    [Fact]
    public void A_prerelease_suffix_compares_on_its_numbers()
    {
        Assert.True(UpdateCheck.IsNewer("v0.2.0-beta", "0.1.0"));
        Assert.False(UpdateCheck.IsNewer("v0.1.0-beta", "0.1.0"));
    }

    [Fact]
    public void Ten_sorts_after_nine_rather_than_before_it()
    {
        // The string comparison that would be the obvious shortcut gets this wrong.
        Assert.True(UpdateCheck.IsNewer("v0.10.0", "0.9.0"));
    }

    // --- the running build ---------------------------------------------------

    [Fact]
    public void The_current_version_is_a_comparable_three_part_number()
    {
        Assert.True(Version.TryParse(UpdateCheck.CurrentVersion, out _),
            $"'{UpdateCheck.CurrentVersion}' has to parse for any comparison to work");
    }

    [Fact]
    public void The_releases_link_points_at_this_repository()
    {
        Assert.StartsWith("https://github.com/", UpdateCheck.ReleasesUrl);
        Assert.EndsWith("/releases", UpdateCheck.ReleasesUrl);
    }
}
