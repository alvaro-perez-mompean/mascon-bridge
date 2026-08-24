using MasconBridge;

namespace MasconBridge.Tests;

public class ConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mascon-bridge-tests", Guid.NewGuid().ToString("N"));

    public ConfigTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    // --- ResolveDevice -------------------------------------------------------

    [Theory]
    [InlineData("ZKNS-001", 0x0F0D, 0x00C1)]
    [InlineData("ZKNS-001b", 0x33DD, 0x0001)]
    [InlineData("ZKNS-002", 0x33DD, 0x0002)]
    [InlineData("ZKNS-011", 0x33DD, 0x0003)]
    [InlineData("ZKNS-012", 0x33DD, 0x0004)]
    [InlineData("ZKNS-013", 0x33DD, 0x0005)]
    public void Every_known_model_resolves_to_its_identity(string model, int vid, int pid)
    {
        var (v, p, product) = new Config { Model = model }.ResolveDevice();

        Assert.Equal((ushort)vid, v);
        Assert.Equal((ushort)pid, p);
        Assert.Equal("One Handle MasCon for Nintendo Switch", product);
    }

    [Fact]
    public void Model_name_is_matched_regardless_of_case()
    {
        var (v, p, _) = new Config { Model = "zkns-002" }.ResolveDevice();
        Assert.Equal((ushort)0x33DD, v);
        Assert.Equal((ushort)0x0002, p);
    }

    [Fact]
    public void An_unknown_model_falls_back_to_the_one_the_game_accepts()
    {
        // Not KnownModels[0]: that is ZKNS-001, which the game ignores.
        var expected = Zuiki.KnownModels.First(m => m.Model == Zuiki.DefaultModel);
        var (v, p, _) = new Config { Model = "not-a-mascon" }.ResolveDevice();

        Assert.Equal(expected.Vid, v);
        Assert.Equal(expected.Pid, p);
    }

    [Fact]
    public void The_default_model_is_one_of_the_known_ones()
    {
        Assert.Contains(Zuiki.KnownModels, m => m.Model == Zuiki.DefaultModel);
    }

    [Fact]
    public void The_default_model_uses_the_zuiki_vendor_id()
    {
        // 0x0F0D is Nintendo's, inherited from the Switch pad, and the game ignores
        // it. Defaulting to a model on that id would ship a broken configuration.
        var model = Zuiki.KnownModels.First(m => m.Model == Zuiki.DefaultModel);
        Assert.Equal((ushort)0x33DD, model.Vid);
    }

    [Fact]
    public void A_fresh_config_defaults_to_that_model()
    {
        Assert.Equal(Zuiki.DefaultModel, new Config().Model);
        Assert.Equal(Zuiki.DefaultModel, Config.Default().Model);
    }

    [Theory]
    [InlineData("0x33DD", "0x0002")]
    [InlineData("33DD", "0002")]
    [InlineData("0X33dd", "2")]
    public void Explicit_ids_override_the_model(string vid, string pid)
    {
        var (v, p, _) = new Config { Model = "ZKNS-001", Vid = vid, Pid = pid }.ResolveDevice();
        Assert.Equal((ushort)0x33DD, v);
        Assert.Equal((ushort)0x0002, p);
    }

    [Fact]
    public void An_out_of_range_id_is_rejected_rather_than_truncated()
    {
        // 0x1F0D0 does not fit in a ushort. Silently truncating it to 0xF0D0 would
        // produce a device that looks valid and matches nothing.
        var (v, _, _) = new Config { Model = "ZKNS-002", Vid = "0x1F0D0" }.ResolveDevice();
        Assert.Equal((ushort)0x33DD, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    [InlineData("0xZZZZ")]
    [InlineData(null)]
    public void An_unparseable_id_falls_back_to_the_model(string? vid)
    {
        var (v, _, _) = new Config { Model = "ZKNS-002", Vid = vid }.ResolveDevice();
        Assert.Equal((ushort)0x33DD, v);
    }

    [Fact]
    public void Product_string_can_be_overridden()
    {
        var (_, _, product) = new Config { Model = "ZKNS-002", ProductString = "Custom" }.ResolveDevice();
        Assert.Equal("Custom", product);
    }

    // --- Load and Save -------------------------------------------------------

    [Fact]
    public void Loading_a_missing_file_writes_the_defaults()
    {
        var path = Path_("config.json");
        Assert.False(File.Exists(path));

        var cfg = Config.Load(path);

        Assert.True(File.Exists(path));
        Assert.Equal(Config.Default().AxisName, cfg.AxisName);
    }

    [Fact]
    public void A_generated_file_can_be_read_back()
    {
        var path = Path_("config.json");
        Config.Load(path);

        var reloaded = Config.Load(path);
        Assert.Equal(Config.Default().AxisDeviceId, reloaded.AxisDeviceId);
    }

    [Fact]
    public void Saving_and_loading_preserves_every_field()
    {
        var path = Path_("roundtrip.json");
        var original = new Config
        {
            AxisDeviceId = 2,
            AxisName = "Z",
            AxisMin = 123,
            AxisMax = 45678,
            Invert = true,
            PowerReleaseDeviceId = 3,
            PowerReleaseButton = 5,
            EmergencyReleaseDeviceId = 2,
            EmergencyReleaseButton = 9,
            Hysteresis = 0.35,
            HatDeviceId = 3,
            Model = "ZKNS-002",
            ProductString = "Custom",
            PollMs = 4,
            Buttons =
            {
                new ButtonBinding { DeviceId = 3, Button = 7, Mascon = "ZL" },
                new ButtonBinding { DeviceId = 2, Button = 1, Mascon = "EB" },
            },
        };

        original.Save(path);
        var loaded = Config.Load(path);

        Assert.Equal(original.AxisDeviceId, loaded.AxisDeviceId);
        Assert.Equal(original.AxisName, loaded.AxisName);
        Assert.Equal(original.AxisMin, loaded.AxisMin);
        Assert.Equal(original.AxisMax, loaded.AxisMax);
        Assert.Equal(original.Invert, loaded.Invert);
        Assert.Equal(original.PowerReleaseDeviceId, loaded.PowerReleaseDeviceId);
        Assert.Equal(original.PowerReleaseButton, loaded.PowerReleaseButton);
        Assert.Equal(original.EmergencyReleaseDeviceId, loaded.EmergencyReleaseDeviceId);
        Assert.Equal(original.EmergencyReleaseButton, loaded.EmergencyReleaseButton);
        Assert.Equal(original.Hysteresis, loaded.Hysteresis);
        Assert.Equal(original.HatDeviceId, loaded.HatDeviceId);
        Assert.Equal(original.Model, loaded.Model);
        Assert.Equal(original.ProductString, loaded.ProductString);
        Assert.Equal(original.PollMs, loaded.PollMs);

        Assert.Equal(2, loaded.Buttons.Count);
        Assert.Equal("ZL", loaded.Buttons[0].Mascon);
        Assert.Equal(7, loaded.Buttons[0].Button);
        Assert.Equal(3, loaded.Buttons[0].DeviceId);
    }

    [Fact]
    public void Property_names_are_read_regardless_of_case()
    {
        var path = Path_("lowercase.json");
        File.WriteAllText(path, """{ "axisdeviceid": 4, "axisname": "R", "model": "ZKNS-011" }""");

        var cfg = Config.Load(path);

        Assert.Equal(4, cfg.AxisDeviceId);
        Assert.Equal("R", cfg.AxisName);
        Assert.Equal("ZKNS-011", cfg.Model);
    }

    [Fact]
    public void An_unreadable_file_raises_rather_than_starting_with_junk()
    {
        var path = Path_("broken.json");
        File.WriteAllText(path, "this is not json");

        Assert.ThrowsAny<Exception>(() => Config.Load(path));
    }

    // --- Defaults ------------------------------------------------------------

    [Fact]
    public void Defaults_are_usable_on_a_machine_we_know_nothing_about()
    {
        var cfg = Config.Default();

        Assert.Contains(cfg.AxisName, Joystick.AxisNames);
        Assert.True(cfg.AxisDeviceId >= 0);
        Assert.True(cfg.AxisMax > cfg.AxisMin);
        Assert.InRange(cfg.Hysteresis, 0, 0.49);
        Assert.True(cfg.PollMs > 0);

        // Neither catch is set up out of the box, so the handle covers the whole
        // scale, emergency included, without needing a button to be found first.
        Assert.True(cfg.PowerReleaseDeviceId < 0);
        Assert.True(cfg.EmergencyReleaseDeviceId < 0);
    }

    [Fact]
    public void Default_button_bindings_name_real_mascon_buttons()
    {
        foreach (var b in Config.Default().Buttons)
            Assert.True(Zuiki.ButtonBits.ContainsKey(b.Mascon)
                        || b.Mascon.Equals("EB", StringComparison.OrdinalIgnoreCase),
                $"'{b.Mascon}' is not a mascon button");
    }

    [Fact]
    public void Default_resolves_to_a_known_device()
    {
        var (v, p, _) = Config.Default().ResolveDevice();
        Assert.Contains(Zuiki.KnownModels, m => m.Vid == v && m.Pid == p);
    }
}
