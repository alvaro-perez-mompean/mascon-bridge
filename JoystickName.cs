using Microsoft.Win32;

namespace MasconBridge;

/// <summary>
/// Windows caches the name of every joystick by vendor and product id, and the Game
/// Controllers panel reads that cache rather than asking the device. The cache
/// outlives the device, so a virtual mascon that changes its name keeps showing the
/// old one -- there is nothing wrong with the device, the panel is quoting itself.
///
/// The copy that matters lives in HKCU. There is one in HKLM too, and it is the one
/// that updates on its own; the per user copy is the one the panel prefers and the
/// one that goes stale.
/// </summary>
public static class JoystickName
{
    private const string Root =
        @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM";

    /// <summary>The per device subkey, e.g. "VID_33DD&amp;PID_0002".</summary>
    public static string KeyName(ushort vid, ushort pid) => $"VID_{vid:X4}&PID_{pid:X4}";

    /// <summary>Where that subkey lives, under HKEY_CURRENT_USER.</summary>
    public static string KeyPath(ushort vid, ushort pid) => $@"{Root}\{KeyName(vid, pid)}";

    /// <summary>
    /// Whether the cached name has to go. A cache that agrees is left alone: deleting
    /// and letting Windows write it again would be churn for nothing, and the entry
    /// may belong to a real mascon rather than to us.
    /// </summary>
    public static bool IsStale(string? cached, string expected)
        => cached is not null && !string.Equals(cached, expected, StringComparison.Ordinal);

    /// <summary>
    /// Drops the cached name when it no longer matches, so Windows writes the current
    /// one as the device is enumerated. Call before creating the device.
    ///
    /// Deleting rather than overwriting on purpose: whatever appears next comes from
    /// whichever device actually enumerates, which is the honest answer if a real
    /// mascon is plugged in beside this one.
    /// </summary>
    public static bool ClearIfStale(ushort vid, ushort pid, string expected)
    {
        try
        {
            using var oem = Registry.CurrentUser.OpenSubKey(Root, writable: true);
            if (oem is null) return false;

            string name = KeyName(vid, pid);
            using (var device = oem.OpenSubKey(name))
            {
                if (device is null) return false;
                if (!IsStale(device.GetValue("OEMName") as string, expected)) return false;
            }

            oem.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (System.Security.SecurityException) { return false; }
        catch (IOException) { return false; }
    }
}
