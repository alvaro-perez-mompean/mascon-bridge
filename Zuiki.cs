namespace MasconBridge;

/// <summary>
/// ZUIKI "One Handle MasCon" data (ZKNS-001 and the 002/011/012/013 variants).
/// HID descriptor and notch values are documented in the Train Controller Database
/// (https://traincontrollerdb.marcriera.cat/hardware/zkns001/) and corroborated by
/// the ConToJREts source.
///
/// Input report, 8 bytes:
///   byte 0 : buttons 0-7   (Y, B, A, X, L, R, ZL, ZR)
///   byte 1 : buttons 8-13  (Minus, Plus, --, --, Home, Capture) + 2 padding bits
///   byte 2 : hat switch in the low nibble (0=up, 2=right, 4=down, 6=left, 8=centre)
///   byte 3 : X axis   unused -> 0x80
///   byte 4 : Y axis   == brake/power handle
///   byte 5 : Z axis   unused -> 0x80
///   byte 6 : Rz axis  unused -> 0x80
///   byte 7 : padding -> 0x00
/// </summary>
public static class Zuiki
{
    /// <summary>Original HID report descriptor, 94 bytes.</summary>
    public const string DescriptorHex =
        "05010905A10115002501350045017501" +
        "950E05091901290E8102950281010501" +
        "2507463B017504950165140939814265" +
        "009501810126FF0046FF000930093109" +
        "3209357508950481027508950181010A" +
        "4F4875089508B1020A4F489102C0";

    public const int InputReportSize = 8;

    public const byte HatCentered = 0x08;

    /// <summary>The 15 handle positions, EB to P5, with their exact Y axis value.</summary>
    public static readonly (string Name, byte Value)[] Notches =
    {
        ("EB", 0x00),
        ("B8", 0x05), ("B7", 0x13), ("B6", 0x20), ("B5", 0x2E),
        ("B4", 0x3C), ("B3", 0x49), ("B2", 0x57), ("B1", 0x65),
        ("N",  0x80),
        ("P1", 0x9F), ("P2", 0xB7), ("P3", 0xCE), ("P4", 0xE6), ("P5", 0xFF),
    };

    /// <summary>
    /// The model to emulate unless told otherwise. Steam recognises all six, but the
    /// game only reacts to the ones on ZUIKI's own 33DD vendor id. ZKNS-001 carries
    /// Nintendo's 0F0D, inherited from the Switch pad, and the game ignores it.
    /// </summary>
    public const string DefaultModel = "ZKNS-002";

    /// <summary>Models the game recognises. They all share the same descriptor.</summary>
    public static readonly (string Model, ushort Vid, ushort Pid, string Product)[] KnownModels =
    {
        ("ZKNS-001", 0x0F0D, 0x00C1, "One Handle MasCon for Nintendo Switch"),
        ("ZKNS-001b", 0x33DD, 0x0001, "One Handle MasCon for Nintendo Switch"),
        ("ZKNS-002", 0x33DD, 0x0002, "One Handle MasCon for Nintendo Switch"),
        ("ZKNS-011", 0x33DD, 0x0003, "One Handle MasCon for Nintendo Switch"),
        ("ZKNS-012", 0x33DD, 0x0004, "One Handle MasCon for Nintendo Switch"),
        ("ZKNS-013", 0x33DD, 0x0005, "One Handle MasCon for Nintendo Switch"),
    };

    /// <summary>Bit index of each button within the 14 bit field.</summary>
    public static readonly Dictionary<string, int> ButtonBits =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Y"] = 0, ["B"] = 1, ["A"] = 2, ["X"] = 3,
            ["L"] = 4, ["R"] = 5, ["ZL"] = 6, ["ZR"] = 7,
            ["Minus"] = 8, ["Plus"] = 9,
            ["Home"] = 12, ["Capture"] = 13,
        };

    public static byte[] BuildReport(byte notchValue, ushort buttons, byte hat)
    {
        return new byte[]
        {
            (byte)(buttons & 0xFF),
            (byte)((buttons >> 8) & 0x3F),
            (byte)(hat & 0x0F),
            0x80,
            notchValue,
            0x80,
            0x80,
            0x00,
        };
    }
}
