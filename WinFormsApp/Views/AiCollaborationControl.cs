using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class AiCollaborationControl : UserControl, IInteractiveResizeAware
{
    private static readonly Color PageBackground = PageChrome.PageBackground;
    private static readonly Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static readonly Color SurfaceBorder = PageChrome.SurfaceBorder;
    private static readonly Color TextPrimaryColor = PageChrome.TextPrimary;
    private static readonly Color TextMutedColor = PageChrome.TextMuted;
    private static readonly Color AccentBlue = PageChrome.AccentBlue;
    private static readonly Color AccentGreen = PageChrome.AccentGreen;
    private static readonly Color AccentOrange = PageChrome.AccentOrange;
    private static readonly Color AccentPurple = PageChrome.AccentPurple;
    private static readonly Color AccentRed = PageChrome.AccentRed;

    private readonly InspectionController _inspectionController;
    private readonly Label _generatedAtLabel;
    private readonly Label _summaryLabel;
    private readonly TableLayoutPanel _cardGrid;
    private readonly Control _layoutRoot;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private InspectionDashboardViewModel _dashboard = new();

    public AiCollaborationControl(InspectionController inspectionController)
    {
        _inspectionController = inspectionController;

        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;

        _generatedAtLabel = PageChrome.CreateInfoLabel();
        _summaryLabel = PageChrome.CreateTextLabel(
            "读取巡检数据后，按部门视角生成可复制的处理建议。",
            9.5F,
            FontStyle.Regular,
            TextMutedColor,
            new Padding(0, 0, 0, 10));

        var refreshButton = PageChrome.CreateActionButton("刷新数据", AccentBlue, true);
        refreshButton.Click += (_, _) => RefreshData();
        var historyButton = PageChrome.CreateActionButton("历史记录", AccentPurple, false);
        historyButton.Click += (_, _) => OpenHistory();

        _cardGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _cardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _cardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _cardGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _cardGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = PageChrome.CreatePageHeader(
            "AI 协同",
            "按部门视角生成设备、生产、质量和管理建议。",
            _generatedAtLabel,
            refreshButton,
            historyButton);
        PageChrome.BindControlHeightToRow(rootLayout, 0, header);

        rootLayout.Controls.Add(header, 0, 0);
        rootLayout.Controls.Add(_summaryLabel, 0, 1);
        rootLayout.Controls.Add(_cardGrid, 0, 2);
        _layoutRoot = rootLayout;
        Controls.Add(rootLayout);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _layoutRoot, PageBackground);
        _layoutRoot.BringToFront();

        ApplyTheme();
        RefreshData();
    }

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        Invalidate(true);
    }

    public void BeginInteractiveResize()
    {
        _interactiveResizeController.Begin();
    }

    public void EndInteractiveResize()
    {
        if (!_interactiveResizeController.IsActive)
        {
            return;
        }

        _interactiveResizeController.End();
        _layoutRoot.PerformLayout();
        PerformLayout();
        Invalidate(true);
        Update();
    }

    public void RefreshData()
    {
        _dashboard = _inspectionController.Load(new InspectionFilterViewModel());
        _generatedAtLabel.Text = $"更新时间：{_dashboard.GeneratedAt:yyyy-MM-dd HH:mm}";
        _summaryLabel.Text = $"当前记录 {_dashboard.TotalCount} 条，预警 {_dashboard.WarningCount} 条，异常 {_dashboard.AbnormalCount} 条，合格率 {_dashboard.PassRateText}。";
        BuildCards();
    }

    private void BuildCards()
    {
        var oldCards = _cardGrid.Controls.Cast<Control>().ToList();
        _cardGrid.Controls.Clear();
        foreach (var oldCard in oldCards)
        {
            oldCard.Dispose();
        }

        _cardGrid.Controls.Add(CreateAgentCard(
            AiCollaborationRole.Equipment,
            "设备部建议",
            "聚焦优先检修设备、疑似原因、停机复检和维修安排。",
            AccentGreen,
            new Padding(0, 0, 10, 10)), 0, 0);
        _cardGrid.Controls.Add(CreateAgentCard(
            AiCollaborationRole.Production,
            "生产部建议",
            "判断产线节拍影响、现场协调和排产风险。",
            AccentOrange,
            new Padding(10, 0, 0, 10)), 1, 0);
        _cardGrid.Controls.Add(CreateAgentCard(
            AiCollaborationRole.Quality,
            "质量部建议",
            "关注复检、质量风险、批次追踪和隔离建议。",
            AccentPurple,
            new Padding(0, 10, 10, 0)), 0, 1);
        _cardGrid.Controls.Add(CreateAgentCard(
            AiCollaborationRole.Management,
            "管理层摘要",
            "汇总总体风险、责任部门、处理优先级和协同事项。",
            AccentRed,
            new Padding(10, 10, 0, 0)), 1, 1);
    }

    private Control CreateAgentCard(
        AiCollaborationRole role,
        string title,
        string description,
        Color accentColor,
        Padding margin)
    {
        var shell = PageChrome.CreateSurfacePanel(
            new Padding(18),
            16,
            fillColor: SurfaceBackground,
            borderColor: MixColor(SurfaceBorder, accentColor, 0.45F));
        shell.Margin = margin;

        var titleLabel = PageChrome.CreateTextLabel(
            title,
            13F,
            FontStyle.Bold,
            TextPrimaryColor,
            new Padding(0, 0, 0, 8));
        var descriptionLabel = PageChrome.CreateTextLabel(
            description,
            9F,
            FontStyle.Regular,
            TextMutedColor,
            new Padding(0, 0, 0, 12));
        var detailLabel = PageChrome.CreateTextLabel(
            BuildCardDetail(role),
            9F,
            FontStyle.Regular,
            TextMutedColor,
            new Padding(0, 0, 0, 12));

        var actionButton = PageChrome.CreateActionButton("生成建议", accentColor, true);
        actionButton.Click += async (_, _) => await GenerateAdviceAsync(role, title, actionButton);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(descriptionLabel, 0, 1);
        layout.Controls.Add(detailLabel, 0, 2);
        layout.Controls.Add(actionButton, 0, 3);

        shell.Controls.Add(layout);
        return shell;
    }

    private async Task GenerateAdviceAsync(AiCollaborationRole role, string title, Button button)
    {
        var originalText = button.Text;
        button.Enabled = false;
        button.Text = "生成中...";
        _generatedAtLabel.Text = $"{title}生成中：{DateTime.Now:HH:mm:ss}";
        UseWaitCursor = true;
        button.Refresh();
        _generatedAtLabel.Refresh();
        Update();

        try
        {
            var filter = new InspectionFilterViewModel();
            var report = await Task.Run(() => _inspectionController.GenerateAiCollaborationAdviceAsync(role, filter));
            var historyReport = BuildHistoryReport(title, report);
            _inspectionController.SaveAiAnalysisHistory(
                _dashboard.RiskAnalysis,
                historyReport,
                title,
                "AI 协同");
            _generatedAtLabel.Text = $"{title}已生成并保存：{DateTime.Now:yyyy-MM-dd HH:mm}";
            using var dialog = new TextReportDialog(
                title,
                "AI 协同已根据当前巡检数据生成部门建议，并保存到历史记录。",
                report);
            dialog.ShowDialog(FindForm());
        }
        catch (TaskCanceledException)
        {
            MessageBox.Show(this, "AI 协同生成超时，请稍后再试。", "AI 协同", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "AI 协同", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show(this, $"AI 协同请求失败：{ex.Message}", "AI 协同", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            button.Text = originalText;
            button.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void OpenHistory()
    {
        using var dialog = new AiAnalysisHistoryDialog(_inspectionController.GetAiAnalysisHistory());
        dialog.ShowDialog(FindForm());
    }

    private static string BuildHistoryReport(string title, string report)
    {
        return string.Join(Environment.NewLine, [
            title,
            $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}",
            string.Empty,
            report
        ]);
    }

    private string BuildCardDetail(AiCollaborationRole role)
    {
        var pendingCount = _dashboard.Records.Count(record =>
            record.Status != InspectionStatus.Normal &&
            !record.IsClosed &&
            !record.IsRevoked);
        var topLine = _dashboard.Records
            .Where(record => record.Status != InspectionStatus.Normal)
            .GroupBy(record => record.LineName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                LineName = group.First().LineName,
                AbnormalCount = group.Count(record => record.Status == InspectionStatus.Abnormal),
                WarningCount = group.Count(record => record.Status == InspectionStatus.Warning)
            })
            .OrderByDescending(item => item.AbnormalCount)
            .ThenByDescending(item => item.WarningCount)
            .FirstOrDefault();
        var topDevice = _dashboard.Records
            .Where(record => record.Status != InspectionStatus.Normal)
            .GroupBy(record => $"{record.LineName} / {record.DeviceName}", StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Target = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .FirstOrDefault();

        return role switch
        {
            AiCollaborationRole.Equipment => topDevice is null
                ? "当前暂无突出设备风险。"
                : $"重点设备：{topDevice.Target}，关联风险 {topDevice.Count} 条。",
            AiCollaborationRole.Production => topLine is null
                ? "当前暂无突出产线风险。"
                : $"重点产线：{topLine.LineName}，异常 {topLine.AbnormalCount} 条，预警 {topLine.WarningCount} 条。",
            AiCollaborationRole.Quality => $"需复核记录：{_dashboard.WarningCount + _dashboard.AbnormalCount} 条，建议关注异常闭环。",
            AiCollaborationRole.Management => $"待闭环：{pendingCount} 条，当前风险等级：{_dashboard.RiskAnalysis.RiskLevel}。",
            _ => "等待巡检数据。"
        };
    }

    private static Color MixColor(Color baseColor, Color overlayColor, float overlayWeight)
    {
        var weight = Math.Clamp(overlayWeight, 0F, 1F);
        var baseWeight = 1F - weight;
        return Color.FromArgb(
            (int)Math.Round(baseColor.R * baseWeight + overlayColor.R * weight),
            (int)Math.Round(baseColor.G * baseWeight + overlayColor.G * weight),
            (int)Math.Round(baseColor.B * baseWeight + overlayColor.B * weight));
    }
}
