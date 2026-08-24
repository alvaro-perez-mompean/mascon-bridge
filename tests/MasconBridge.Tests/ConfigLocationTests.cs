using MasconBridge;

namespace MasconBridge.Tests;

/// <summary>
/// Where the settings live. This matters for updating: a release unzips into a folder
/// named after its version, so a configuration kept beside the executable is left
/// behind by every update, taking the calibration and every binding with it.
/// </summary>
public class ConfigLocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    private string AppData => Path.Combine(_root, "appdata", "mascon-bridge", "config.json");
    private string BesideExe => Path.Combine(_root, "v0.1.0", "config.json");

    public ConfigLocationTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BesideExe)!);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static void Write(string path, string language)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        new Config { Language = language }.Save(path);
    }

    // --- the everyday case ---------------------------------------------------

    [Fact]
    public void With_nothing_anywhere_the_settings_go_to_appdata()
    {
        Assert.Equal(AppData, Config.Resolve(null, AppData, BesideExe));
    }

    [Fact]
    public void An_existing_appdata_file_is_used()
    {
        Write(AppData, "en");

        Assert.Equal(AppData, Config.Resolve(null, AppData, BesideExe));
    }

    [Fact]
    public void An_explicit_path_always_wins()
    {
        Write(AppData, "en");
        string wanted = Path.Combine(_root, "elsewhere.json");

        Assert.Equal(wanted, Config.Resolve(wanted, AppData, BesideExe));
    }

    // --- coming from an older install ----------------------------------------

    [Fact]
    public void A_configuration_beside_the_executable_is_carried_across()
    {
        Write(BesideExe, "en");

        string resolved = Config.Resolve(null, AppData, BesideExe);

        Assert.Equal(AppData, resolved);
        Assert.True(File.Exists(AppData), "the old file has to be copied, not abandoned");
        Assert.Equal("en", Config.Load(resolved).Language);
    }

    [Fact]
    public void The_old_file_is_left_where_it_was()
    {
        // Copied, not moved: an update that goes wrong should leave the previous
        // install exactly as it was found.
        Write(BesideExe, "en");

        Config.Resolve(null, AppData, BesideExe);

        Assert.True(File.Exists(BesideExe));
    }

    [Fact]
    public void Once_carried_across_the_old_file_is_never_read_again()
    {
        Write(BesideExe, "en");
        Config.Resolve(null, AppData, BesideExe);

        // Somebody edits the leftover file, or an older version writes to it.
        Write(BesideExe, "ja");

        Assert.Equal("en", Config.Load(Config.Resolve(null, AppData, BesideExe)).Language);
    }

    [Fact]
    public void Appdata_wins_over_a_leftover_file_even_on_the_first_look()
    {
        Write(AppData, "en");
        Write(BesideExe, "ja");

        Assert.Equal("en", Config.Load(Config.Resolve(null, AppData, BesideExe)).Language);
    }

    [Fact]
    public void The_first_of_several_old_locations_is_the_one_taken()
    {
        string cwd = Path.Combine(_root, "cwd", "config.json");
        Write(cwd, "en");
        Write(BesideExe, "ja");

        Config.Resolve(null, AppData, cwd, BesideExe);

        Assert.Equal("en", Config.Load(AppData).Language);
    }

    // --- writing --------------------------------------------------------------

    [Fact]
    public void Saving_creates_the_folder_it_needs()
    {
        // %APPDATA%\mascon-bridge does not exist until something writes there.
        string fresh = Path.Combine(_root, "brand", "new", "config.json");

        new Config().Save(fresh);

        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void Loading_a_missing_file_creates_it_with_its_folder()
    {
        string fresh = Path.Combine(_root, "made", "on", "demand", "config.json");

        var cfg = Config.Load(fresh);

        Assert.True(File.Exists(fresh));
        Assert.Equal(Zuiki.DefaultModel, cfg.Model);
    }

    [Fact]
    public void The_real_appdata_path_sits_under_the_roaming_folder()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mascon-bridge", "config.json");

        Assert.Equal(expected, Config.InAppData);
    }
}
