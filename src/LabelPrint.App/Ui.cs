namespace LabelPrint;

internal static class Ui
{
    public static void Error(IWin32Window owner, string message) =>
        MessageBox.Show(owner, message, "LabelPrint", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static void Info(IWin32Window owner, string message) =>
        MessageBox.Show(owner, message, "LabelPrint", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(IWin32Window owner, string message, string title) =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Confirm(IWin32Window owner, string message) =>
        MessageBox.Show(owner, message, "LabelPrint", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
}

internal static class NamePickForm
{
    public static int? Choose(IWin32Window owner, string title, IReadOnlyList<string> captions)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(460, 320),
            Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point)
        };
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (var caption in captions)
            list.Items.Add(caption);
        if (list.Items.Count > 0)
            list.SelectedIndex = 0;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var ok = new Button { Text = "確定", DialogResult = DialogResult.OK, Width = 88 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(list, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        list.DoubleClick += (_, _) =>
        {
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        if (form.ShowDialog(owner) != DialogResult.OK || list.SelectedIndex < 0)
            return null;
        return list.SelectedIndex;
    }
}
