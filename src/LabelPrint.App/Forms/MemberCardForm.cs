using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelPrint;

/// <summary>
/// Legacy frmMemberCard. Reads and writes the member number on an SLE4428/4442 card.
/// The card workflow that lived in modMemberCard is <see cref="ICardReader"/> plus <see cref="CardCodec"/>.
/// </summary>
internal sealed class MemberCardForm : Form
{
    private readonly DeskSession _session;
    private readonly ICardReader _reader;
    private readonly INameplatePrintPipeline _pipeline;
    private readonly IOptions<LabelPrintOptions> _options;
    private readonly ILogger<MemberCardForm> _logger;
    private readonly TextBox txtLeag_No = new() { Dock = DockStyle.Fill };
    private readonly TextBox txtLeag_Name = new() { Dock = DockStyle.Fill };
    private readonly TextBox txtCardNo = new() { Dock = DockStyle.Fill };
    private readonly TextBox txtMemberCode = new() { Dock = DockStyle.Fill };
    private readonly TextBox txtValiddate = new() { Dock = DockStyle.Fill };
    private readonly TextBox txtTempMem = new() { Dock = DockStyle.Fill };
    private readonly TextBox _hex;
    private bool _busy;

    public MemberCardForm(
        DeskSession session,
        ICardReader reader,
        INameplatePrintPipeline pipeline,
        IOptions<LabelPrintOptions> options,
        ILogger<MemberCardForm> logger)
    {
        _session = session;
        _reader = reader;
        _pipeline = pipeline;
        _options = options;
        _logger = logger;
        _hex = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
            Text = "Card memory appears here after Read or Dump."
        };

        Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Member card";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(640, 480);

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(12, 12, 12, 0) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var row = 0; row < 3; row++)
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Add(fields, 0, "Member no", txtLeag_No, "Name", txtLeag_Name);
        Add(fields, 1, "Card no", txtCardNo, "Member code", txtMemberCode);
        Add(fields, 2, "Valid date", txtValiddate, "Temp member", txtTempMem);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 8, 12, 8) };
        buttons.Controls.Add(Action("Read card", async () => await ReadAsync(fullDump: false)));
        buttons.Controls.Add(Action("Dump main memory", async () => await ReadAsync(fullDump: true)));
        buttons.Controls.Add(Action("Write member number", WriteAsync));
        buttons.Controls.Add(Action("Print", () => Print(preview: false)));
        buttons.Controls.Add(Action("Preview", () => Print(preview: true)));
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
        buttons.Controls.Add(close);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12, 0, 12, 8),
            MaximumSize = new Size(740, 0),
            Text = "TODO(on-machine): member-number offset, PIN, and IC_D600 signatures need a real card and dumpbin /exports before relying on Write. Probe does not send a PIN. A wrong SLE4442 PIN can lock the card."
        };

        Controls.Add(_hex);
        Controls.Add(hint);
        Controls.Add(buttons);
        Controls.Add(fields);
        CancelButton = close;
        LoadFields();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveFields();
        base.OnFormClosed(e);
    }

    private void LoadFields()
    {
        txtLeag_No.Text = _session.Member.Number;
        txtLeag_Name.Text = _session.Member.Name;
        txtCardNo.Text = _session.Member.CardNumber;
        txtMemberCode.Text = _session.Member.MemberCode;
        txtValiddate.Text = _session.Member.ValidDate;
        txtTempMem.Text = _session.Member.TempMember;
    }

    private void SaveFields()
    {
        _session.Member.Number = txtLeag_No.Text.Trim();
        _session.Member.Name = txtLeag_Name.Text.Trim();
        _session.Member.CardNumber = txtCardNo.Text.Trim();
        _session.Member.MemberCode = txtMemberCode.Text.Trim();
        _session.Member.ValidDate = txtValiddate.Text.Trim();
        _session.Member.TempMember = txtTempMem.Text.Trim();
    }

    private async Task ReadAsync(bool fullDump)
    {
        if (_busy)
            return;
        _busy = true;
        UseWaitCursor = true;
        try
        {
            var memory = await Task.Run(() => fullDump
                ? _reader.ReadRange(0, CardCodec.Capacity(_reader.Settings.CardType))
                : _reader.ReadMemberArea());
            _hex.Text = $"Offset {memory.Offset}, {memory.Bytes.Length} bytes{Environment.NewLine}{memory.ToHex()}";
            if (!string.IsNullOrWhiteSpace(memory.MemberNumber))
            {
                txtCardNo.Text = memory.MemberNumber;
                txtLeag_No.Text = memory.MemberNumber;
            }

            _logger.LogInformation("Member card read {Length} bytes at offset {Offset}", memory.Bytes.Length, memory.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Member card read failed");
            Ui.Error(this, ex.Message);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private async Task WriteAsync()
    {
        if (_busy)
            return;

        var number = txtLeag_No.Text.Trim();
        if (number.Length == 0)
        {
            Ui.Info(this, "Enter the member number to write.");
            return;
        }

        var access = _options.Value.Card;
        var message =
            $"Write member number {number} to the card at offset {access.MemberNumberOffset} ({access.MemberNumberLength} bytes)?" +
            Environment.NewLine + Environment.NewLine +
            "This changes the card. TODO(on-machine): confirm the offset against a dumped Jumping Gym card first.";
        if (!Ui.Confirm(this, message))
            return;

        _busy = true;
        UseWaitCursor = true;
        try
        {
            await Task.Run(() => _reader.WriteMemberNumber(number));
            _logger.LogInformation("Wrote member number {Number} to the card", number);
            Ui.Info(this, "Member number written.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Member card write failed");
            Ui.Error(this, ex.Message);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private void Print(bool preview)
    {
        try
        {
            SaveFields();
            if (string.IsNullOrWhiteSpace(_session.Member.Name))
            {
                Ui.Info(this, "Enter the member name before printing.");
                return;
            }

            var job = _pipeline.Create(_session.MemberTemplate, _session.Member.ToBinding());
            if (job.UsedFallbackLayout && !Ui.Confirm(this, job.Template.Message + Environment.NewLine + Environment.NewLine + "Print the built-in layout?"))
                return;

            using var bitmap = NameplateRenderer.Render(job.Layout, job.Binding);
            if (preview)
                NameplatePrinter.Preview(this, bitmap, job.Layout, job.DocumentName, _options.Value.PrinterName);
            else
                NameplatePrinter.Print(bitmap, job.Layout, job.DocumentName, job.Copies, _options.Value.PrinterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Member card print failed");
            Ui.Error(this, ex.Message);
        }
    }

    private Button Action(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        button.Click += async (_, _) => await action();
        return button;
    }

    private Button Action(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        button.Click += (_, _) => action();
        return button;
    }

    private static void Add(TableLayoutPanel grid, int row, string leftCaption, Control left, string rightCaption, Control right)
    {
        grid.Controls.Add(new Label { Text = leftCaption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) }, 0, row);
        left.Margin = new Padding(0, 4, 12, 4);
        grid.Controls.Add(left, 1, row);
        grid.Controls.Add(new Label { Text = rightCaption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) }, 2, row);
        right.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(right, 3, row);
    }
}
