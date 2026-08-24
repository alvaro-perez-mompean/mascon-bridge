using System.Net;
using System.Reflection;

namespace MasconBridge;

/// <summary>
/// Whether a newer release has been published.
///
/// It asks for the releases/latest page and reads the redirect rather than calling
/// the GitHub API: the API is rate limited to sixty calls an hour per address and
/// answers in JSON, while the redirect carries the tag in its Location header and
/// needs neither a token nor a parser.
///
/// Nothing is downloaded and nothing is installed. The most it does is offer a link.
/// </summary>
public static class UpdateCheck
{
    public const string ReleasesUrl =
        "https://github.com/alvaro-perez-mompean/mascon-bridge/releases";

    private const string LatestUrl = ReleasesUrl + "/latest";

    /// <summary>The version of the running build, from the assembly.</summary>
    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>
    /// The tag out of a redirect target, so ".../releases/tag/v1.2.3" gives "v1.2.3".
    /// Returns null for anything that is not a tag URL, which is what a rate limit
    /// page or an error page looks like.
    /// </summary>
    public static string? TagFromLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;

        int at = location.IndexOf("/releases/tag/", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;

        string tag = location[(at + "/releases/tag/".Length)..].Trim('/');
        return tag.Length == 0 ? null : tag;
    }

    /// <summary>
    /// True when the published tag is a later version than this build. Anything that
    /// does not parse as a version answers false: a tag nobody can compare is not a
    /// reason to tell somebody they are out of date.
    /// </summary>
    public static bool IsNewer(string? latestTag, string? currentVersion)
    {
        if (!TryVersion(latestTag, out var latest)) return false;
        if (!TryVersion(currentVersion, out var current)) return false;

        return latest > current;
    }

    private static bool TryVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V')) trimmed = trimmed[1..];

        // A tag like v1.2.3-beta compares on its numbers; the suffix is not ordered.
        int dash = trimmed.IndexOf('-');
        if (dash > 0) trimmed = trimmed[..dash];

        return Version.TryParse(trimmed, out version!);
    }

    /// <summary>
    /// The newest published tag, or null if it could not be found out. Every failure
    /// is the same answer: no network, no GitHub, a proxy in the way, all of it means
    /// "do not say anything", never an error in the user's face.
    /// </summary>
    public static async Task<string?> LatestTagAsync(CancellationToken cancel = default)
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };

            // GitHub refuses requests without one.
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"mascon-bridge/{CurrentVersion}");

            using var request = new HttpRequestMessage(HttpMethod.Head, LatestUrl);
            using var response = await http.SendAsync(request, cancel).ConfigureAwait(false);

            if (response.StatusCode is not (HttpStatusCode.Found or HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect))
                return null;

            return TagFromLocation(response.Headers.Location?.ToString());
        }
        catch
        {
            return null;
        }
    }
}
