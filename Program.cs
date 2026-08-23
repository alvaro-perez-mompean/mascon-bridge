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

string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "gui";

return mode switch
{
    "gui" => CmdGui(),
    "list" => CmdList(),
    "calibrate" => CmdCalibrate(),
    "test" => CmdTest(),
    "run" => CmdRun(),
    _ => Usage(),
};

static int Usage()
{
    Console.WriteLine("Usage: mascon-bridge [gui|list|calibrate|test|run]");
    Console.WriteLine("  with no arguments it opens the control window");
    return 1;
}

// -----------------------------------------------------------------------------
static int CmdGui()
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
            Application.Run(new MainForm());
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
    Console.WriteLine("Move the handles and press buttons. Ctrl+C to quit.\n");
    var ids = Joystick.Enumerate().Select(e => e.Id).ToList();
    if (ids.Count == 0)
    {
        Console.WriteLine("No joystick detected.");
        return 1;
    }

    while (true)
    {
        Console.SetCursorPosition(0, 2);
        foreach (var id in ids)
        {
            Joystick.TryCaps(id, out var caps);
            if (!Joystick.TryRead(id, out var j)) continue;

            Console.WriteLine($"Joystick {id}: {caps.wMid:X4}:{caps.wPid:X4}  {caps.wNumAxes} axes, {caps.wNumButtons} buttons".PadRight(70));
            foreach (var ax in Joystick.AxisNames)
            {
                int v = Joystick.GetAxis(j, ax);
                Console.WriteLine($"   axis {ax}: {v,6}  {Bar(v, 0, 65535)}".PadRight(70));
            }
            var pressed = Joystick.PressedButtons(j);
            Console.WriteLine($"   buttons: {(pressed.Count == 0 ? "-" : string.Join(' ', pressed))}".PadRight(70));
            Console.WriteLine($"   POV: {j.dwPOV}".PadRight(70));
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
static int CmdCalibrate()
{
    var cfg = Config.Load(Config.DefaultPath);
    Console.WriteLine($"Calibrating joystick {cfg.AxisDeviceId}, axis {cfg.AxisName}.");
    Console.WriteLine("Move the handle end to end a few times, then press Enter.\n");

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
                Console.Write($"\r  current {v,6}   min {min,6}   max {max,6}   ");
            }
        }
        Thread.Sleep(20);
    }

    if (min >= max)
    {
        Console.WriteLine("\nNo movement seen. Check the joystick number and axis with 'list'.");
        return 1;
    }

    cfg.AxisMin = min;
    cfg.AxisMax = max;
    cfg.Save(Config.DefaultPath);
    Console.WriteLine($"\nSaved: AxisMin={min}, AxisMax={max}");
    return 0;
}

// -----------------------------------------------------------------------------
static int CmdTest()
{
    var cfg = Config.Load(Config.DefaultPath);
    var (vid, pid, product) = cfg.ResolveDevice();
    Console.WriteLine($"Creating virtual mascon  VID=0x{vid:X4}  PID=0x{pid:X4}  \"{product}\"");

    using var dev = new VirtualMascon(vid, pid, product);
    Console.WriteLine("Ready. Open joy.cpl or Steam's controller test and watch the Y axis.");
    Console.WriteLine("Cycling the notches... Ctrl+C to quit.\n");

    int i = 0;
    while (true)
    {
        var (name, value) = Zuiki.Notches[i];
        dev.Submit(Zuiki.BuildReport(value, 0, Zuiki.HatCentered));
        Console.Write($"\r  notch {name,-3}  value 0x{value:X2}   ");
        i = (i + 1) % Zuiki.Notches.Length;
        Thread.Sleep(1200);
    }
}

// -----------------------------------------------------------------------------
static int CmdRun()
{
    var cfg = Config.Load(Config.DefaultPath);
    var (vid, pid, product) = cfg.ResolveDevice();

    int firstNotch = cfg.IncludeEmergencyInAxis ? 0 : 1;
    int zones = Zuiki.Notches.Length - firstNotch;

    Console.WriteLine($"Handle : joystick {cfg.AxisDeviceId}, axis {cfg.AxisName}, "
                      + $"range {cfg.AxisMin}..{cfg.AxisMax}{(cfg.Invert ? " (inverted)" : "")}");
    Console.WriteLine($"Zones  : {zones} ({Zuiki.Notches[firstNotch].Name} .. P5)");
    Console.WriteLine($"Virtual: VID=0x{vid:X4} PID=0x{pid:X4} \"{product}\"");
    Console.WriteLine();

    using var runner = new BridgeRunner(cfg);
    runner.Start();

    var quit = new ManualResetEventSlim(false);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit.Set(); };

    string lastLine = "";
    while (!quit.IsSet)
    {
        var s = runner.State;
        string line = $"  {s.NotchName,-3}  0x{s.NotchValue:X2}   buttons {Convert.ToString(s.Buttons, 2).PadLeft(14, '0')}";
        if (line != lastLine)
        {
            Console.Write("\r" + line.PadRight(60));
            lastLine = line;
        }
        Thread.Sleep(50);
    }

    Console.WriteLine("\nStopped.");
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
