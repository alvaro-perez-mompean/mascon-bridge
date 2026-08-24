namespace MasconBridge;

/// <summary>
/// winmm numbers joysticks by position, not by identity. Plugging or unplugging any
/// USB device renumbers them, and a configuration that names joystick 2 then reads
/// somebody else's axis without saying a word — the handle simply stops responding,
/// or worse, responds to the wrong lever.
///
/// So the configuration also remembers what each number was, by vendor and product
/// id, and this puts the numbers back where they belong before anything is read.
/// </summary>
public static class DeviceMap
{
    /// <summary>How a device is written down in the configuration: "044F:B687".</summary>
    public static string Identity(ushort vid, ushort pid) => $"{vid:X4}:{pid:X4}";

    /// <summary>A remembered device that is now at a different number.</summary>
    public readonly record struct Move(int From, int To, string Identity);

    /// <summary>A remembered device that is not connected any more.</summary>
    public readonly record struct Gone(int Id, string Identity);

    /// <summary>What has to change, and what to tell the user about it.</summary>
    public sealed class Plan
    {
        public Dictionary<int, int> Remap { get; } = new();
        public List<Move> Moves { get; } = new();
        public List<Gone> Missing { get; } = new();

        public bool Anything => Moves.Count > 0 || Missing.Count > 0;

        /// <summary>The number a device carries now. Untouched ids pass through.</summary>
        public int Apply(int id) => Remap.TryGetValue(id, out var to) ? to : id;
    }

    /// <summary>
    /// Works out where each remembered device went. A device that is still at its own
    /// number keeps it — that claim comes first, so two identical devices cannot swap
    /// places just because one of them was looked at earlier.
    /// </summary>
    public static Plan Match(
        IReadOnlyDictionary<int, string> remembered,
        IReadOnlyDictionary<int, string> present)
    {
        var plan = new Plan();
        var claimed = new HashSet<int>();
        var pending = new List<KeyValuePair<int, string>>();

        foreach (var entry in remembered.OrderBy(e => e.Key))
        {
            if (present.TryGetValue(entry.Key, out var here) && here == entry.Value)
                claimed.Add(entry.Key);
            else
                pending.Add(entry);
        }

        foreach (var (id, identity) in pending)
        {
            int found = -1;
            foreach (var candidate in present.Where(p => p.Value == identity)
                                             .Select(p => p.Key)
                                             .OrderBy(k => k))
            {
                if (claimed.Add(candidate)) { found = candidate; break; }
            }

            if (found < 0) plan.Missing.Add(new Gone(id, identity));
            else
            {
                plan.Remap[id] = found;
                plan.Moves.Add(new Move(id, found, identity));
            }
        }

        return plan;
    }

    /// <summary>The joysticks attached right now, by number.</summary>
    public static Dictionary<int, string> Present()
    {
        var map = new Dictionary<int, string>();
        foreach (var (id, caps) in Joystick.Enumerate())
            map[id] = Identity(caps.wMid, caps.wPid);
        return map;
    }
}
