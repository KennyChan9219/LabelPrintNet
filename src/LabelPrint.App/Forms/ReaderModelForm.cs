using LabelPrint.CardReader;
using Microsoft.Extensions.Logging;

namespace LabelPrint;

/// <summary>Legacy frmReaderModel. Picks the ACS CCID reader model and port stored in CardReader.config.</summary>
internal sealed class ReaderModelForm : Form
{
    private readonly ICardReader _reader;
    private readonly ILogger<ReaderModelForm> _logger;
    private readonly ComboBox cboReaderModel = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly NumericUpDown _port = new() { Minimum = 0, Maximum = 32767, Dock = DockStyle.Left, Width = 120 };
    private readonly ComboBox _cardType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox _initTypeEnabled = new() { Text = "Call IC_InitType", AutoSize = true };
    private readonly NumericUpDown _initType = new() { Minimum = 0, Maximum = 255, Dock = DockStyle.Left, Width = 80, Enabled = false };
    private readonly Label _result = new() { AutoSize = true, MaximumSize = new Size(460, 0), Padding = new Padding(0, 8, 0, 0) };

    public ReaderModelForm(ICardReader reader, ILogger<ReaderModelForm> logger)
    {
        _reader = reader;
        _logger = logger;
        Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Reader model";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(520, 390);

        _reader.Reload();
        _cardType.Items.AddRange(["SLE4442", "SLE4428"]);
        FillModels(Array.Empty<string>());
        cboReaderModel.Text = _reader.Settings.Model;
        _port.Value = Math.Clamp(_reader.Settings.Port, 0, 32767);
        _cardType.SelectedItem = CardReaderConfigStore.ToConfig(_reader.Settings.CardType);
        if (_reader.Settings.InitType is int type)
        {
            _initTypeEnabled.Checked = true;
            _initType.Value = Math.Clamp(type, 0, 255);
            _initType.Enabled = true;
        }

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            RowCount = 8
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 8; row++)
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var refresh = new Button { Text = "Refresh PC/SC readers", AutoSize = true };
        refresh.Click += (_, _) => RefreshReaders();
        var probe = new Button { Text = "Probe", AutoSize = true };
        probe.Click += (_, _) => Probe();
        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => Save();
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(refresh);
        buttons.Controls.Add(probe);
        buttons.Controls.Add(save);
        buttons.Controls.Add(close);

        _initTypeEnabled.CheckedChanged += (_, _) => _initType.Enabled = _initTypeEnabled.Checked;

        Add(grid, 0, "Model", cboReaderModel);
        Add(grid, 1, "Port", _port);
        Add(grid, 2, "Card type", _cardType);
        grid.Controls.Add(_initTypeEnabled, 0, 3);
        grid.Controls.Add(_initType, 1, 3);
        grid.Controls.Add(buttons, 0, 4);
        grid.SetColumnSpan(buttons, 2);
        var dll = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Text = "IC_D600.dll: " + (_reader.NativeLibraryPresent ? _reader.NativeLibraryPath : "missing, copy it beside the exe")
                + Environment.NewLine
                + "acsr2032.dll: " + (_reader.DependencyLibraryPresent ? _reader.DependencyLibraryPath : "missing, copy it beside the exe")
                + Environment.NewLine
                + "Config: " + _reader.SettingsPath
        };
        grid.Controls.Add(dll, 0, 5);
        grid.SetColumnSpan(dll, 2);
        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Padding = new Padding(0, 8, 0, 0),
            Text = "Port 100 is the common USB convention and is unconfirmed for this DLL. TODO(on-machine): pick the model Windows shows for the ACS CCID reader, probe it, then read a spare card before writing."
        };
        grid.Controls.Add(note, 0, 6);
        grid.SetColumnSpan(note, 2);
        grid.Controls.Add(_result, 0, 7);
        grid.SetColumnSpan(_result, 2);

        Controls.Add(grid);
        CancelButton = close;
        AcceptButton = save;
    }

    private void FillModels(IReadOnlyList<string> connected)
    {
        var selected = cboReaderModel.Text;
        cboReaderModel.Items.Clear();
        foreach (var model in _reader.KnownModels.Concat(connected).Distinct(StringComparer.OrdinalIgnoreCase))
            cboReaderModel.Items.Add(model);
        if (!string.IsNullOrWhiteSpace(selected))
            cboReaderModel.Text = selected;
    }

    private void RefreshReaders()
    {
        try
        {
            var readers = _reader.ListConnectedReaders();
            FillModels(readers);
            _result.Text = readers.Count == 0
                ? "PC/SC reported no readers. On Windows this lists ACS CCID names such as \"ACS CCID USB Reader 0\"."
                : "PC/SC readers: " + string.Join(", ", readers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PC/SC reader list failed");
            Ui.Error(this, ex.Message);
        }
    }

    private void Probe()
    {
        try
        {
            var result = _reader.Probe();
            _result.Text = result.Message;
            _logger.LogInformation("Reader probe success={Success} code={Code}", result.Success, result.Code);
            if (!result.Success)
                Ui.Error(this, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reader probe failed");
            Ui.Error(this, ex.Message);
        }
    }

    private void Save()
    {
        try
        {
            var settings = new CardReaderSettings
            {
                Model = cboReaderModel.Text.Trim(),
                Port = (int)_port.Value,
                CardType = _cardType.SelectedItem is "SLE4428" ? CardType.Sle4428 : CardType.Sle4442,
                InitType = _initTypeEnabled.Checked ? (int)_initType.Value : null
            };
            _reader.SaveSettings(settings);
            _result.Text = "Saved " + _reader.SettingsPath;
            Ui.Info(this, "Reader settings saved.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving CardReader.config failed");
            Ui.Error(this, ex.Message);
        }
    }

    private static void Add(TableLayoutPanel grid, int row, string caption, Control control)
    {
        grid.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) }, 0, row);
        control.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(control, 1, row);
    }
}
