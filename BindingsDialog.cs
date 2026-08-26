using System.Drawing;

namespace MasconBridge;

/// <summary>
/// Where the mascon's buttons and hat are assigned. It is a window of its own because
/// fourteen rows would not fit beside the rest without pushing the control panel off
/// a 1080p screen, and because this is set up once and then forgotten.
/// </summary>
internal sealed class BindingsDialog : Form
{
    private sealed record GameItem(string Id, string Display)
    {
        public override string ToString() => Display;
    }

    private readonly List<ButtonBinding> _buttons;
    private readonly List<BindingRow> _rows = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 };

    private readonly ComboBox _cboGame = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _gameNote = new();

    private int _hatDeviceId;
    private string _game;

    public BindingsDialog(IEnumerable<ButtonBinding> buttons, int hatDeviceId, string? game)
    {
        // A copy: Cancel has to leave the configuration exactly as it was.
        _buttons = buttons.Select(b => new ButtonBinding
        {
            DeviceId = b.DeviceId, Button = b.Button, Mascon = b.Mascon,
        }).ToList();
        _hatDeviceId = hatDeviceId;
        _game = GameProfile.Normalise(game);

        BuildUi();
        SelectGame();
        foreach (var row in _rows) Refresh(row);

        _timer.Tick += (_, _) => { foreach (var row in _rows) row.CaptureWhileLearning(); };
        _timer.Start();
    }

    /// <summary>The bindings as they stand, once the dialog closes with OK.</summary>
    public List<ButtonBinding> Buttons => _buttons;

    public int HatDeviceId => _hatDeviceId;

    /// <summary>The game whose button names are being shown. Display only.</summary>
    public string Game => _game;

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

        rows.Controls.Add(Row(new BindingRow(Bindings.Hat, Strings.LabelHat, hat: true)));

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(LogicalToDeviceUnits(6)),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildIntro());
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

    /// <summary>
    /// What the page is for, on the left, and which game its captions come from, on
    /// the right. The picker sits up here rather than inside the card because the
    /// card is already as tall as a 1080p screen at 125% will take, and this corner
    /// was empty.
    /// </summary>
    private Control BuildIntro()
    {
        var intro = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(10)),
        };
        intro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        intro.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var hint = new Label
        {
            Text = Strings.DialogBindingsHint,
            AutoSize = true,
            MaximumSize = new Size(LogicalToDeviceUnits(430), 0),
            ForeColor = Theme.Muted,
            Margin = new Padding(3, LogicalToDeviceUnits(4), 3, 0),
        };

        _gameNote.Text = Strings.HintGameDefaults;
        _gameNote.AutoSize = true;
        _gameNote.Visible = false;
        _gameNote.MaximumSize = new Size(LogicalToDeviceUnits(430), 0);
        _gameNote.Font = Theme.Ui(8.25F);
        _gameNote.ForeColor = Theme.Muted;
        _gameNote.Margin = new Padding(3, LogicalToDeviceUnits(6), 3, 0);

        intro.Controls.Add(hint, 0, 0);
        intro.Controls.Add(BuildGamePicker(), 1, 0);
        intro.Controls.Add(_gameNote, 0, 1);
        intro.SetColumnSpan(_gameNote, 2);
        return intro;
    }

    private Control BuildGamePicker()
    {
        foreach (var id in GameProfile.Supported)
            _cboGame.Items.Add(new GameItem(id, GameProfile.DisplayName(id)));

        // Wide enough for the longest name in whichever language is running, rather
        // than a number that fits the English and clips the Japanese.
        int widest = GameProfile.Supported
            .Max(id => Theme.Measure(GameProfile.DisplayName(id), Font).Width);
        _cboGame.Width = widest + LogicalToDeviceUnits(36);
        _cboGame.Margin = new Padding(0);
        _cboGame.SelectedIndexChanged += OnGameChanged;

        var caption = new Label
        {
            Text = Strings.LabelGame,
            AutoSize = true,
            ForeColor = Theme.Ink,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, LogicalToDeviceUnits(4), LogicalToDeviceUnits(8), 3),
        };

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(LogicalToDeviceUnits(16), 0, 0, 0),
        };
        row.Controls.Add(caption);
        row.Controls.Add(_cboGame);
        return row;
    }

    /// <summary>
    /// Puts the list on the current game, which is what puts its names on the rows.
    /// A game that is not in the list leaves the page bare, which is the same page
    /// as no game at all.
    /// </summary>
    private void SelectGame()
    {
        for (int i = 0; i < _cboGame.Items.Count; i++)
            if (_cboGame.Items[i] is GameItem g && g.Id == _game)
            {
                _cboGame.SelectedIndex = i;
                return;
            }
    }

    /// <summary>
    /// Puts the chosen game's names on the rows. The column is as wide as the widest
    /// caption and no wider, and it disappears altogether when no game is chosen, so
    /// the page is exactly what it was before.
    /// </summary>
    private void ApplyGame()
    {
        var captions = _rows
            .Select(row => (Row: row, Caption: GameProfile.FunctionOf(_game, row.Mascon)))
            .ToList();

        int widest = captions
            .Where(c => c.Caption is not null)
            .Select(c => Theme.Measure(c.Caption!.Value.Text, Font).Width)
            .DefaultIfEmpty(0)
            .Max();

        foreach (var (row, caption) in captions) row.ShowFunction(caption, widest);

        _gameNote.Visible = widest > 0;
    }

    private void OnGameChanged(object? sender, EventArgs e)
    {
        if (_cboGame.SelectedItem is not GameItem chosen) return;

        _game = chosen.Id;

        // Nothing to put names on until the rows exist. Building the list does not
        // pick anything, so in practice this only fires once they do.
        if (_rows.Count > 0) ApplyGame();
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

        if (row.Mascon == Bindings.Hat) _hatDeviceId = hit.DeviceId;
        else Bindings.Add(_buttons, row.Mascon, hit.DeviceId, hit.Button);

        Refresh(row);
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        if (sender is not BindingRow row) return;

        if (row.Mascon == Bindings.Hat) _hatDeviceId = -1;
        else Bindings.Clear(_buttons, row.Mascon);

        Refresh(row);
    }

    private void Refresh(BindingRow row)
    {
        if (row.Mascon == Bindings.Hat)
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
