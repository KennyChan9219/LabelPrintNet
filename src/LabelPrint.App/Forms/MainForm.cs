using LabelPrint.Data;
using LabelPrint.Printing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelPrint;

/// <summary>
/// Startup form. CardPrint4.vbp sets Startup = frmMemberCard. Employee badges are the same form,
/// shown when 員工 - 工作証 is selected. There is no separate Form1.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly ILeagerRepository _leager;
    private readonly IMtUserRepository _employees;
    private readonly MisConnectionFactory _database;
    private readonly ILabelTemplateCatalog _catalog;
    private readonly INameplatePrintPipeline _pipeline;
    private readonly ICardReader _reader;
    private readonly IOptions<LabelPrintOptions> _options;
    private readonly ILogger<MainForm> _logger;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };

    private readonly RadioButton optMember = new() { Text = "一般會員", AutoSize = true };
    private readonly RadioButton optTrial = new() { Text = "試玩會員", AutoSize = true };
    private readonly RadioButton optEmployee = new() { Text = "員工 - 工作証", AutoSize = true };
    private readonly RadioButton optGoodLuck = new() { Text = "Good Luck 卡", AutoSize = true };

    private readonly Label txtCardNo = FieldLabel();
    private readonly Label txtMemberCode = FieldLabel();
    private readonly Label txtLeag_Name = FieldLabel();
    private readonly Label txtValiddate = FieldLabel();
    private readonly Label txtGoodLuckDate = FieldLabel();
    private readonly TextBox txtTempMem0 = new() { Width = 280 };
    private readonly TextBox txtTempMem1 = new() { Width = 280 };
    private readonly TextBox txtTempMem2 = new() { Width = 280, Text = LabelChoice.TrialDateTemplate };
    private readonly Button cmdRead = new() { Text = "ＩＣ咭編號：", AutoSize = true, Width = 120 };
    private readonly Button cmdPrint = new() { Text = "列印 (&P)", Width = 96, Height = 32, Enabled = false };
    private readonly Button cmdCancel = new() { Text = "離開 (&E)", Width = 96, Height = 32 };

    private readonly Panel picLabel1 = new() { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Width = 280, Height = 78 };
    private readonly Panel picLabel2 = new() { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Width = 280, Height = 78 };
    private readonly Label lblMemID = PreviewLabel();
    private readonly Label lblMemName = PreviewLabel();
    private readonly Label lblValidUntil = PreviewLabel();
    private readonly Label lblValidDate = PreviewLabel();
    private readonly Label lblMemID2 = PreviewLabel();
    private readonly Label lblMemName2 = PreviewLabel();
    private readonly Label lblValidUntil2 = PreviewLabel();
    private readonly Label lblValidDate2 = PreviewLabel();

    private readonly Panel fraMemCard = new() { Dock = DockStyle.Fill };
    private readonly Panel fraEmpCard = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly TextBox txtCardNum = new() { Width = 180 };
    private readonly TextBox txtStaffNo = new() { Width = 280 };
    private readonly TextBox txtEmpName = new() { Width = 360 };
    private readonly Button cmdPrintEmp = new() { Text = "列印", Width = 96, Height = 32 };

    private readonly ToolStripStatusLabel _branch = new("分店");
    private readonly ToolStripStatusLabel _databaseName = new("未設定") { AutoSize = false, Width = 90 };
    private readonly ToolStripStatusLabel _readerStatus = new("") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _readerModel = new("讀卡器型號: RD600P") { AutoSize = false, Width = 180 };

    private bool _busy;
    private string _transferCard = "";

    public MainForm(
        ILeagerRepository leager,
        IMtUserRepository employees,
        MisConnectionFactory database,
        ILabelTemplateCatalog catalog,
        INameplatePrintPipeline pipeline,
        ICardReader reader,
        IOptions<LabelPrintOptions> options,
        ILogger<MainForm> logger)
    {
        _leager = leager;
        _employees = employees;
        _database = database;
        _catalog = catalog;
        _pipeline = pipeline;
        _reader = reader;
        _options = options;
        _logger = logger;

        Text = DeskMessages.AppTitle;
        Font = new Font("Microsoft JhengHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 460);

        var menu = new MenuStrip();
        var readerMenu = new ToolStripMenuItem("選擇讀卡器");
        readerMenu.Click += (_, _) => OpenReader();
        menu.Items.Add(readerMenu);
        MainMenuStrip = menu;

        var modes = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 4),
            WrapContents = false
        };
        modes.Controls.Add(new Label { Text = "列印 :", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
        modes.Controls.Add(optMember);
        modes.Controls.Add(optTrial);
        modes.Controls.Add(optEmployee);
        modes.Controls.Add(optGoodLuck);

        BuildMember();
        BuildEmployee();
        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(fraEmpCard);
        body.Controls.Add(fraMemCard);

        var status = new StatusStrip();
        status.Items.AddRange([_branch, _databaseName, _readerStatus, _readerModel]);

        Controls.Add(body);
        Controls.Add(modes);
        Controls.Add(status);
        Controls.Add(menu);

        optMember.CheckedChanged += (_, _) => ApplyMode();
        optTrial.CheckedChanged += (_, _) => ApplyMode();
        optEmployee.CheckedChanged += (_, _) => ApplyMode();
        optGoodLuck.CheckedChanged += (_, _) => ApplyMode();
        cmdRead.Click += async (_, _) =>
        {
            ClearMember();
            await ReadCardAsync();
        };
        cmdPrint.Click += (_, _) => PrintMember();
        cmdCancel.Click += (_, _) => Close();
        txtTempMem0.TextChanged += (_, _) => UpdatePreviewTrial();
        txtTempMem1.TextChanged += (_, _) => UpdatePreviewTrial();
        txtTempMem2.TextChanged += (_, _) => UpdatePreviewTrial();
        txtTempMem2.Enter += (_, _) => txtTempMem2.Select(2, 2);
        txtCardNum.KeyDown += async (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                args.SuppressKeyPress = true;
                await LookupEmployeeAsync();
            }
        };

        _timer.Tick += async (_, _) =>
        {
            if (_busy || _transferCard.Length == 0)
                return;
            _timer.Stop();
            try
            {
                await ReadCardAsync();
            }
            finally
            {
                if (!IsDisposed)
                    _timer.Start();
            }
        };

        optMember.Checked = true;
        ApplyMode();
        ClearMember();
        RefreshChrome();
        _timer.Start();
    }

    private void BuildMember()
    {
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(16, 8, 16, 0)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddMemberRow(fields, 0, cmdRead, txtCardNo);
        AddMemberRow(fields, 1, Caption("　會員編號："), txtMemberCode);
        AddMemberRow(fields, 2, Caption("　會員名稱："), txtLeag_Name);
        var value = new Panel { Height = 28, Width = 400 };
        txtValiddate.Dock = DockStyle.Fill;
        txtGoodLuckDate.Dock = DockStyle.Fill;
        txtGoodLuckDate.Text = LabelChoice.GoodLuckFormDate;
        txtGoodLuckDate.Visible = false;
        value.Controls.Add(txtGoodLuckDate);
        value.Controls.Add(txtValiddate);
        AddMemberRow(fields, 3, Caption("有效日期至："), value);

        var trial = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(16, 0, 16, 0),
            Visible = false,
            Name = "trial"
        };
        trial.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        trial.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddMemberRow(trial, 0, Caption("請輸入會員編號"), txtTempMem0);
        AddMemberRow(trial, 1, Caption("請輸入會員名稱"), txtTempMem1);
        AddMemberRow(trial, 2, Caption("有效日期至："), txtTempMem2);

        FillPreview(picLabel1, lblMemID, lblMemName, lblValidUntil, lblValidDate);
        FillPreview(picLabel2, lblMemID2, lblMemName2, lblValidUntil2, lblValidDate2);
        picLabel2.Visible = false;
        var preview = new GroupBox
        {
            Text = "預覽",
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(8)
        };
        var pics = new FlowLayoutPanel { Dock = DockStyle.Fill };
        pics.Controls.Add(picLabel1);
        pics.Controls.Add(picLabel2);
        preview.Controls.Add(pics);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8)
        };
        buttons.Controls.Add(cmdPrint);
        buttons.Controls.Add(cmdCancel);

        fraMemCard.Controls.Add(buttons);
        fraMemCard.Controls.Add(preview);
        fraMemCard.Controls.Add(trial);
        fraMemCard.Controls.Add(fields);
    }

    private void BuildEmployee()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(16, 12, 16, 0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var title = new Label
        {
            Text = "列印工作証Label",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };
        grid.Controls.Add(title, 0, 0);
        grid.SetColumnSpan(title, 3);
        var lookup = new Button { Text = "確定", Width = 88, Height = 32 };
        lookup.Click += async (_, _) => await LookupEmployeeAsync();
        var cardLine = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        var hint = new Label { Text = "(請拍卡/輸入卡背號碼)", AutoSize = true, Margin = new Padding(8, 8, 0, 0) };
        cardLine.Controls.Add(txtCardNum);
        cardLine.Controls.Add(lookup);
        cardLine.Controls.Add(hint);
        grid.Controls.Add(new Label { Text = "卡號碼", AutoSize = true, Margin = new Padding(0, 8, 8, 4) }, 0, 1);
        grid.Controls.Add(cardLine, 1, 1);
        grid.SetColumnSpan(cardLine, 2);
        grid.Controls.Add(new Label { Text = "員工編號", AutoSize = true, Margin = new Padding(0, 8, 8, 4) }, 0, 2);
        grid.Controls.Add(txtStaffNo, 1, 2);
        grid.Controls.Add(new Label { Text = "員工名稱", AutoSize = true, Margin = new Padding(0, 8, 8, 4) }, 0, 3);
        grid.Controls.Add(txtEmpName, 1, 3);
        grid.SetColumnSpan(txtEmpName, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 16, 16, 8) };
        var leave = new Button { Text = "離開", Width = 96, Height = 32 };
        leave.Click += (_, _) => Close();
        cmdPrintEmp.Click += (_, _) => PrintEmployee();
        buttons.Controls.Add(cmdPrintEmp);
        buttons.Controls.Add(leave);

        fraEmpCard.Controls.Add(buttons);
        fraEmpCard.Controls.Add(grid);
    }

    private void ApplyMode()
    {
        var trial = optTrial.Checked;
        var employee = optEmployee.Checked;
        var goodLuck = optGoodLuck.Checked;
        fraEmpCard.Visible = employee;
        fraMemCard.Visible = !employee;
        fraMemCard.Controls.OfType<TableLayoutPanel>().First(panel => panel.Name == "trial").Visible = trial;
        cmdRead.Visible = !trial;
        txtCardNo.Visible = !trial;
        txtMemberCode.Visible = !trial;
        txtLeag_Name.Visible = !trial;
        txtValiddate.Visible = !trial && !goodLuck;
        txtGoodLuckDate.Visible = goodLuck && !trial;
        if (trial)
        {
            cmdPrint.Enabled = true;
            UpdatePreviewTrial();
        }
        else if (!employee)
        {
            UpdatePreviewMember();
        }

        if (employee)
            txtCardNum.Focus();
    }

    private async Task ReadCardAsync()
    {
        if (_busy)
            return;
        _busy = true;
        cmdRead.Enabled = false;
        try
        {
            SetReaderStatus("正在讀取資料...");
            CardIdRead read;
            try
            {
                read = await Task.Run(_reader.ReadCardId).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is CardReaderException or IOException or InvalidOperationException)
            {
                _logger.LogError(ex, "Card read failed");
                SetReaderStatus(ex.Message);
                Ui.Warn(this, ex.Message, Text);
                return;
            }

            if (!read.CardPresent || CardCodec.IsBlankCardId(read.CardId) || read.CardId.Length == 0)
            {
                SetReaderStatus("ＩＣ卡不存在");
                return;
            }

            await ShowMemberAsync(read.CardId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Member lookup failed");
            SetReaderStatus(ex.Message);
            Ui.Error(this, ex.Message);
        }
        finally
        {
            _busy = false;
            cmdRead.Enabled = true;
        }
    }

    private async Task ShowMemberAsync(string cardId)
    {
        var count = await _leager.CountActiveByCardAsync(cardId).ConfigureAwait(true);
        if (count >= 1)
        {
            var row = await _leager.FindLabelByCardAsync(cardId).ConfigureAwait(true);
            if (row is null || string.IsNullOrWhiteSpace(row.Number))
            {
                ClearMember();
                SetReaderStatus("此會員不存在");
                return;
            }

            txtCardNo.Text = cardId;
            txtMemberCode.Text = row.OldCardNumber;
            txtLeag_Name.Text = row.Name;
            txtValiddate.Text = row.ValidDate;
            UpdatePreviewMember();
            cmdPrint.Enabled = true;
            SetReaderStatus("讀取成功!");
            return;
        }

        if (!string.Equals(_transferCard, cardId, StringComparison.OrdinalIgnoreCase))
        {
            await _leager.RequestCardTransferAsync(cardId).ConfigureAwait(true);
            _transferCard = cardId;
            SetReaderStatus("會員正在過場...");
            return;
        }

        SetReaderStatus("正在讀取資料...");
    }

    private void PrintMember()
    {
        var goodLuck = optGoodLuck.Checked;
        var trial = optTrial.Checked;
        string memId;
        string memName;
        string validSource;
        if (optMember.Checked || goodLuck)
        {
            if (string.IsNullOrWhiteSpace(txtMemberCode.Text))
            {
                Ui.Warn(this, "請先讀取會員卡資料", Text);
                return;
            }

            memId = txtMemberCode.Text;
            memName = txtLeag_Name.Text;
            validSource = txtValiddate.Text;
        }
        else if (trial)
        {
            if (string.IsNullOrWhiteSpace(txtTempMem0.Text))
            {
                Ui.Warn(this, "請輸入會員編號!", Text);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTempMem1.Text))
            {
                Ui.Warn(this, "請輸入會員名稱!", Text);
                return;
            }

            if (txtTempMem2.Text.StartsWith("20__", StringComparison.Ordinal) || txtTempMem2.Text.Length != 10)
            {
                Ui.Warn(this, "請輸入有效日期年份!", Text);
                return;
            }

            if (!LabelChoice.TrialDateIsReady(txtTempMem2.Text))
            {
                Ui.Warn(this, "年份必須為數字!", Text);
                return;
            }

            memId = txtTempMem0.Text;
            memName = txtTempMem1.Text;
            validSource = txtTempMem2.Text;
        }
        else
        {
            return;
        }

        var slot = LabelChoice.ForMember(goodLuck, memName);
        if (!TemplateIsPresent(slot))
            return;

        var valid = LabelChoice.PrintedValidDate(goodLuck, validSource);
        PrintSlot(slot, NameplateBinding.ForLabel(memId, memName, valid));
        cmdPrint.Enabled = true;
    }

    private async Task LookupEmployeeAsync()
    {
        var card = txtCardNum.Text.Trim();
        if (card.Length == 0)
        {
            Ui.Warn(this, "請輸入卡號碼!", Text);
            txtCardNum.Focus();
            return;
        }

        if (!card.All(static character => character is >= '0' and <= '9'))
        {
            Ui.Warn(this, "卡號碼必定為數字!", Text);
            return;
        }

        txtStaffNo.Text = "";
        txtEmpName.Text = "";
        try
        {
            var row = await _employees.FindByCardIdAsync(card).ConfigureAwait(true);
            if (row is null)
            {
                cmdPrintEmp.Enabled = false;
                Ui.Warn(this, "找不到此卡號!" + Environment.NewLine + card, Text);
                return;
            }

            txtStaffNo.Text = row.StaffNumber;
            txtEmpName.Text = row.EnglishName;
            cmdPrintEmp.Enabled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Employee lookup failed");
            cmdPrintEmp.Enabled = false;
            Ui.Error(this, ex.Message);
        }
    }

    private void PrintEmployee()
    {
        var slot = LabelChoice.ForEmployee(txtEmpName.Text);
        if (!TemplateIsPresent(slot))
            return;
        PrintSlot(slot, NameplateBinding.ForLabel(txtStaffNo.Text, txtEmpName.Text, ""));
    }

    private bool TemplateIsPresent(LabelTemplateSlot slot)
    {
        var loaded = _catalog.Load(slot);
        if (loaded.Status != TemplateLoadStatus.Missing)
            return true;

        Ui.Warn(this, "找不到檔案:" + Path.Combine(AppContext.BaseDirectory, slot.FileName()), Text);
        return false;
    }

    private void PrintSlot(LabelTemplateSlot slot, NameplateBinding binding)
    {
        try
        {
            var job = _pipeline.Create(slot, binding);
            if (job.UsedFallbackLayout &&
                !Ui.Confirm(this, job.Template.Message + Environment.NewLine + Environment.NewLine + "要列印內建版面嗎？"))
            {
                return;
            }

            using var bitmap = NameplateRenderer.Render(job.Layout, job.Binding);
            NameplatePrinter.Print(bitmap, job.Layout, job.DocumentName, job.Copies, _options.Value.PrinterName);
            SetReaderStatus("已將名牌送到打印機。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print failed");
            Ui.Error(this, ex.Message);
        }
    }

    private void UpdatePreviewMember()
    {
        var name = txtLeag_Name.Text.Trim();
        var showLong = optGoodLuck.Checked || name.Length >= LabelChoice.ShortNameLength;
        picLabel1.Visible = !showLong;
        picLabel2.Visible = showLong;
        var until = optGoodLuck.Checked ? LabelChoice.GoodLuckValidUntil : LabelChoice.StandardValidUntil;
        var date = LabelChoice.PrintedValidDate(optGoodLuck.Checked, txtValiddate.Text);
        SetPair(txtMemberCode.Text.Trim(), name, until, date);
    }

    private void UpdatePreviewTrial()
    {
        var name = txtTempMem1.Text.Trim();
        var showLong = name.Length >= LabelChoice.ShortNameLength;
        picLabel1.Visible = !showLong;
        picLabel2.Visible = showLong;
        SetPair(txtTempMem0.Text.Trim(), name, LabelChoice.StandardValidUntil, LabelChoice.PrintedYear(txtTempMem2.Text));
    }

    private void SetPair(string id, string name, string until, string date)
    {
        lblMemID.Text = id;
        lblMemName.Text = name;
        lblValidUntil.Text = until;
        lblValidDate.Text = date;
        lblMemID2.Text = id;
        lblMemName2.Text = name;
        lblValidUntil2.Text = until;
        lblValidDate2.Text = date;
    }

    private void ClearMember()
    {
        txtCardNo.Text = "";
        txtMemberCode.Text = "";
        txtLeag_Name.Text = "";
        txtValiddate.Text = "";
        SetPair("", "", "", "");
        cmdPrint.Enabled = optTrial.Checked;
    }

    private void OpenReader()
    {
        using var form = new ReaderModelForm(_reader);
        if (form.ShowDialog(this) == DialogResult.OK)
            RefreshChrome();
    }

    private void RefreshChrome()
    {
        _databaseName.Text = _database.MemberDatabaseLabel;
        _readerModel.Text = "讀卡器型號: " + _reader.Settings.Model;
    }

    private void SetReaderStatus(string text) => _readerStatus.Text = text;

    private static void AddMemberRow(TableLayoutPanel grid, int row, Control caption, Control value)
    {
        caption.Margin = new Padding(0, 6, 8, 4);
        value.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(caption, 0, row);
        grid.Controls.Add(value, 1, row);
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true };

    private static Label FieldLabel() => new()
    {
        AutoSize = false,
        Width = 360,
        Height = 24,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label PreviewLabel() => new()
    {
        AutoSize = false,
        Height = 20,
        Width = 250,
        Font = new Font("Arial", 9F, FontStyle.Bold, GraphicsUnit.Point),
        BackColor = Color.White
    };

    private static void FillPreview(Panel picture, Label id, Label name, Label until, Label date)
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8, 4, 4, 4),
            BackColor = Color.White
        };
        stack.Controls.Add(id);
        stack.Controls.Add(name);
        var line = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.White };
        until.Width = 90;
        date.Width = 150;
        line.Controls.Add(until);
        line.Controls.Add(date);
        stack.Controls.Add(line);
        picture.Controls.Add(stack);
    }
}
