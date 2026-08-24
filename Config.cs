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
    public const string FileName = "config.json";

    /// <summary>Kept for callers that want a bare relative path.</summary>
    public const string DefaultPath = FileName;

    /// <summary>
    /// %APPDATA%\mascon-bridge\config.json — outside the program's own folder on
    /// purpose. A release unzips into a folder named after its version, so a
    /// configuration living beside the executable would be left behind by every
    /// update, taking the calibration and every binding with it.
    /// </summary>
    public static string InAppData => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "mascon-bridge", FileName);

    /// <summary>
    /// Where this run reads and writes its settings.
    ///
    /// An explicit path always wins. Otherwise the one in %APPDATA% is used, and the
    /// first time a configuration is found beside the executable instead — an install
    /// from before this moved — it is copied across rather than abandoned.
    /// </summary>
    public static string Resolve(string? explicitPath, string appData, params string[] legacy)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath)) return Path.GetFullPath(explicitPath);
        if (File.Exists(appData)) return appData;

        foreach (var old in legacy)
        {
            if (!File.Exists(old)) continue;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(appData)!);
                File.Copy(old, appData);
                return appData;
            }
            catch (IOException)
            {
                // Nothing to gain from failing here: the old file still works.
                return old;
            }
            catch (UnauthorizedAccessException)
            {
                return old;
            }
        }

        return appData;
    }

    /// <summary>The everyday case: no explicit path, the real folders.</summary>
    public static string Resolve(string? explicitPath = null) => Resolve(
        explicitPath,
        InAppData,
        Path.GetFullPath(FileName),
        Path.Combine(AppContext.BaseDirectory, FileName));

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

    // --- Devices ---
    /// <summary>
    /// What each joystick number was, as "2": "044F:B687". winmm renumbers devices
    /// whenever anything is plugged in or out, so the numbers above are only useful
    /// alongside a note of what they pointed at. See <see cref="DeviceMap"/>.
    /// </summary>
    public Dictionary<int, string> Devices { get; set; } = new();

    // --- Overlay ---
    /// <summary>The notch strip drawn over the game while the bridge runs.</summary>
    public bool OverlayEnabled { get; set; } = true;

    /// <summary>
    /// Where it was left. int.MinValue means it has never been placed, so it starts
    /// against the right hand edge; a position on a monitor that is gone is pulled
    /// back onto a screen rather than leaving it invisible.
    /// </summary>
    public int OverlayX { get; set; } = int.MinValue;

    public int OverlayY { get; set; } = int.MinValue;

    // --- Virtual device ---
    public string Model { get; set; } = Zuiki.DefaultModel;

    /// <summary>Overrides the model's vendor id, e.g. "0x0F0D".</summary>
    public string? Vid { get; set; }
    public string? Pid { get; set; }
    public string? ProductString { get; set; }

    public int PollMs { get; set; } = 8;

    // --- Interface ---
    /// <summary>
    /// Ask GitHub once at startup whether a newer release exists. Nothing is
    /// downloaded either way; the window only offers a link.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

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
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(fresh, Opts));
            Console.WriteLine(string.Format(Strings.ConfigCreatedSample, path));
            return fresh;
        }

        return JsonSerializer.Deserialize<Config>(File.ReadAllText(path), Opts)
               ?? throw new InvalidDataException(Strings.ConfigUnreadable);
    }

    public void Save(string path)
    {
        // The %APPDATA% folder does not exist until something writes there.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Opts));
    }

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

    /// <summary>Every joystick number this configuration depends on.</summary>
    public IEnumerable<int> ReferencedDevices()
    {
        var seen = new HashSet<int>();
        foreach (var id in Ids())
            if (id >= 0 && seen.Add(id))
                yield return id;

        IEnumerable<int> Ids()
        {
            yield return AxisDeviceId;
            yield return HatDeviceId;
            yield return PowerReleaseDeviceId;
            yield return EmergencyReleaseDeviceId;
            foreach (var b in Buttons) yield return b.DeviceId;
        }
    }

    /// <summary>
    /// Writes down what each referenced number points at right now, so a later run
    /// can find those devices again after Windows has renumbered them.
    /// </summary>
    public void RememberDevices(IReadOnlyDictionary<int, string> present)
    {
        foreach (var id in ReferencedDevices())
            if (present.TryGetValue(id, out var identity))
                Devices[id] = identity;
    }

    /// <summary>
    /// Follows the remembered devices to wherever they are now and rewrites every
    /// number that names one. Devices that are not connected are left alone: their
    /// numbers stay put, and the plan says which ones they are so the caller can
    /// tell the user instead of silently reading the wrong axis.
    /// </summary>
    public DeviceMap.Plan RelocateDevices(IReadOnlyDictionary<int, string> present)
    {
        var plan = DeviceMap.Match(Devices, present);
        if (plan.Remap.Count == 0) return plan;

        AxisDeviceId = plan.Apply(AxisDeviceId);
        HatDeviceId = plan.Apply(HatDeviceId);
        PowerReleaseDeviceId = plan.Apply(PowerReleaseDeviceId);
        EmergencyReleaseDeviceId = plan.Apply(EmergencyReleaseDeviceId);
        foreach (var b in Buttons) b.DeviceId = plan.Apply(b.DeviceId);

        var moved = new Dictionary<int, string>();
        foreach (var (id, identity) in Devices) moved[plan.Apply(id)] = identity;
        Devices = moved;

        return plan;
    }

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
