using LabelPrint.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelPrint;

/// <summary>Legacy Form1. Employee and member nameplates, plus the menu into the card and reader forms.</summary>
internal sealed class MainForm : Form
{
    private readonly DeskSession _session;
    private readonly ILeagerRepository _members;
    private readonly IMtUserRepository _employees;
    private readonly INameplatePrintPipeline _pipeline;
    private readonly ILabelTemplateCatalog _catalog;
    private readonly ICardReader _reader;
    private readonly MisConnectionFactory _database;
    private readonly IOptions<LabelPrintOptions> _options;
    private readonly IServiceProvider _services;
    private readonly ILogger<MainForm> _logger;
    private readonly TextBox txtStaffNo;
    private readonly TextBox txtEmpName;
    private readonly Label lblEmpEnglish;
    private readonly Button cmdGetEmp;
    private readonly Button cmdPrintEmp;
    private readonly Button cmdPreviewEmp;
    private readonly RadioButton optEmp1;
    private readonly RadioButton optEmp2;
    private readonly PictureBox picEmp;
    private readonly TextBox txtLeag_No;
    private readonly TextBox txtLeag_Name;
    private readonly TextBox txtCardNo;
    private readonly TextBox txtMemberCode;
    private readonly TextBox txtValiddate;
    private readonly TextBox txtTempMem;
    private readonly Button cmdGetMem;
    private readonly Button cmdRead;
    private readonly Button cmdPrint;
    private readonly Button cmdPreview;
    private readonly RadioButton optMember1;
    private readonly RadioButton optMember2;
    private readonly RadioButton optMember3;
    private readonly PictureBox picLabel1;
    private readonly PictureBox picLabel2;
    private readonly Label lblMemName;
    private readonly Label lblMemName2;
    private readonly ToolStripStatusLabel _status;
    private readonly ToolStripStatusLabel _statusDb;
    private readonly ToolStripStatusLabel _statusReader;
    private readonly ToolStripStatusLabel _statusTemplate;
    private readonly SplitContainer _split;
    private bool _loading;
    private bool _busy;

    public MainForm(
        DeskSession session,
        ILeagerRepository members,
        IMtUserRepository employees,
        INameplatePrintPipeline pipeline,
        ILabelTemplateCatalog catalog,
        ICardReader reader,
        MisConnectionFactory database,
        IOptions<LabelPrintOptions> options,
        IServiceProvider services,
        ILogger<MainForm> logger)
    {
        _session = session;
        _members = members;
        _employees = employees;
        _pipeline = pipeline;
        _catalog = catalog;
        _reader = reader;
        _database = database;
        _options = options;
        _services = services;
        _logger = logger;

        Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "LabelPrint 1.06";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 740);
        MinimumSize = new Size(960, 640);
        AutoScaleMode = AutoScaleMode.Dpi;

