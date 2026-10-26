namespace LabelPrint;

/// <summary>frmReaderModel. The combo is the three models from the VB6 form. Pictures in the .frx are not ported.</summary>
internal sealed class ReaderModelForm : Form
{
    private readonly ICardReader _reader;
    private readonly ComboBox cboReaderModel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };

    public ReaderModelForm(ICardReader reader)
    {
        _reader = reader;
        Text = DeskMessages.ReaderTitle;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(300, 280);
        Font = new Font("Microsoft JhengHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point);

        var title = new Label
        {
            Text = "請選擇讀卡器",
            Dock = DockStyle.Top,
            Height = 48,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft JhengHei", 16F, FontStyle.Bold, GraphicsUnit.Point)
        };
        var frame = new GroupBox
        {
            Text = "讀卡器型號",
            Dock = DockStyle.Fill,
            Padding = new Padding(16)
        };
        foreach (var model in CardReaderModelNames.Known)
            cboReaderModel.Items.Add(model);
        var current = CardReaderModelNames.Normalize(_reader.Settings.Model);
        cboReaderModel.SelectedItem = current;
        cboReaderModel.Location = new Point(80, 80);
        frame.Controls.Add(cboReaderModel);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88, Height = 32 };
        var ok = new Button { Text = "確定", Width = 88, Height = 32 };
        ok.Click += (_, _) => Save();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        Controls.Add(frame);
        Controls.Add(buttons);
        Controls.Add(title);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void Save()
    {
        var model = cboReaderModel.SelectedItem as string ?? CardReaderModelNames.Rd600P;
        try
        {
            _reader.SaveSettings(new CardReaderSettings { Model = model });
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Ui.Warn(this, ex.Message, Text);
        }
    }
}
