using System.Runtime.InteropServices;

namespace MasconBridge;

/// <summary>
/// Joystick input through the classic Windows multimedia API (winmm). No external
/// dependencies, and it works with any joystick-class device Windows exposes.
/// </summary>
public static class Joystick
{
    private const int JOYERR_NOERROR = 0;
    private const int JOY_RETURNALL = 0x000000FF;
    private const int MAX_DEVICES = 16;

    [StructLayout(LayoutKind.Sequential)]
    public struct JoyInfoEx
    {
        public int dwSize;
        public int dwFlags;
        public int dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
        public int dwButtons, dwButtonNumber, dwPOV;
        public int dwReserved1, dwReserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct JoyCaps
    {
        public ushort wMid, wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
        public int wXmin, wXmax, wYmin, wYmax, wZmin, wZmax;
        public int wNumButtons, wPeriodMin, wPeriodMax;
        public int wRmin, wRmax, wUmin, wUmax, wVmin, wVmax;
        public int wCaps, wMaxAxes, wNumAxes, wMaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
    }

    [DllImport("winmm.dll")]
    private static extern int joyGetPosEx(int uJoyID, ref JoyInfoEx pji);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "joyGetDevCapsW")]
    private static extern int joyGetDevCaps(IntPtr uJoyID, ref JoyCaps pjc, int cbjc);

    public static bool TryRead(int id, out JoyInfoEx info)
    {
        info = new JoyInfoEx
        {
            dwSize = Marshal.SizeOf<JoyInfoEx>(),
            dwFlags = JOY_RETURNALL,
        };
        return joyGetPosEx(id, ref info) == JOYERR_NOERROR;
    }

    public static bool TryCaps(int id, out JoyCaps caps)
    {
        caps = default;
        return joyGetDevCaps((IntPtr)id, ref caps, Marshal.SizeOf<JoyCaps>()) == JOYERR_NOERROR;
    }

    public static IEnumerable<(int Id, JoyCaps Caps)> Enumerate()
    {
        for (int i = 0; i < MAX_DEVICES; i++)
            if (TryRead(i, out _) && TryCaps(i, out var caps))
                yield return (i, caps);
    }

    public static readonly string[] AxisNames = { "X", "Y", "Z", "R", "U", "V" };

    public static int GetAxis(in JoyInfoEx j, string name) => name.ToUpperInvariant() switch
    {
        "X" => j.dwXpos,
        "Y" => j.dwYpos,
        "Z" => j.dwZpos,
        "R" => j.dwRpos,
        "U" => j.dwUpos,
        "V" => j.dwVpos,
        _ => -1,
    };

    /// <summary>Buttons currently held down, numbered from 1.</summary>
    public static List<int> PressedButtons(in JoyInfoEx j)
    {
        var list = new List<int>();
        int mask = j.dwButtons;
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0)
                list.Add(i + 1);
        return list;
    }

    public static bool IsButtonDown(in JoyInfoEx j, int button1Based)
        => button1Based >= 1 && button1Based <= 32 && (j.dwButtons & (1 << (button1Based - 1))) != 0;

    /// <summary>Converts the POV reading (hundredths of a degree) to the mascon's 8 way hat.</summary>
    public static byte PovToHat(int pov)
    {
        if (pov < 0 || pov > 35999) return Zuiki.HatCentered;
        int oct = (int)Math.Round(pov / 4500.0) % 8;
        return (byte)oct;
    }
}
