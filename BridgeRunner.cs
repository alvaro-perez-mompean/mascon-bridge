namespace MasconBridge;

/// <summary>Snapshot of what the bridge is currently sending.</summary>
public readonly record struct BridgeState(
    int RawAxis,
    double Fraction,
    int NotchIndex,
    string NotchName,
    byte NotchValue,
    ushort Buttons,
    byte Hat);

/// <summary>
/// The bridge loop, shared by the console modes and the control window so the two
/// cannot drift apart.
/// </summary>
public sealed class BridgeRunner : IDisposable
{
    private readonly Config _cfg;
    private readonly object _lock = new();

    private VirtualMascon? _dev;
    private Thread? _thread;
    private volatile bool _stop;
    private BridgeState _state;

    public BridgeRunner(Config cfg) => _cfg = cfg;

    public bool IsRunning => _thread is { IsAlive: true };

    public BridgeState State
    {
        get { lock (_lock) return _state; }
    }

    /// <summary>
    /// Creates the virtual device and starts the loop. Synchronous on purpose: a
    /// missing elevation surfaces as an exception the caller can show.
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        var (vid, pid, product) = _cfg.ResolveDevice();
        _dev = new VirtualMascon(vid, pid, product);

        _stop = false;
        _thread = new Thread(Loop) { IsBackground = true, Name = "mascon-bridge" };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        _thread?.Join(1000);
        _thread = null;

        _dev?.Dispose();
        _dev = null;

        lock (_lock) _state = default;
    }

    private void Loop()
    {
        int firstNotch = _cfg.IncludeEmergencyInAxis ? 0 : 1;
        int zones = Zuiki.Notches.Length - firstNotch;
        int cur = 0;

        var extraDevices = _cfg.Buttons.Select(b => b.DeviceId)
            .Concat(_cfg.HatDeviceId >= 0 ? new[] { _cfg.HatDeviceId } : Array.Empty<int>())
            .Distinct().ToList();

        while (!_stop)
        {
            int rawAxis = -1;
            double p = 0;
            if (Joystick.TryRead(_cfg.AxisDeviceId, out var ja))
            {
                rawAxis = Joystick.GetAxis(ja, _cfg.AxisName);
                if (rawAxis >= 0)
                {
                    p = AxisFraction(rawAxis, _cfg);
                    cur = ZoneWithHysteresis(p, zones, cur, _cfg.Hysteresis);
                }
            }

            int notchIndex = firstNotch + cur;

            ushort buttons = 0;
            byte hat = Zuiki.HatCentered;
            bool emergency = false;

            var states = new Dictionary<int, Joystick.JoyInfoEx>();
            foreach (var id in extraDevices)
                if (Joystick.TryRead(id, out var js))
                    states[id] = js;

            foreach (var bind in _cfg.Buttons)
            {
                if (!states.TryGetValue(bind.DeviceId, out var js)) continue;
                if (!Joystick.IsButtonDown(js, bind.Button)) continue;

                if (string.Equals(bind.Mascon, "EB", StringComparison.OrdinalIgnoreCase))
                    emergency = true;
                else if (Zuiki.ButtonBits.TryGetValue(bind.Mascon, out int bit))
                    buttons |= (ushort)(1 << bit);
            }

            if (_cfg.HatDeviceId >= 0 && states.TryGetValue(_cfg.HatDeviceId, out var jh))
                hat = Joystick.PovToHat(jh.dwPOV);

            if (emergency) notchIndex = 0;

            var (name, value) = Zuiki.Notches[notchIndex];
            _dev!.Submit(Zuiki.BuildReport(value, buttons, hat));

            lock (_lock)
                _state = new BridgeState(rawAxis, p, notchIndex, name, value, buttons, hat);

            Thread.Sleep(Math.Max(1, _cfg.PollMs));
        }
    }

    /// <summary>Axis position normalised to 0..1, clamped and inverted as configured.</summary>
    public static double AxisFraction(int raw, Config cfg)
    {
        double p = (raw - cfg.AxisMin) / (double)Math.Max(1, cfg.AxisMax - cfg.AxisMin);
        p = Math.Clamp(p, 0, 1);
        return cfg.Invert ? 1 - p : p;
    }

    /// <summary>Splits 0..1 into equal zones, with hysteresis so edges do not chatter.</summary>
    public static int ZoneWithHysteresis(double p, int zones, int current, double hysteresis)
    {
        double w = 1.0 / zones;
        double h = Math.Clamp(hysteresis, 0, 0.49) * w;

        double lo = current * w;
        double hi = lo + w;

        if (p >= lo - h && p <= hi + h)
            return current;

        int idx = (int)Math.Floor(p / w);
        return Math.Clamp(idx, 0, zones - 1);
    }

    public void Dispose() => Stop();
}
