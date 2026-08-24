using HIDMaestro;

namespace MasconBridge;

/// <summary>Wraps the virtual HID device created through HIDMaestro.</summary>
public sealed class VirtualMascon : IDisposable
{
    private readonly HMContext _ctx;
    private readonly HMController _ctrl;

    public VirtualMascon(ushort vid, ushort pid, string product)
    {
        _ctx = new HMContext();
        _ctx.LoadDefaultProfiles();

        // Installs and signs the UMDF2 driver on first use. Needs administrator
        // rights, but no reboot and no Windows test signing mode.
        _ctx.InstallDriver();

        var profile = new HMProfileBuilder()
            .Id("mascon-bridge")
            .Name("mascon-bridge")
            .Vendor("mascon-bridge")
            .Vid(vid)
            .Pid(pid)
            .ProductString(product)
            .Type("other")
            .Connection("usb")
            .DescriptorHex(Zuiki.DescriptorHex)
            .InputReportSize(Zuiki.InputReportSize)
            .Build();

        _ctrl = _ctx.CreateController(profile);
    }

    public void Submit(byte[] report) => _ctrl.SubmitRawReport(report);

    public void Dispose()
    {
        _ctrl.Dispose();
        _ctx.Dispose();
    }
}
