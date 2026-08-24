using System.Drawing;

namespace MasconBridge;

/// <summary>
/// Where the mascon's buttons and hat are assigned. It is a window of its own because
/// fourteen rows would not fit beside the rest without pushing the control panel off
/// a 1080p screen, and because this is set up once and then forgotten.
/// </summary>
internal sealed class BindingsDialog : Form
{
    private const string HatKey = "Hat";

    private readonly List<ButtonBinding> _buttons;
    private readonly List<BindingRow> _rows = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 };

    private int _hatDeviceId;

    public BindingsDialog(IEnumerable<ButtonBinding> buttons, int hatDeviceId)
    {
        // A copy: Cancel has to leave the configuration exactly as it was.
        _buttons = buttons.Select(b => new ButtonBinding
        {
            DeviceId = b.DeviceId, Button = b.Button, Mascon = b.Mascon,
        }).ToList();
        _hatDeviceId = hatDeviceId;

        BuildUi();
        foreach (var row in _rows) Refresh(row);

        _timer.Tick += (_, _) => { foreach (var row in _rows) row.CaptureWhileLearning(); };
        _timer.Start();
    }

    /// <summary>The bindings as they stand, once the dialog closes with OK.</summary>
    public List<ButtonBinding> Buttons => _buttons;

    public int HatDeviceId => _hatDeviceId;

    private void BuildUi()
    {
        Text = Strings.DialogBindingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = Theme.Ui(9F);
        BackColor = Theme.Page;
        ForeColor = Theme.Ink;

        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { /* the dialog just keeps the default icon */ }

        var rows = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var mascon in Bindings.Order)
            rows.Controls.Add(Row(new BindingRow(mascon, mascon)));

        rows.Controls.Add(new Label
        {
            Text = Strings.HintEmergencyButton,
            AutoSize = true,
            Font = Theme.Ui(8.25F),
            ForeColor = Theme.Muted,
            Margin = new Padding(3, 0, 3, LogicalToDeviceUnits(10)),
        });

        rows.Controls.Add(Row(new BindingRow(HatKey, Strings.LabelHat, hat: true)));

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(LogicalToDeviceUnits(6)),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(new Label
        {
            Text = Strings.DialogBindingsHint,
            AutoSize = true,
            MaximumSize = new Size(LogicalToDeviceUnits(430), 0),
            ForeColor = Theme.Muted,
            Margin = new Padding(3, LogicalToDeviceUnits(4), 3, LogicalToDeviceUnits(10)),
        });
        root.Controls.Add(new Card(Strings.DialogBindingsTitle, rows));
        root.Controls.Add(BuildActions());

        Controls.Add(root);
    }

    private BindingRow Row(BindingRow row)
    {
        row.Captured += OnCaptured;
        row.Cleared += OnCleared;
        row.LearningStarted += OnLearningStarted;
        _rows.Add(row);
        return row;
    }

    private Control BuildActions()
    {
        var ok = new FlatButton(FlatButton.Look.Primary)
        {
            Text = Strings.ButtonDialogOk, DialogResult = DialogResult.OK,
        };
        var cancel = new FlatButton(FlatButton.Look.Plain)
        {
            Text = Strings.ButtonDialogCancel, DialogResult = DialogResult.Cancel,
        };
        cancel.Margin = new Padding(LogicalToDeviceUnits(8), 0, 0, 0);

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, LogicalToDeviceUnits(10), 0, 0),
            WrapContents = false,
        };
        row.Controls.Add(ok);
        row.Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
        return row;
    }

    // -------------------------------------------------------------------------
    private void OnLearningStarted(object? sender, EventArgs e)
    {
        // Only one row listens at a time, or one press would land in several.
        foreach (var row in _rows)
            if (!ReferenceEquals(row, sender)) row.StopLearning();
    }

    private void OnCaptured(object? sender, (int DeviceId, int Button) hit)
    {
        if (sender is not BindingRow row) return;

        if (row.Mascon == HatKey) _hatDeviceId = hit.DeviceId;
        else Bindings.Add(_buttons, row.Mascon, hit.DeviceId, hit.Button);

        Refresh(row);
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        if (sender is not BindingRow row) return;

        if (row.Mascon == HatKey) _hatDeviceId = -1;
        else Bindings.Clear(_buttons, row.Mascon);

        Refresh(row);
    }

    private void Refresh(BindingRow row)
    {
        if (row.Mascon == HatKey)
        {
            row.Show(_hatDeviceId >= 0
                ? string.Format(Strings.HintHatBinding, _hatDeviceId)
                : null);
            return;
        }

        var bound = Bindings.For(_buttons, row.Mascon)
            .Select(b => string.Format(Strings.HintReleaseBinding, b.DeviceId, b.Button));
        row.Show(string.Join("   ", bound));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }
}
