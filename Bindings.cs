namespace MasconBridge;

/// <summary>
/// What is bound to each mascon button. The list in the configuration is a flat one,
/// physical button to mascon button, and it is a many to one relationship on purpose:
/// the same mascon button can be reachable from the stick and from the throttle, and
/// EB in particular is worth having under more than one thumb.
///
/// These are the operations the window needs, kept out of it so they can be tested
/// without opening a window.
/// </summary>
public static class Bindings
{
    /// <summary>
    /// The mascon buttons, in the order the window lists them. EB is not one of the
    /// device's buttons — it is the emergency notch, reachable as a button — so it
    /// comes last, after the twelve that are.
    /// </summary>
    public static readonly string[] Order =
    {
        "Y", "B", "A", "X", "L", "R", "ZL", "ZR", "Minus", "Plus", "Home", "Capture", "EB",
    };

    public const string Emergency = "EB";

    private static bool Same(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Everything bound to one mascon button, in the order it was added.</summary>
    public static List<ButtonBinding> For(IEnumerable<ButtonBinding> all, string mascon)
        => all.Where(b => Same(b.Mascon, mascon)).ToList();

    /// <summary>
    /// Adds a physical button. Pressing the same one twice is not an error and not a
    /// duplicate either: it simply leaves the binding as it was.
    /// </summary>
    public static bool Add(List<ButtonBinding> all, string mascon, int deviceId, int button)
    {
        if (deviceId < 0 || button < 1) return false;

        if (all.Any(b => Same(b.Mascon, mascon) && b.DeviceId == deviceId && b.Button == button))
            return false;

        all.Add(new ButtonBinding { DeviceId = deviceId, Button = button, Mascon = mascon });
        return true;
    }

    /// <summary>Drops every physical button bound to one mascon button.</summary>
    public static int Clear(List<ButtonBinding> all, string mascon)
        => all.RemoveAll(b => Same(b.Mascon, mascon));

    /// <summary>
    /// Bindings that name a mascon button this program does not have. They are left
    /// alone rather than tidied away — a configuration edited by hand may be ahead of
    /// the window, and silently dropping somebody's work is worse than showing less.
    /// </summary>
    public static List<ButtonBinding> Unknown(IEnumerable<ButtonBinding> all)
        => all.Where(b => !Order.Any(o => Same(o, b.Mascon))).ToList();
}