        txtStaffNo = Box();
        txtEmpName = Box();
        lblEmpEnglish = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) };
        cmdGetEmp = Button("查詢");
        cmdPrintEmp = Button("列印員工");
        cmdPreviewEmp = Button("預覽");
        optEmp1 = Radio("員工名牌 1", true);
        optEmp2 = Radio("員工名牌 2", false);
        picEmp = Preview();
        txtLeag_No = Box();
        txtLeag_Name = Box();
        txtCardNo = Box();
        txtMemberCode = Box();
        txtValiddate = Box();
        txtTempMem = Box();
        cmdGetMem = Button("查詢");
        cmdRead = Button("讀卡");
        cmdPrint = Button("列印會員");
        cmdPreview = Button("預覽");
        optMember1 = Radio("會員名牌 1", true);
        optMember2 = Radio("會員名牌 2", false);
        optMember3 = Radio("會員名牌 3", false);
        picLabel1 = Preview();
        picLabel2 = Preview();
        lblMemName = Caption("會員姓名");
        lblMemName2 = Caption("會員姓名");
        _status = new ToolStripStatusLabel("就緒") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusDb = StatusCell(160);
        _statusReader = StatusCell(340);
        _statusTemplate = StatusCell(120);
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };

        var menu = BuildMenu();
        BuildEmployee(_split.Panel1);
        BuildMember(_split.Panel2);
        var status = new StatusStrip();
        status.Items.AddRange([_status, _statusDb, _statusReader, _statusTemplate]);
        Controls.Add(_split);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;
        Wire();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            _split.SplitterDistance = 250;
        }
        catch (InvalidOperationException)
        {
            // The splitter rejects a distance before the handle has a real height. The default split still works.
        }

        RefreshPreviews();
        RefreshStatus();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("檔案(&F)");
        file.DropDownItems.Add("結束(&X)", null, (_, _) => Close());

        var card = new ToolStripMenuItem("智能卡(&C)");
        card.DropDownItems.Add("會員卡(&M)...", null, (_, _) => OpenMemberCard());
        card.DropDownItems.Add("讀卡(&R)", null, async (_, _) => await RunBusy(ReadCardAsync));

        var settings = new ToolStripMenuItem("設定(&S)");
        settings.DropDownItems.Add("讀卡機型號(&E)...", null, (_, _) => OpenReader());
        settings.DropDownItems.Add("測試 MIS 連線(&T)", null, async (_, _) => await RunBusy(TestMisAsync));

        var help = new ToolStripMenuItem("說明(&H)");
        help.DropDownItems.Add("關於 LabelPrint(&A)", null, (_, _) => ShowAbout());

        menu.Items.AddRange([file, card, settings, help]);
        return menu;
    }

    private void BuildEmployee(Control parent)
    {
        var group = new GroupBox { Text = "員工名牌", Dock = DockStyle.Fill, Padding = new Padding(8) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var fields = FieldGrid();
        AddRow(fields, 0, Label("員工編號"), txtStaffNo, cmdGetEmp);
        AddSpan(fields, 1, Label("員工姓名"), txtEmpName);
        AddSpan(fields, 2, Label("英文姓名"), lblEmpEnglish);
        var radios = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        radios.Controls.Add(optEmp1);
        radios.Controls.Add(optEmp2);
        fields.Controls.Add(radios, 0, 3);
        fields.SetColumnSpan(radios, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        buttons.Controls.Add(cmdPrintEmp);
        buttons.Controls.Add(cmdPreviewEmp);
        fields.Controls.Add(buttons, 0, 4);
        fields.SetColumnSpan(buttons, 3);

        grid.Controls.Add(fields, 0, 0);
        grid.Controls.Add(picEmp, 1, 0);
        group.Controls.Add(grid);
        parent.Controls.Add(group);
    }

    private void BuildMember(Control parent)
    {
        var group = new GroupBox { Text = "會員名牌", Dock = DockStyle.Fill, Padding = new Padding(8) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 4 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var row = 0; row < 4; row++)
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        fields.Controls.Add(Label("會員編號"), 0, 0);
        fields.Controls.Add(txtLeag_No, 1, 0);
        fields.Controls.Add(cmdGetMem, 2, 0);
        fields.Controls.Add(Label("會員姓名"), 0, 1);
        fields.Controls.Add(txtLeag_Name, 1, 1);
        fields.SetColumnSpan(txtLeag_Name, 3);
        fields.Controls.Add(Label("卡號"), 0, 2);
        fields.Controls.Add(txtCardNo, 1, 2);
        fields.Controls.Add(Label("會員代碼"), 2, 2);
        fields.Controls.Add(txtMemberCode, 3, 2);
        fields.Controls.Add(Label("有效日期"), 0, 3);
        fields.Controls.Add(txtValiddate, 1, 3);
        fields.Controls.Add(Label("臨時會員"), 2, 3);
        fields.Controls.Add(txtTempMem, 3, 3);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        var memberRadios = new Panel { Width = 430, Height = 28 };
        optMember1.Location = new Point(0, 4);
        optMember2.Location = new Point(140, 4);
        optMember3.Location = new Point(280, 4);
        memberRadios.Controls.Add(optMember1);
        memberRadios.Controls.Add(optMember2);
        memberRadios.Controls.Add(optMember3);
        actions.Controls.Add(memberRadios);
        actions.Controls.Add(cmdRead);
        actions.Controls.Add(cmdPrint);
        actions.Controls.Add(cmdPreview);

        var previews = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        previews.Controls.Add(picLabel1, 0, 0);
        previews.Controls.Add(picLabel2, 1, 0);

        var captions = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Height = 24 };
        captions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        captions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        captions.Controls.Add(lblMemName, 0, 0);
        captions.Controls.Add(lblMemName2, 1, 0);

        grid.Controls.Add(fields, 0, 0);
        grid.Controls.Add(actions, 0, 1);
        grid.Controls.Add(previews, 0, 2);
        grid.Controls.Add(captions, 0, 3);
        group.Controls.Add(grid);
        parent.Controls.Add(group);
    }

    private void Wire()
    {
        cmdGetEmp.Click += async (_, _) => await RunBusy(LookupEmployeeAsync);
        cmdGetMem.Click += async (_, _) => await RunBusy(LookupMemberAsync);
        cmdRead.Click += async (_, _) => await RunBusy(ReadCardAsync);
        cmdPrintEmp.Click += (_, _) => PrintEmployee(preview: false);
        cmdPreviewEmp.Click += (_, _) => PrintEmployee(preview: true);
        cmdPrint.Click += (_, _) => PrintMember(preview: false);
        cmdPreview.Click += (_, _) => PrintMember(preview: true);
        txtStaffNo.KeyDown += (_, e) => Activate(e, cmdGetEmp);
        txtLeag_No.KeyDown += (_, e) => Activate(e, cmdGetMem);
        txtStaffNo.TextChanged += (_, _) =>
        {
            if (_loading)
                return;
            _session.Employee.EnglishName = "";
            _session.Employee.ChineseName = "";
            _session.Employee.Id = "";
            lblEmpEnglish.Text = "";
            RefreshPreviews();
        };
        foreach (var box in new Control[] { txtEmpName, txtLeag_No, txtLeag_Name, txtCardNo, txtMemberCode, txtValiddate, txtTempMem })
            box.TextChanged += (_, _) => { if (!_loading) RefreshPreviews(); };
        foreach (var radio in new[] { optEmp1, optEmp2, optMember1, optMember2, optMember3 })
            radio.CheckedChanged += (_, _) => { if (!_loading) RefreshPreviews(); };
    }

    private async Task LookupEmployeeAsync()
    {
        var staffNo = txtStaffNo.Text.Trim();
        var name = txtEmpName.Text.Trim();
        if (staffNo.Length == 0 && name.Length == 0)
        {
            Ui.Info(this, "請輸入員工編號或姓名。");
            return;
        }

        if (staffNo.Length > 0)
        {
            var record = await _employees.FindByStaffNumberAsync(staffNo);
            if (record is null)
            {
                Ui.Info(this, $"找不到員工編號 {staffNo}。");
                return;
            }

            ShowEmployee(record);
            return;
        }

        var rows = await _employees.SearchByNameAsync(name);
        if (rows.Count == 0)
        {
            Ui.Info(this, $"找不到符合「{name}」的員工。");
            return;
        }

        var index = rows.Count == 1
            ? 0
            : NamePickForm.Choose(this, "員工", rows.Select(row => $"{row.StaffNumber}  {row.DisplayName}").ToList());
        if (index is int chosen)
            ShowEmployee(rows[chosen]);
    }

    private async Task LookupMemberAsync()
    {
        var number = txtLeag_No.Text.Trim();
        var card = txtCardNo.Text.Trim();
        var name = txtLeag_Name.Text.Trim();
        if (number.Length == 0 && card.Length == 0 && name.Length == 0)
        {
            Ui.Info(this, "請輸入會員編號、卡號或姓名。");
            return;
        }

        if (number.Length > 0)
        {
            var record = await _members.FindByNumberAsync(number);
            if (record is null)
            {
                Ui.Info(this, $"找不到會員編號 {number}。");
                return;
            }

            ShowMember(record);
            return;
        }

        if (card.Length > 0)
        {
            var record = await _members.FindByOldCardNumberAsync(card);
            if (record is null)
            {
                Ui.Info(this, $"找不到卡號 {card}。");
                return;
            }

            ShowMember(record);
            return;
        }

        var rows = await _members.SearchByNameAsync(name);
        if (rows.Count == 0)
        {
            Ui.Info(this, $"找不到符合「{name}」的會員。");
            return;
        }

        var index = rows.Count == 1
            ? 0
            : NamePickForm.Choose(this, "會員", rows.Select(row => $"{row.Number}  {row.Name}").ToList());
        if (index is int chosen)
            ShowMember(rows[chosen]);
    }

    private async Task ReadCardAsync()
    {
        var memory = await Task.Run(_reader.ReadMemberArea);
        var memberNumber = memory.MemberNumber ?? "";
        _loading = true;
        try
        {
            txtCardNo.Text = memberNumber;
            if (memberNumber.Length > 0)
                txtLeag_No.Text = memberNumber;
        }
        finally
        {
            _loading = false;
        }

        SetStatus($"已讀取偏移 {memory.Offset} 的 {memory.Bytes.Length} 位元組。");
        _logger.LogInformation("Read member area, {Length} bytes at offset {Offset}", memory.Bytes.Length, memory.Offset);
        if (memberNumber.Length > 0)
            await LookupMemberAsync();
        else
            RefreshPreviews();
    }

    private async Task TestMisAsync()
    {
        await _database.CanConnectAsync();
        Ui.Info(this, "MIS 已接受連線。");
        SetStatus("MIS 連線成功。");
    }

    private void PrintEmployee(bool preview)
    {
        try
        {
            PullUi();
            if (string.IsNullOrWhiteSpace(_session.Employee.PrintedName))
            {
                Ui.Info(this, "列印前請輸入員工姓名。");
                return;
            }

            var job = _pipeline.Create(_session.EmployeeTemplate, _session.Employee.ToBinding());
            if (!AcceptJob(job))
                return;

            using var bitmap = NameplateRenderer.Render(job.Layout, job.Binding);
            if (preview)
                NameplatePrinter.Preview(this, bitmap, job.Layout, job.DocumentName, _options.Value.PrinterName);
            else
                NameplatePrinter.Print(bitmap, job.Layout, job.DocumentName, job.Copies, _options.Value.PrinterName);

            SetStatus(preview ? "已關閉員工名牌預覽。" : "已將員工名牌送到打印機。");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void PrintMember(bool preview)
    {
        try
        {
            PullUi();
            if (string.IsNullOrWhiteSpace(_session.Member.Name))
            {
                Ui.Info(this, "列印前請輸入會員姓名。");
                return;
            }

            var job = _pipeline.Create(_session.MemberTemplate, _session.Member.ToBinding());
            if (!AcceptJob(job))
                return;

            using var bitmap = NameplateRenderer.Render(job.Layout, job.Binding);
            if (preview)
                NameplatePrinter.Preview(this, bitmap, job.Layout, job.DocumentName, _options.Value.PrinterName);
            else
                NameplatePrinter.Print(bitmap, job.Layout, job.DocumentName, job.Copies, _options.Value.PrinterName);

            SetStatus(preview ? "已關閉會員名牌預覽。" : "已將會員名牌送到打印機。");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private bool AcceptJob(NameplateJob job)
    {
        if (!job.UsedFallbackLayout)
            return true;

        return Ui.Confirm(this, job.Template.Message + Environment.NewLine + Environment.NewLine + "要列印內建版面嗎？");
    }

    private void ShowEmployee(EmployeeRecord record)
    {
        _loading = true;
        try
        {
            _session.Employee.Apply(record);
            txtStaffNo.Text = record.StaffNumber;
            txtEmpName.Text = record.DisplayName;
            lblEmpEnglish.Text = record.EnglishName;
        }
        finally
        {
            _loading = false;
        }

        RefreshPreviews();
        SetStatus($"已載入員工 {record.StaffNumber}。");
    }

    private void ShowMember(LeagerRecord record)
    {
        _loading = true;
        try
        {
            _session.Member.Apply(record);
            PushMemberFields();
        }
        finally
        {
            _loading = false;
        }

        RefreshPreviews();
        SetStatus($"已載入會員 {record.Number}。");
    }

    private void OpenMemberCard()
    {
        PullUi();
        using var form = ActivatorUtilities.CreateInstance<MemberCardForm>(_services);
        form.ShowDialog(this);
        _loading = true;
        try
        {
            PushMemberFields();
        }
        finally
        {
            _loading = false;
        }

        RefreshPreviews();
        RefreshStatus();
    }

    private void OpenReader()
    {
        using var form = ActivatorUtilities.CreateInstance<ReaderModelForm>(_services);
        form.ShowDialog(this);
        RefreshStatus();
    }

    private void ShowAbout()
    {
        var dll = _reader.NativeLibraryPresent ? "已找到" : "找不到";
        var dependency = _reader.DependencyLibraryPresent ? "已找到" : "找不到";
        Ui.Info(this,
            "LabelPrint 1.06" + Environment.NewLine +
            "Jumping Gym U.S.A" + Environment.NewLine +
            ".NET 8 重寫的 VB6 櫃檯程式。" + Environment.NewLine + Environment.NewLine +
            "名牌範本：" + Environment.NewLine + _catalog.SearchPathDescription + Environment.NewLine + Environment.NewLine +
            "讀卡機設定檔：" + _reader.SettingsPath + Environment.NewLine +
            "IC_D600.dll：" + dll + Environment.NewLine +
            "acsr2032.dll：" + dependency + Environment.NewLine +
            "記錄檔：" + FileLog.Path);
    }

    private void PullUi()
    {
        _session.Employee.StaffNumber = txtStaffNo.Text.Trim();
        _session.Employee.PrintedName = txtEmpName.Text.Trim();
        _session.EmployeeTemplate = optEmp2.Checked
            ? LabelTemplateSlot.EmployeeNameplate2
            : LabelTemplateSlot.EmployeeNameplate1;
        _session.Member.Number = txtLeag_No.Text.Trim();
        _session.Member.Name = txtLeag_Name.Text.Trim();
        _session.Member.CardNumber = txtCardNo.Text.Trim();
        _session.Member.MemberCode = txtMemberCode.Text.Trim();
        _session.Member.ValidDate = txtValiddate.Text.Trim();
        _session.Member.TempMember = txtTempMem.Text.Trim();
        _session.MemberTemplate = SelectedMemberSlot();
    }

    private void PushMemberFields()
    {
        txtLeag_No.Text = _session.Member.Number;
        txtLeag_Name.Text = _session.Member.Name;
        txtCardNo.Text = _session.Member.CardNumber;
        txtMemberCode.Text = _session.Member.MemberCode;
        txtValiddate.Text = _session.Member.ValidDate;
        txtTempMem.Text = _session.Member.TempMember;
        optMember1.Checked = _session.MemberTemplate == LabelTemplateSlot.MemberNameplate1;
        optMember2.Checked = _session.MemberTemplate == LabelTemplateSlot.MemberNameplate2;
        optMember3.Checked = _session.MemberTemplate == LabelTemplateSlot.MemberNameplate3;
    }

    private void RefreshPreviews()
    {
        PullUi();
        ShowPreview(picEmp, null, _pipeline.Create(_session.EmployeeTemplate, _session.Employee.ToBinding()));
        var selected = _pipeline.Create(_session.MemberTemplate, _session.Member.ToBinding());
        var companionSlot = _session.MemberTemplate switch
        {
            LabelTemplateSlot.MemberNameplate1 => LabelTemplateSlot.MemberNameplate2,
            LabelTemplateSlot.MemberNameplate2 => LabelTemplateSlot.MemberNameplate1,
            _ => LabelTemplateSlot.MemberNameplate1
        };
        var companion = _pipeline.Create(companionSlot, _session.Member.ToBinding());
        ShowPreview(picLabel1, lblMemName, selected);
        ShowPreview(picLabel2, lblMemName2, companion);
    }

    private static void ShowPreview(PictureBox box, Label? caption, NameplateJob job)
    {
        var bitmap = NameplateRenderer.Render(job.Layout, job.Binding);
        var previous = box.Image;
        box.Image = bitmap;
        previous?.Dispose();
        if (caption is null)
            return;

        var note = job.Template.Status == TemplateLoadStatus.Loaded ? "" : "（內建版面）";
        var name = job.Binding.Get(NameplateFields.LeagName);
        caption.Text = (string.IsNullOrWhiteSpace(name) ? "會員姓名" : name) + note;
    }

    private void RefreshStatus()
    {
        var templates = _catalog.LoadAll();
        var ready = templates.Count(result => result.Status == TemplateLoadStatus.Loaded);
        _statusDb.Text = _database.IsConfigured ? "MIS：已設定" : "MIS：未設定";
        _statusReader.Text = _reader.Settings.Model + (_reader.NativeLibraryPresent ? "  已找到 DLL" : "  找不到 DLL");
        _statusTemplate.Text = $"範本 {ready}/{templates.Count}";
    }

    private LabelTemplateSlot SelectedMemberSlot()
    {
        if (optMember2.Checked)
            return LabelTemplateSlot.MemberNameplate2;
        if (optMember3.Checked)
            return LabelTemplateSlot.MemberNameplate3;
        return LabelTemplateSlot.MemberNameplate1;
    }

    private async Task RunBusy(Func<Task> action)
    {
        if (_busy)
            return;

        _busy = true;
        UseWaitCursor = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
            RefreshStatus();
        }
    }

    private void ShowError(Exception ex)
    {
        _logger.LogError(ex, "Desk operation failed");
        Ui.Error(this, ex.Message);
    }

    private void SetStatus(string text) => _status.Text = text;

    private static void Activate(KeyEventArgs args, Button button)
    {
        if (args.KeyCode != Keys.Enter)
            return;
        args.SuppressKeyPress = true;
        button.PerformClick();
    }

    private static TextBox Box() => new()
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 4, 8, 4),
        MinimumSize = new Size(80, 23)
    };

    private static Button Button(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 4, 8, 4),
        Padding = new Padding(8, 2, 8, 2)
    };

    private static RadioButton Radio(string text, bool on) => new()
    {
        Text = text,
        AutoSize = true,
        Checked = on,
        Margin = new Padding(0, 6, 16, 0)
    };

    private static PictureBox Preview() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        SizeMode = PictureBoxSizeMode.Zoom,
        Margin = new Padding(4)
    };

    private static Label Label(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 8, 8, 4)
    };

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Dock = DockStyle.Fill,
        Margin = new Padding(4, 0, 4, 0)
    };

    private static ToolStripStatusLabel StatusCell(int width) => new()
    {
        AutoSize = false,
        Width = width,
        BorderSides = ToolStripStatusLabelBorderSides.Left,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static TableLayoutPanel FieldGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        for (var row = 0; row < 5; row++)
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return grid;
    }

    private static void AddRow(TableLayoutPanel grid, int row, Control label, Control field, Control extra)
    {
        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(field, 1, row);
        grid.Controls.Add(extra, 2, row);
    }

    private static void AddSpan(TableLayoutPanel grid, int row, Control label, Control field)
    {
        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(field, 1, row);
        grid.SetColumnSpan(field, 2);
    }
}
