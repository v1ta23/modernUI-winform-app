namespace WinFormsApp.Views;

internal sealed class TextReportDialog : Form
{
    private readonly string _reportText;

    public TextReportDialog(string title, string subtitle, string reportText)
    {
        _reportText = reportText;

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(680, 500);
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        Controls.Add(BuildContent(title, subtitle));
    }

    private Control BuildContent(string title, string subtitle)
    {
        var shell = PageChrome.CreateSurfacePanel(new Padding(22), 16);
        shell.Margin = Padding.Empty;

        var titleLabel = PageChrome.CreateTextLabel(
            title,
            15F,
            FontStyle.Bold,
            PageChrome.TextPrimary,
            new Padding(0, 0, 0, 6));
        var subtitleLabel = PageChrome.CreateTextLabel(
            subtitle,
            9.5F,
            FontStyle.Regular,
            PageChrome.TextMuted,
            new Padding(0, 0, 0, 14));

        var reportBox = new TextBox
        {
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = PageChrome.TextPrimary,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = _reportText
        };

        var copyButton = PageChrome.CreateActionButton("复制内容", PageChrome.AccentGreen, false);
        copyButton.Margin = new Padding(10, 0, 0, 0);
        copyButton.Click += (_, _) =>
        {
            Clipboard.SetText(_reportText);
            MessageBox.Show(this, "内容已复制。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var closeButton = PageChrome.CreateActionButton("关闭", PageChrome.AccentBlue, true);
        closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        AcceptButton = closeButton;
        CancelButton = closeButton;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 16, 0, 0),
            Padding = Padding.Empty,
            WrapContents = false
        };
        actions.Controls.Add(closeButton);
        actions.Controls.Add(copyButton);

        var layout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(subtitleLabel, 0, 1);
        layout.Controls.Add(reportBox, 0, 2);
        layout.Controls.Add(actions, 0, 3);

        shell.Controls.Add(layout);
        return shell;
    }
}
