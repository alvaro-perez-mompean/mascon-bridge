using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasconBridge;

public sealed class ButtonBinding
{
    /// <summary>Physical joystick number, as shown by "list".</summary>
    public int DeviceId { get; set; }

    /// <summary>Physical button number, starting at 1.</summary>
    public int Button { get; set; }

    /// <summary>Mascon button: Y, B, A, X, L, R, ZL, ZR, Minus, Plus, Home, Capture or EB.</summary>
    public string Mascon { get; set; } = "A";
}

public sealed class Config
{
    public const string DefaultPath = "config.json";

    // --- Handle ---
    public int AxisDeviceId { get; set; } = 1;
    public string AxisName { get; set; } = "Z";
    public int AxisMin { get; set; } = 0;
    public int AxisMax { get; set; } = 65535;
    public bool Invert { get; set; } = false;

    /// <summary>Hysteresis as a fraction of one zone's width (0 disables it).</summary>
    public double Hysteresis { get; set; } = 0.25;

    // --- Catches ---
    // Both ends of the real handle are protected, by different means and against
    // different mistakes: a thumb release button guards N to P1 so power cannot be
    // taken by accident, and a cam guards B8 to EB so emergency cannot. An analogue
    // lever cannot reproduce the cam's extra force, so both become buttons here.

    /// <summary>Joystick carrying the button that unlocks power, -1 to disable.</summary>
    public int PowerReleaseDeviceId { get; set; } = -1;

    /// <summary>Physical button number, starting at 1.</summary>
    public int PowerReleaseButton { get; set; } = 1;

    /// <summary>Joystick carrying the button that unlocks EB, -1 to disable.</summary>
    public int EmergencyReleaseDeviceId { get; set; } = -1;

    /// <summary>Physical button number, starting at 1.</summary>
    public int EmergencyReleaseButton { get; set; } = 1;

    // --- Hat ---
    /// <summary>-1 disables the hat.</summary>
    public int HatDeviceId { get; set; } = -1;

    // --- Buttons ---
    public List<ButtonBinding> Buttons { get; set; } = new();

    // --- Virtual device ---
    public string Model { get; set; } = Zuiki.DefaultModel;

    /// <summary>Overrides the model's vendor id, e.g. "0x0F0D".</summary>
    public string? Vid { get; set; }
    public string? Pid { get; set; }
    public string? ProductString { get; set; }

    public int PollMs { get; set; } = 8;

    // --- Interface ---
    /// <summary>Language code, "ja" or "en". See Language.Supported.</summary>
    public string Language { get; set; } = MasconBridge.Language.Default;

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Config Load(string path)
    {
        if (!File.Exists(path))
        {
            var fresh = Default();
            File.WriteAllText(path, JsonSerializer.Serialize(fresh, Opts));
            Console.WriteLine(string.Format(Strings.ConfigCreatedSample, path));
            return fresh;
        }

        return JsonSerializer.Deserialize<Config>(File.ReadAllText(path), Opts)
               ?? throw new InvalidDataException(Strings.ConfigUnreadable);
    }

    public void Save(string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(this, Opts));

    /// <summary>
    /// Written on first run. Deliberately neutral: the control panel picks the first
    /// device present, and the axis has to be identified by hand anyway.
    /// </summary>
    public static Config Default() => new()
    {
        AxisDeviceId = 0,
        AxisName = "Z",
        Buttons =
        {
            new ButtonBinding { DeviceId = 0, Button = 1, Mascon = "A" },
            new ButtonBinding { DeviceId = 0, Button = 2, Mascon = "B" },
            new ButtonBinding { DeviceId = 0, Button = 3, Mascon = "X" },
            new ButtonBinding { DeviceId = 0, Button = 4, Mascon = "Y" },
        },
    };

    public (ushort Vid, ushort Pid, string Product) ResolveDevice()
    {
        var model = Zuiki.KnownModels.FirstOrDefault(
            m => string.Equals(m.Model, Model, StringComparison.OrdinalIgnoreCase));

        // An unrecognised name falls back to the model the game actually accepts,
        // not to whichever happens to be first in the table.
        if (model.Model is null)
            model = Zuiki.KnownModels.First(
                m => string.Equals(m.Model, Zuiki.DefaultModel, StringComparison.OrdinalIgnoreCase));

        ushort vid = ParseHex(Vid) ?? model.Vid;
        ushort pid = ParseHex(Pid) ?? model.Pid;
        return (vid, pid, ProductString ?? model.Product);
    }

    private static ushort? ParseHex(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return ushort.TryParse(s, NumberStyles.HexNumber, null, out var v) ? v : null;
    }
}
