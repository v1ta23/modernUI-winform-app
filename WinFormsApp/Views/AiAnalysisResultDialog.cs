using App.Core.Models;

namespace WinFormsApp.Views;

internal sealed class AiAnalysisResultDialog : Form
{
    public AiAnalysisResultDialog(RiskAnalysisResult analysis)
    {
        Text = "AI 分析结果";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 480);
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        Controls.Add(BuildContent(analysis));
    }

    private Control BuildContent(RiskAnalysisResult analysis)
    {
        var shell = PageChrome.CreateSurfacePanel(new Padding(22), 16);
        shell.Margin = Padding.Empty;

        var titleLabel = PageChrome.CreateTextLabel(
            "AI 分析结果",
            15F,
            FontStyle.Bold,
            PageChrome.TextPrimary,
            new Padding(0, 0, 0, 6));
        var decisionLabel = PageChrome.CreateTextLabel(
            analysis.DecisionTitle,
            9.5F,
            FontStyle.Regular,
            PageChrome.TextMuted,
            new Padding(0, 0, 0, 14));

        var resultBox = new TextBox
        {
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = PageChrome.TextPrimary,
            Margin = Padding.Empty,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = BuildResultText(analysis)
        };

        var closeButton = PageChrome.CreateActionButton("知道了", PageChrome.AccentBlue, true);
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
        layout.Controls.Add(decisionLabel, 0, 1);
        layout.Controls.Add(resultBox, 0, 2);
        layout.Controls.Add(actions, 0, 3);

        shell.Controls.Add(layout);
        return shell;
    }

    private static string BuildResultText(RiskAnalysisResult analysis)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, [
            $"风险等级：{analysis.RiskLevel}",
            $"等级说明：{analysis.RiskLevelNote}",
            $"重点产线：{analysis.PrimaryLineName}",
            $"产线说明：{analysis.PrimaryLineNote}",
            $"处理建议：{analysis.ActionTitle}",
            $"建议说明：{analysis.ActionNote}",
            $"主要原因：{analysis.RiskReason}",
            $"优先处理：{analysis.PriorityAction}",
            $"管理建议：{analysis.ManagementAdvice}"
        ]);
    }
}
