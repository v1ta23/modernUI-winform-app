using App.Core.Models;

namespace WinFormsApp.Views;

internal sealed class AiAnalysisHistoryDialog : Form
{
    private readonly IReadOnlyList<AiAnalysisHistoryEntry> _entries;
    private readonly ListBox _historyListBox;
    private readonly TextBox _reportTextBox;

    public AiAnalysisHistoryDialog(IReadOnlyList<AiAnalysisHistoryEntry> entries)
    {
        _entries = entries;

        Text = "AI 历史记录";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(780, 520);
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        _historyListBox = CreateHistoryListBox();
        _reportTextBox = CreateReportTextBox();
        Controls.Add(BuildContent());

        foreach (var entry in _entries)
        {
            _historyListBox.Items.Add(new HistoryListItem(entry));
        }

        if (_historyListBox.Items.Count > 0)
        {
            _historyListBox.SelectedIndex = 0;
        }
        else
        {
            _reportTextBox.Text = "暂无 AI 历史记录。生成一次 AI 分析或 AI 协同建议后，这里会自动保存。";
        }
    }

    private Control BuildContent()
    {
        var shell = PageChrome.CreateSurfacePanel(new Padding(22), 16);
        shell.Margin = Padding.Empty;

        var titleLabel = PageChrome.CreateTextLabel(
            "AI 历史记录",
            15F,
            FontStyle.Bold,
            PageChrome.TextPrimary,
            new Padding(0, 0, 0, 6));
        var noteLabel = PageChrome.CreateTextLabel(
            "最近 50 次 AI 分析和协同建议会保存在本机，方便复盘和复制报告。",
            9F,
            FontStyle.Regular,
            PageChrome.TextMuted,
            new Padding(0, 0, 0, 14));

        var contentLayout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _historyListBox.Margin = new Padding(0, 0, 12, 0);
        contentLayout.Controls.Add(_historyListBox, 0, 0);
        contentLayout.Controls.Add(_reportTextBox, 1, 0);

        var copyButton = PageChrome.CreateActionButton("复制报告", PageChrome.AccentGreen, false);
        copyButton.Margin = new Padding(10, 0, 0, 0);
        copyButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_reportTextBox.Text))
            {
                return;
            }

            Clipboard.SetText(_reportTextBox.Text);
            MessageBox.Show(this, "报告已复制。", "AI 历史记录", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        layout.Controls.Add(noteLabel, 0, 1);
        layout.Controls.Add(contentLayout, 0, 2);
        layout.Controls.Add(actions, 0, 3);

        shell.Controls.Add(layout);
        return shell;
    }

    private ListBox CreateHistoryListBox()
    {
        var listBox = new ListBox
        {
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            ForeColor = PageChrome.TextPrimary,
            IntegralHeight = false
        };
        listBox.SelectedIndexChanged += (_, _) =>
        {
            if (listBox.SelectedItem is HistoryListItem item)
            {
                _reportTextBox.Text = item.Entry.ReportText;
            }
        };
        return listBox;
    }

    private static TextBox CreateReportTextBox()
    {
        return new TextBox
        {
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = PageChrome.TextPrimary,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical
        };
    }

    private sealed class HistoryListItem
    {
        public HistoryListItem(AiAnalysisHistoryEntry entry)
        {
            Entry = entry;
        }

        public AiAnalysisHistoryEntry Entry { get; }

        public override string ToString()
        {
            var title = string.IsNullOrWhiteSpace(Entry.Title)
                ? $"{Entry.Analysis.RiskLevel} {Entry.Analysis.PrimaryLineName}"
                : Entry.Title;
            var category = string.IsNullOrWhiteSpace(Entry.Category)
                ? "AI 分析"
                : Entry.Category;
            return $"{Entry.CreatedAt:MM-dd HH:mm}  {category}  {title}";
        }
    }
}
