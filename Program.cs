using System.Runtime.InteropServices;
using MasconBridge;

// =============================================================================
//  mascon-bridge
//  Maps an analogue axis on any joystick or lever to a virtual ZUIKI One Handle
//  MasCon, so that JR EAST Train Simulator reads the ABSOLUTE POSITION of the
//  hardware instead of keystrokes.
//
//  With no arguments it opens the control window. Console modes, for diagnosis:
//    list       live view of joysticks, axes and buttons
//    calibrate  measures the handle's real travel and stores it in config.json
//    test       creates the virtual mascon and cycles the notches on its own
//    run        normal mode, no window
// =============================================================================

// Language before anything else, so even the first message comes out translated.
// Read straight from disk: Config.Load would print in the wrong language if the
// file is missing.
// --config <path> overrides where the settings live, for anyone keeping several
// setups or working in a source tree. Everything else uses %APPDATA%.
string? explicitConfig = ExplicitConfigPath(args);
string configPath = Config.Resolve(explicitConfig);

Language.Apply(ReadConfiguredLanguage());

string mode = args.FirstOrDefault(a => !a.StartsWith('-')) is { } first
    ? first.ToLowerInvariant()
    : "gui";

return mode switch
{
    "gui" => CmdGui(explicitConfig),
    "list" => CmdList(),
    "calibrate" => CmdCalibrate(configPath),
    "test" => CmdTest(configPath),
    "run" => CmdRun(configPath),
    _ => Usage(),
};

static string? ExplicitConfigPath(string[] args)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i].Equals("--config", StringComparison.OrdinalIgnoreCase))
            return args[i + 1];

    return null;
}

string ReadConfiguredLanguage()
{
    // Read straight from disk rather than through Config.Load: a missing file would
    // print its "created" message before the language is known.
    try
    {
        return File.Exists(configPath) ? Config.Load(configPath).Language : Language.Default;
    }
    catch
    {
        return Language.Default;
    }
}

static int Usage()
{
    Console.WriteLine(Strings.ConsoleUsage);
    Console.WriteLine(Strings.ConsoleUsageNoArgs);
    return 1;
}

// -----------------------------------------------------------------------------
static int CmdGui(string? explicitConfig)
{
    Native.HideConsole();

    int rc = 0;
    var t = new Thread(() =>
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Changing the language rebuilds the window rather than asking for a
            // restart: every control is created in code, so there is nothing to
            // re-translate in place.
            bool again;
            do
            {
                var form = new MainForm(explicitConfig);
                Application.Run(form);
                again = form.LanguageChanged;
            }
            while (again);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex.GetType().Name}: {ex.Message}", "Mascon Bridge",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            rc = 1;
        }
    });

    // WinForms needs an STA apartment, and the entry point generated for top level
    // statements cannot carry [STAThread].
    t.SetApartmentState(ApartmentState.STA);
    t.Start();
    t.Join();
    return rc;
}

// -----------------------------------------------------------------------------
static int CmdList()
{
    Console.WriteLine(Strings.ConsoleListHeader);
    Console.WriteLine();

    var ids = Joystick.Enumerate().Select(e => e.Id).ToList();
    if (ids.Count == 0)
    {
        Console.WriteLine(Strings.ConsoleNoJoystick);
        return 1;
    }

    while (true)
    {
        Console.SetCursorPosition(0, 2);
        foreach (var id in ids)
        {
            Joystick.TryCaps(id, out var caps);
            if (!Joystick.TryRead(id, out var j)) continue;

            Console.WriteLine(string.Format(Strings.ConsoleJoystickLine,
                id, $"{caps.wMid:X4}:{caps.wPid:X4}", caps.wNumAxes, caps.wNumButtons).PadRight(70));

            foreach (var ax in Joystick.AxisNames)
            {
                int v = Joystick.GetAxis(j, ax);
                Console.WriteLine(string.Format(Strings.ConsoleAxisLine,
                    ax, v, Bar(v, 0, 65535)).PadRight(70));
            }

            var pressed = Joystick.PressedButtons(j);
            Console.WriteLine(string.Format(Strings.ConsoleButtonsLine,
                pressed.Count == 0 ? "-" : string.Join(' ', pressed)).PadRight(70));
            Console.WriteLine(string.Format(Strings.ConsolePovLine, j.dwPOV).PadRight(70));
            Console.WriteLine(new string(' ', 70));
        }
        Thread.Sleep(80);
    }
}

static string Bar(int v, int min, int max)
{
    if (max <= min) return "";
    double f = Math.Clamp((v - min) / (double)(max - min), 0, 1);
    int n = (int)Math.Round(f * 24);
    return "[" + new string('#', n) + new string('.', 24 - n) + "]";
}

// -----------------------------------------------------------------------------
static int CmdCalibrate(string configPath)
{
    var cfg = Config.Load(configPath);
    FollowDevices(cfg, configPath);
    Console.WriteLine(string.Format(Strings.ConsoleCalibrating, cfg.AxisDeviceId, cfg.AxisName));
    Console.WriteLine(Strings.ConsoleCalibrateHint);
    Console.WriteLine();

    int min = int.MaxValue, max = int.MinValue;
    var stop = false;
    var t = new Thread(() => { Console.ReadLine(); stop = true; }) { IsBackground = true };
    t.Start();

    while (!stop)
    {
        if (Joystick.TryRead(cfg.AxisDeviceId, out var j))
        {
            int v = Joystick.GetAxis(j, cfg.AxisName);
            if (v >= 0)
            {
                min = Math.Min(min, v);
                max = Math.Max(max, v);
                Console.Write("\r" + string.Format(Strings.ConsoleCalibrateLive, v, min, max));
            }
        }
        Thread.Sleep(20);
    }

    if (min >= max)
    {
        Console.WriteLine();
        Console.WriteLine(Strings.ConsoleCalibrateNoMovement);
        return 1;
    }

    cfg.AxisMin = min;
    cfg.AxisMax = max;
    cfg.Save(configPath);
    Console.WriteLine();
    Console.WriteLine(string.Format(Strings.ConsoleCalibrateSaved, min, max));
    return 0;
}

// -----------------------------------------------------------------------------
static int CmdTest(string configPath)
{
    var cfg = Config.Load(configPath);
    var (vid, pid, product) = cfg.ResolveDevice();
    Console.WriteLine(string.Format(Strings.ConsoleCreatingDevice, vid, pid, product));

    using var dev = new VirtualMascon(vid, pid, product);
    Console.WriteLine(Strings.ConsoleTestReady);
    Console.WriteLine(Strings.ConsoleTestCycling);
    Console.WriteLine();

    int i = 0;
    while (true)
    {
        var (name, value) = Zuiki.Notches[i];
        dev.Submit(Zuiki.BuildReport(value, 0, Zuiki.HatCentered));
        Console.Write("\r" + string.Format(Strings.ConsoleTestNotch, name, value));
        i = (i + 1) % Zuiki.Notches.Length;
        Thread.Sleep(1200);
    }
}

// -----------------------------------------------------------------------------
// winmm renumbers joysticks whenever anything is plugged in or out. Follow the
// remembered devices to their new numbers before reading a single axis, and say so,
// because a silent renumber is exactly how the handle ends up reading the rudder.
static void FollowDevices(Config cfg, string configPath)
{
    var plan = cfg.RelocateDevices(DeviceMap.Present());
    if (!plan.Anything) return;

    foreach (var m in plan.Moves)
        Console.WriteLine(string.Format(Strings.ConsoleDeviceMoved, m.From, m.To, m.Identity));
    foreach (var g in plan.Missing)
        Console.WriteLine(string.Format(Strings.ConsoleDeviceMissing, g.Id, g.Identity));

    if (plan.Moves.Count > 0) cfg.Save(configPath);
    Console.WriteLine();
}

// -----------------------------------------------------------------------------
static int CmdRun(string configPath)
{
    var cfg = Config.Load(configPath);
    FollowDevices(cfg, configPath);
    var (vid, pid, product) = cfg.ResolveDevice();


    Console.WriteLine(string.Format(Strings.ConsoleRunHandle,
        cfg.AxisDeviceId, cfg.AxisName, cfg.AxisMin, cfg.AxisMax,
        cfg.Invert ? Strings.ConsoleRunInverted : ""));
    Console.WriteLine(string.Format(Strings.ConsoleRunZones,
        Zuiki.Notches.Length, Zuiki.Notches[0].Name));
    Console.WriteLine(string.Format(Strings.ConsoleRunVirtual, vid, pid, product));
    Console.WriteLine();

    using var runner = new BridgeRunner(cfg);
    runner.Start();

    var quit = new ManualResetEventSlim(false);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit.Set(); };

    string lastLine = "";
    while (!quit.IsSet)
    {
        var s = runner.State;
        string line = string.Format(Strings.ConsoleRunLine,
            s.NotchName, s.NotchValue, Convert.ToString(s.Buttons, 2).PadLeft(14, '0'));
        if (line != lastLine)
        {
            Console.Write("\r" + line.PadRight(60));
            lastLine = line;
        }
        Thread.Sleep(50);
    }

    Console.WriteLine();
    Console.WriteLine(Strings.ConsoleStopped);
    return 0;
}

/// <summary>Hides the console window when the graphical interface opens.</summary>
static class Native
{
    private const int SW_HIDE = 0;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void HideConsole()
    {
        var h = GetConsoleWindow();
        if (h != IntPtr.Zero) ShowWindow(h, SW_HIDE);
    }
}
